namespace CheckTrip.Web.Features.Reservations.Models;

public class CustomerUpsertModel
{
    public string DocumentType { get; set; } = "Cedula";
    public string DocumentNumber { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Nationality { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? Age { get; set; }
}