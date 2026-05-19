using CheckTrip.Web.Data.Entities;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CheckTrip.Web.Features.Tickets.Services;

public static class TripDocumentGenerator
{
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

            body.Append(CreateTitle("ARMADA DEL ECUADOR"));
            body.Append(CreateTitle("CAPITANÍA DE PUERTO DE PUERTO AYORA"));
            body.Append(CreateParagraph("LISTA DE PASAJEROS", true, JustificationValues.Center));

            body.Append(CreateSpacing());

            body.Append(CreateSectionTitle("I. INFORMACIÓN DEL VIAJE"));
            body.Append(CreateInfoTable([
                ("Fecha", trip.TripDate.ToString("yyyy-MM-dd")),
                ("Puerto de Zarpe", schedule.Route.Origin),
                ("Hora estimada de Zarpe", schedule.Schedule.DepartureTime.ToString(@"hh\:mm")),
                ("Puerto de Arribo", schedule.Route.Destination),
                ("Hora estimada de Arribo", ""),
                ("Actividad", schedule.Route.Type ?? "")
            ]));

            body.Append(CreateSectionTitle("II. INFORMACIÓN DE LA NAVE"));
            body.Append(CreateInfoTable([
                ("Nombres", schedule.Boat.Name),
                ("Matrícula", schedule.Boat.RegistrationNumber ?? ""),
                ("Capacidad Pasajeros", (schedule.Boat.Capacity + schedule.Boat.ExtraCapacity).ToString()),
                ("Capacidad Tripulantes", "4")
            ]));

            body.Append(CreateSectionTitle("III. INFORMACIÓN DEL ARMADOR"));
            body.Append(CreateInfoTable([
                ("Nombres", schedule.Boat.OwnerName ?? ""),
                ("Ruc", schedule.Boat.OwnerRuc ?? ""),
                ("e-mail", schedule.Boat.OwnerEmail ?? ""),
                ("Telf.", schedule.Boat.OwnerPhone ?? "")
            ]));

            body.Append(CreateSectionTitle("IV. RESPONSABLE DE LOS PASAJEROS"));
            body.Append(CreateInfoTable([
                ("Nombres", crew.CaptainName),
                ("Cédula Nro.", crew.CaptainDocument ?? "")
            ]));

            body.Append(CreateSectionTitle("V. INFORMACIÓN DE LA TRIPULACIÓN"));
            body.Append(CreateInfoTable([
                ("Capitán", crew.CaptainName),
                ("Cédula Nro.", crew.CaptainDocument ?? ""),
                ("Marinero 1", crew.Sailor1Name ?? ""),
                ("Cédula Marinero 1", crew.Sailor1Document ?? ""),
                ("Marinero 2", crew.Sailor2Name ?? ""),
                ("Cédula Marinero 2", crew.Sailor2Document ?? ""),
                ("Marinero 3", crew.Sailor3Name ?? ""),
                ("Cédula Marinero 3", crew.Sailor3Document ?? "")
            ]));

            body.Append(CreateSectionTitle("VI. LISTA DE PASAJEROS"));
            body.Append(CreatePassengerTable(passengers));

            body.Append(CreateSpacing());

            body.Append(CreateParagraph(
                "Declaración de responsabilidad: El Capitán es la máxima autoridad y responsable directo de la navegación, maniobras y gobierno de la nave como de la seguridad de los pasajeros. Declaran que la información detallada en el presente formulario es verdadera.",
                false,
                JustificationValues.Both));

            body.Append(CreateSpacing());
            body.Append(CreateSignatureTable());

            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph CreateTitle(string text)
    {
        return CreateParagraph(text, true, JustificationValues.Center, "24");
    }

    private static Paragraph CreateSectionTitle(string text)
    {
        return CreateParagraph(text, true, JustificationValues.Left, "22");
    }

    private static Paragraph CreateSpacing()
    {
        return new Paragraph(new Run(new Text(" ")));
    }

    private static Paragraph CreateParagraph(
        string text,
        bool bold,
        JustificationValues justification,
        string fontSize = "20")
    {
        var runProperties = new RunProperties
        {
            FontSize = new FontSize { Val = fontSize }
        };

        if (bold)
            runProperties.Append(new Bold());

        var paragraph = new Paragraph(
            new ParagraphProperties(
                new Justification { Val = justification }),
            new Run(runProperties, new Text(text ?? string.Empty)));

        return paragraph;
    }

    private static Table CreateInfoTable(IEnumerable<(string Label, string Value)> rows)
    {
        var table = CreateBaseTable();

        foreach (var row in rows)
        {
            table.Append(new TableRow(
                CreateCell(row.Label, true),
                CreateCell(row.Value, false)));
        }

        return table;
    }

    private static Table CreatePassengerTable(List<ReservationPassengerTrip> passengers)
    {
        var table = CreateBaseTable();

        table.Append(new TableRow(
            CreateCell("Nro.", true),
            CreateCell("Nombres y Apellidos", true),
            CreateCell("Cédula/Pasaporte", true),
            CreateCell("Nacionalidad", true),
            CreateCell("Edad", true),
            CreateCell("Estatus", true),
            CreateCell("Agencia", true),
            CreateCell("Observación", true)
        ));

        var index = 1;

        foreach (var passenger in passengers.OrderBy(x => GetPassengerName(x)))
        {
            table.Append(new TableRow(
                CreateCell(index.ToString(), false),
                CreateCell(GetPassengerName(passenger), false),
                CreateCell(GetPassengerDocument(passenger), false),
                CreateCell(passenger.Customer?.Nationality ?? "Ecuatoriana", false),
                CreateCell(passenger.Customer?.Age?.ToString() ?? "", false),
                CreateCell(GetPassengerTypeText(passenger.PassengerType), false),
                CreateCell(passenger.Reservation?.Agency?.Name ?? "", false),
                CreateCell(passenger.SegmentType == "Outbound" ? "Ida" : "Retorno", false)
            ));

            index++;
        }

        return table;
    }

    private static Table CreateSignatureTable()
    {
        var table = CreateBaseTable();

        table.Append(new TableRow(
            CreateCell("Firma y nombres Capitán de la Nave", true),
            CreateCell("Firma y nombres Armador", true),
            CreateCell("Firma y sello CAPAYO", true)
        ));

        table.Append(new TableRow(
            CreateCell("\n\n\n", false),
            CreateCell("\n\n\n", false),
            CreateCell("\n\n\n", false)
        ));

        return table;
    }

    private static Table CreateBaseTable()
    {
        var table = new Table();

        var properties = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
            ),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });

        table.AppendChild(properties);

        return table;
    }

    private static TableCell CreateCell(string? text, bool bold)
    {
        var runProperties = new RunProperties
        {
            FontSize = new FontSize { Val = "18" }
        };

        if (bold)
            runProperties.Append(new Bold());

        return new TableCell(
            new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Auto }),
            new Paragraph(
                new Run(
                    runProperties,
                    new Text(text ?? string.Empty))));
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
            "Adult" => "Adulto",
            "Infant" => "Infante",
            "Courtesy" => "Cortesía",
            _ => type ?? ""
        };
    }
}