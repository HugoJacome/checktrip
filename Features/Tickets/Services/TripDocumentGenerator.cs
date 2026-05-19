using CheckTrip.Web.Data.Entities;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CheckTrip.Web.Features.Tickets.Services;

public static class TripDocumentGenerator
{
    private const string FontSizeNormal = "14";
    private const string FontSizeSmall = "12";
    private const string FontSizeTitle = "18";

    public static byte[] GeneratePassengerListDocument(
        BoatDailyTrip trip,
        BoatRouteSchedule schedule,
        BoatDailyTripCrew crew,
        List<ReservationPassengerTrip> passengers)
    {
        using var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(
                   stream,
                   WordprocessingDocumentType.Document,
                   true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();

            var body = new Body();

            body.Append(CreateHeader());
            body.Append(CreateSpacer(120));
            body.Append(CreateTravelAndBoatInfoTable(trip, schedule));
            //body.Append(CreateSpacer(40));
            body.Append(CreateOwnerResponsibleAndCrewTable(schedule, crew));

            body.Append(CreateSectionTitle("VI. LISTA DE PASAJEROS"));
            body.Append(CreatePassengerTable(passengers));

            body.Append(CreateSpacer(10)); 
            body.Append(CreateResponsibilityAndSignatureTable());

            body.Append(CreateSectionProperties());

            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph CreateHeader()
    {
        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center }),
            CreateRun("ARMADA DEL ECUADOR", true, FontSizeTitle),
            new Run(new Break()),
            CreateRun("CAPITANÍA DE PUERTO DE PUERTO AYORA", true, FontSizeTitle));
    }

    private static Table CreateTravelAndBoatInfoTable(
    BoatDailyTrip trip,
    BoatRouteSchedule schedule)
    {
        var table = CreateTable();

        table.Append(new TableRow(
            CreateCell("I. INFORMACIÓN DEL VIAJE", true, 5000, GridSpan(4), Shading()),
            CreateCell("II. INFORMACIÓN DE LA NAVE", true, 5000, GridSpan(4), Shading())
        ));

        table.Append(CreateEightColumnRow(
            "Fecha:", trip.TripDate.ToString("dd/MM/yyyy"),
            "Puerto de Zarpe:", schedule.Route.Origin ?? "",
            "Nombres:", schedule.Boat.Name,
            "Capacidad Tripulantes:", "4"));

        table.Append(CreateEightColumnRow(
            "Actividad:", schedule.Route.Type ?? "",
            "Puerto de Arribo:", schedule.Route.Destination ?? "",
            "Matrícula:", schedule.Boat.RegistrationNumber ?? "",
            "Capacidad Pasajeros:", (schedule.Boat.Capacity + schedule.Boat.ExtraCapacity).ToString()));

        table.Append(CreateEightColumnRow(
            "Hora estimada de Zarpe:", schedule.Schedule.DepartureTime.ToString(@"hh\:mm"),
            "Hora estimada de Arribo:", "",
            "", "",
            "", ""));

        return table;
    }

    private static Table CreateOwnerResponsibleAndCrewTable(
        BoatRouteSchedule schedule,
        BoatDailyTripCrew crew)
    {
        var table = CreateTable();

        table.Append(new TableRow(
            CreateCell("III. INFORMACIÓN DEL ARMADOR", true, 2500, GridSpan(2), Shading()),
            CreateCell("IV. INFORMACIÓN RESPONSABLE DE LOS PASAJEROS", true, 3500, GridSpan(2), Shading()),
            CreateCell("V. INFORMACIÓN DE LA TRIPULACIÓN", true, 4000, GridSpan(4), Shading())
        ));

        table.Append(new TableRow(
            CreateCell("Nombres:", true, 900),
            CreateCell(schedule.Boat.OwnerName ?? "", false, 1600),

            CreateCell("Nombres:", true, 900),
            CreateCell(crew.CaptainName ?? "", false, 2600),

            CreateCell("Capitán:", true, 800),
            CreateCell(crew.CaptainName ?? "", false, 1200),
            CreateCell("Cédula Nro.:", true, 900),
            CreateCell(crew.CaptainDocument ?? "", false, 1100)
        ));

        table.Append(new TableRow(
            CreateCell("Ruc:", true, 900),
            CreateCell(schedule.Boat.OwnerRuc ?? "", false, 1600),

            CreateCell("Cédula Nro.:", true, 900),
            CreateCell(crew.CaptainDocument ?? "", false, 2600),

            CreateCell("Marinero:", true, 800),
            CreateCell(crew.Sailor1Name ?? "", false, 1200),
            CreateCell("Cédula Nro.:", true, 900),
            CreateCell(crew.Sailor1Document ?? "", false, 1100)
        ));

        table.Append(new TableRow(
            CreateCell("e-mail:", true, 900),
            CreateCell(schedule.Boat.OwnerEmail ?? "", false, 1600),

            CreateCell("Telf.:", true, 900),
            CreateCell(schedule.Boat.OwnerPhone ?? "", false, 2600),

            CreateCell("Marinero:", true, 800),
            CreateCell(crew.Sailor2Name ?? "", false, 1200),
            CreateCell("Cédula Nro.:", true, 900),
            CreateCell(crew.Sailor2Document ?? "", false, 1100)
        ));

        table.Append(new TableRow(
            CreateCell("Telf.:", true, 900),
            CreateCell(schedule.Boat.OwnerPhone ?? "", false, 1600),

            CreateCell("", false, 900),
            CreateCell("", false, 2600),

            CreateCell("Guía (De existir):", true, 800),
            CreateCell(crew.Sailor3Name ?? "", false, 1200),
            CreateCell("Cédula Nro.:", true, 900),
            CreateCell(crew.Sailor3Document ?? "", false, 1100)
        ));

        return table;
    }

    private static TableRow CreateEightColumnRow(
        string label1,
        string value1,
        string label2,
        string value2,
        string label3,
        string value3,
        string label4,
        string value4)
    {
        return new TableRow(
            CreateCell(label1, true, 1200),
            CreateCell(value1, false, 1300),
            CreateCell(label2, true, 1300),
            CreateCell(value2, false, 1200),

            CreateCell(label3, true, 1200),
            CreateCell(value3, false, 1300),
            CreateCell(label4, true, 1300),
            CreateCell(value4, false, 1200)
        );
    }

    private static Table CreatePassengerTable(List<ReservationPassengerTrip> passengers)
    {
        var table = CreateTable();

        table.Append(new TableRow(
            CreateCell("Check", true, 500, VerticalMergeRestart(), Shading()),
            CreateCell("Nro.", true, 450, VerticalMergeRestart(), Shading()),
            CreateCell("Nombres y Apellidos", true, 1900, VerticalMergeRestart(), Shading()),
            CreateCell("Cédula/pasaporte", true, 1200, VerticalMergeRestart(), Shading()),
            CreateCell("Nacionalidad", true, 900, VerticalMergeRestart(), Shading()),
            CreateCell("Edad", true, 500, VerticalMergeRestart(), Shading()),
            CreateCell("Estatus", true, 1200, GridSpan(3), Shading()),
            CreateCell("Agencia de Turismo (De aplicar)", true, 1200, VerticalMergeRestart(), Shading()),
            CreateCell("Telf. Emergencia", true, 900, VerticalMergeRestart(), Shading()),
            CreateCell("Información adicional/observación", true, 1600, VerticalMergeRestart(), Shading())
        ));

        table.Append(new TableRow(
            CreateCell("", true, 500, VerticalMergeContinue(), Shading()),
            CreateCell("", true, 450, VerticalMergeContinue(), Shading()),
            CreateCell("", true, 1900, VerticalMergeContinue(), Shading()),
            CreateCell("", true, 1200, VerticalMergeContinue(), Shading()),
            CreateCell("", true, 900, VerticalMergeContinue(), Shading()),
            CreateCell("", true, 500, VerticalMergeContinue(), Shading()),
            CreateCell("Res.", true, 400, Shading()),
            CreateCell("Tra.", true, 400, Shading()),
            CreateCell("Tur.", true, 400, Shading()),
            CreateCell("", true, 1200, VerticalMergeContinue(), Shading()),
            CreateCell("", true, 900, VerticalMergeContinue(), Shading()),
            CreateCell("", true, 1600, VerticalMergeContinue(), Shading())
        ));

        var index = 1;
        var passengerSegments = passengers
            .GroupBy(x =>
                x.CustomerId?.ToString()
                ?? $"GEN-{x.GenericDocumentNumber}-{x.GenericPassengerName}")
            .ToDictionary(
                x => x.Key,
                x => x.Select(s => s.SegmentType)
                      .Distinct()
                      .ToList());

        foreach (var passenger in passengers.OrderBy(x => GetPassengerName(x)))
        {
            var status = GetPassengerTypeText(passenger.PassengerType);

            var passengerKey =
                passenger.CustomerId?.ToString()
                ?? $"GEN-{passenger.GenericDocumentNumber}-{passenger.GenericPassengerName}";

            var segments = passengerSegments[passengerKey];

            var observation = GetObservation(
                segments.Contains("Outbound"),
                segments.Contains("Return"));

            table.Append(new TableRow(
                CreateCell("SI", false, 500),
                CreateCell(index.ToString(), false, 450),
                CreateCell(GetPassengerName(passenger), false, 1900),
                CreateCell(GetPassengerDocument(passenger), false, 1200),
                CreateCell(passenger.Customer?.Nationality ?? "Ecuatoriana", false, 900),
                CreateCell(passenger.Customer?.Age?.ToString() ?? "", false, 500),
                CreateCell(status == "Residente" ? "X" : "", false, 400),
                CreateCell(status == "Transeúnte" ? "X" : "", false, 400),
                CreateCell(status == "Turista" ? "X" : "", false, 400),
                CreateCell(passenger.Reservation?.Agency?.Name ?? "", false, 1200),
                CreateCell("", false, 900),
                CreateCell(observation, false, 1600)
            ));

            index++;
        }

        return table;
    }

    private static Table CreateResponsibilityAndSignatureTable()
    {
        var table = CreateTable();

        var declaration =
            "Declaración de responsabilidad: El Capitán es la máxima autoridad y responsable directo de la navegación, maniobras y gobierno de la nave como de la seguridad de los pasajeros. Bajo citado contexto, en conjunto con el Armador, DECLARAN que la información detallada en el presente formulario es verdadera; quedando sujeto a las responsabilidades que correspondan por el ingreso de información y/o referencias no verdaderas, adulteradas, o falsificadas. Se enfatiza que la lista de pasajeros es un documento habilitante previo a la emisión del zarpe (Reglamento LONSEA; Arts. 184, 209, 210).";

        table.Append(new TableRow(
            CreateLongTextCell(declaration, 3600),
            CreateSignatureCell("Firma y nombres Capitán de la Nave", 2100),
            CreateSignatureCell("Firma y nombres Armador:", 2100),
            CreateSignatureCell("Firma y sello de la División de Arribos y Zarpes CAPAYO:", 2200)
        ));

        return table;
    }

    private static string GetObservation(
    bool hasOutbound,
    bool hasReturn)
    {
        if (hasOutbound && hasReturn)
            return "";

        if (hasOutbound)
            return "Solo ida";

        if (hasReturn)
            return "Solo vuelta";

        return "";
    }

    private static Paragraph CreateSectionTitle(string text)
    {
        return CreateParagraph(text, true, JustificationValues.Left, FontSizeNormal);
    }

    private static Paragraph CreateParagraph(
        string text,
        bool bold,
        JustificationValues justification,
        string fontSize)
    {
        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = justification },
                new SpacingBetweenLines { After = "80" }),
            CreateRun(text, bold, fontSize));
    }

    private static Paragraph CreateSpacer(int after)
    {
        return new Paragraph(
            new ParagraphProperties(
                new SpacingBetweenLines { After = after.ToString() }),
            new Run(new Text("")));
    }

    private static Run CreateRun(string text, bool bold, string fontSize)
    {
        var runProperties = new RunProperties(
            new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
            new FontSize { Val = fontSize });

        if (bold)
            runProperties.Append(new Bold());

        return new Run(runProperties, new Text(text ?? string.Empty)
        {
            Space = SpaceProcessingModeValues.Preserve
        });
    }

    private static Table CreateTable()
    {
        var table = new Table();

        table.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
            ),
            new TableCellMarginDefault(
                new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "40", Type = TableWidthUnitValues.Dxa }
            )));

        return table;
    }

    private static TableRow CreateFourColumnRow(
        string label1,
        string value1,
        string label2,
        string value2)
    {
        return new TableRow(
            CreateCell(label1, true, 1800),
            CreateCell(value1, false, 3200),
            CreateCell(label2, true, 1800),
            CreateCell(value2, false, 3200)
        );
    }

    private static TableRow CreateSixColumnRow(
        string label1,
        string value1,
        string label2,
        string value2,
        string label3,
        string value3)
    {
        return new TableRow(
            CreateCell(label1, true, 1100),
            CreateCell(value1, false, 2200),
            CreateCell(label2, true, 1100),
            CreateCell(value2, false, 2200),
            CreateCell(label3, true, 1100),
            CreateCell(value3, false, 2300)
        );
    }

    private static TableCell CreateCell(
        string? text,
        bool bold,
        int width,
        params OpenXmlElement[] properties)
    {
        var cellProperties = new TableCellProperties(
            new TableCellWidth
            {
                Width = width.ToString(),
                Type = TableWidthUnitValues.Dxa
            },
            new TableCellVerticalAlignment
            {
                Val = TableVerticalAlignmentValues.Center
            });

        foreach (var property in properties)
            cellProperties.Append(property.CloneNode(true));

        return new TableCell(
            cellProperties,
            new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { Before = "0", After = "0" }),
                CreateRun(text ?? "", bold, FontSizeSmall)));
    }

    private static GridSpan GridSpan(int value)
    {
        return new GridSpan { Val = value };
    }

    private static Shading Shading()
    {
        return new Shading
        {
            Val = ShadingPatternValues.Clear,
            Color = "auto",
            Fill = "D9EAF7"
        };
    }

    private static VerticalMerge VerticalMergeRestart()
    {
        return new VerticalMerge { Val = MergedCellValues.Restart };
    }

    private static VerticalMerge VerticalMergeContinue()
    {
        return new VerticalMerge { Val = MergedCellValues.Continue };
    }

    private static SectionProperties CreateSectionProperties()
    {
        return new SectionProperties(
            new PageSize
            {
                Width = 16840,
                Height = 11900,
                Orient = PageOrientationValues.Landscape
            },
            new PageMargin
            {
                Top = 720,
                Right = 360,
                Bottom = 720,
                Left = 360,
                Header = 360,
                Footer = 360,
                Gutter = 0
            });
    }

    private static string GetPassengerName(ReservationPassengerTrip trip)
    {
        return trip.Customer?.FullName
               ?? trip.GenericPassengerName
               ?? "Pasajero genérico";
    }

    private static string GetPassengerDocument(ReservationPassengerTrip trip)
    {
        return trip.Customer?.DocumentNumber
               ?? trip.GenericDocumentNumber
               ?? "";
    }

    private static string GetPassengerTypeText(string? type)
    {
        return type switch
        {
            "Resident" => "Residente",
            "Residente" => "Residente",
            "Transit" => "Transeúnte",
            "Transeunte" => "Transeúnte",
            "Transeúnte" => "Transeúnte",
            "Tourist" => "Turista",
            "Turista" => "Turista",
            "Adult" => "Turista",
            "Infant" => "Turista",
            "Courtesy" => "Turista",
            _ => "Turista"
        };
    }
    private static TableCell CreateLongTextCell(string text, int width)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Both },
                new SpacingBetweenLines { Before = "0", After = "0" }));

        paragraph.Append(CreateRun("Declaración de responsabilidad: ", true, FontSizeSmall));
        paragraph.Append(CreateRun(
            text.Replace("Declaración de responsabilidad:", "").Trim(),
            false,
            FontSizeSmall));

        return new TableCell(
            new TableCellProperties(
                new TableCellWidth
                {
                    Width = width.ToString(),
                    Type = TableWidthUnitValues.Dxa
                },
                new TableCellVerticalAlignment
                {
                    Val = TableVerticalAlignmentValues.Top
                }),
            paragraph);
    }

    private static TableCell CreateSignatureCell(string title, int width)
    {
        return new TableCell(
            new TableCellProperties(
                new TableCellWidth
                {
                    Width = width.ToString(),
                    Type = TableWidthUnitValues.Dxa
                },
                new TableCellVerticalAlignment
                {
                    Val = TableVerticalAlignmentValues.Top
                }),
            new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { Before = "0", After = "0" }),
                CreateRun(title, true, FontSizeSmall)),
            new Paragraph(new Run(new Text(""))),
            new Paragraph(new Run(new Text(""))),
            new Paragraph(new Run(new Text(""))),
            new Paragraph(new Run(new Text(""))));
    }

    private static Run[] CreateRunWithBoldPrefix(
        string prefix,
        string text,
        string fontSize)
    {
        return
        [
            CreateRun(prefix + " ", true, fontSize),
        CreateRun(text, false, fontSize)
        ];
    }
}