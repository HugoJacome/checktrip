public class AgencyListItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Ruc { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Type { get; set; }

    public bool IsActive { get; set; }
}