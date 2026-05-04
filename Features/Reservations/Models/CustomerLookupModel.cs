namespace CheckTrip.Web.Features.Reservations.Models;

public class CustomerLookupModel
{
    public string DocumentType { get; set; } = default!;
    public string DocumentNumber { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public int? Age { get; set; }
}