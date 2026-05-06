using CheckTrip.Web.Data.Entities;

public class Customer : BaseEntity
{
    public string DocumentType { get; set; } = default!;
    public string DocumentNumber { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public int? Age { get; set; }
    public string? Nationality { get; set; }
    public DateTime? BirthDate { get; set; }
    public bool IsActive { get; set; } = true;
}