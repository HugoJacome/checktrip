namespace CheckTrip.Web.Features.Reservations.Models;

public class ReservationPassengerModel
{
    public string DocumentType { get; set; } = "Cedula";
    public string DocumentNumber { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Nationality { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? Age { get; set; }

    public string PassengerType { get; set; } = "Adult";
    // Adult, Infant, Courtesy
    public bool Outbound { get; set; } = true;
    public bool Return { get; set; } = true;
    public DateTime? ReturnDate { get; set; }
    public Guid? CustomerId { get; set; }
    public bool IsNewCustomer { get; set; }
    public bool CustomerChanged { get; set; }
    public Guid? ReservationItemId { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsGenericPassenger { get; set; }
    public int GenericQuantity { get; set; } = 1;

}