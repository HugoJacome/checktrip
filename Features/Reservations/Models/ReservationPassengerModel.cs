namespace CheckTrip.Web.Features.Reservations.Models;

public class ReservationPassengerModel
{
    public string DocumentType { get; set; } = "Cedula";
    public string DocumentNumber { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public int? Age { get; set; }

    public bool Outbound { get; set; } = true;
    public bool Return { get; set; } = true;

    public string PassengerType { get; set; } = "Normal";
}