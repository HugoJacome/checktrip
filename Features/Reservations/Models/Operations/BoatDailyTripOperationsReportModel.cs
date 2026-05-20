namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class BoatDailyTripOperationsReportModel
{
    public int TotalPassengers { get; set; }
    public int OutboundPassengers { get; set; }
    public int ReturnPassengers { get; set; }

    public decimal TotalAmount { get; set; }

    public List<BoatDailyTripReportByTripItem> ByTrip { get; set; } = [];
    public List<BoatDailyTripReportByAgencyItem> ByAgency { get; set; } = [];
    public List<BoatDailyTripReportBySellerItem> BySeller { get; set; } = [];
}

public class BoatDailyTripReportByTripItem
{
    public DateOnly TravelDate { get; set; }
    public string Boat { get; set; } = default!;
    public string Route { get; set; } = default!;
    public string Schedule { get; set; } = default!;

    public int OutboundPassengers { get; set; }
    public int ReturnPassengers { get; set; }
    public int TotalPassengers { get; set; }

    public decimal TotalAmount { get; set; }
}

public class BoatDailyTripReportByAgencyItem
{
    public string Agency { get; set; } = default!;

    public int OutboundPassengers { get; set; }
    public int ReturnPassengers { get; set; }
    public int TotalPassengers { get; set; }

    public decimal TotalAmount { get; set; }
}

public class BoatDailyTripReportBySellerItem
{
    public string Seller { get; set; } = default!;
    public string Route { get; set; } = default!;

    public int OutboundPassengers { get; set; }
    public int ReturnPassengers { get; set; }
    public int TotalPassengers { get; set; }

    public decimal TotalAmount { get; set; }
}