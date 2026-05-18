using CheckTrip.Web.Data.Entities;

public class Reservation : BaseEntity
{
    public string ReservationCode { get; set; } = default!;
    public string? ExternalReference { get; set; }

    public Guid? AgencyId { get; set; }
    public Agency? Agency { get; set; }

    public Guid? SellerId { get; set; }
    public Seller? Seller { get; set; }

    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }

    public string Status { get; set; } = "Active";
    public string PaymentStatus { get; set; } = "Pending";

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public string? Notes { get; set; }

    public List<ReservationPassengerTrip> PassengerTrips { get; set; } = [];
    public List<ReservationComment> Comments { get; set; } = [];
}