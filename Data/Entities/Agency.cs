using CheckTrip.Web.Data.Entities;

public class Agency : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Ruc { get; set; }
    public bool IsActive { get; set; } = true;
}