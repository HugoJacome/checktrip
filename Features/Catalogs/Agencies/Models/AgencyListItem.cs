public class AgencyListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public bool IsActive { get; set; }
}