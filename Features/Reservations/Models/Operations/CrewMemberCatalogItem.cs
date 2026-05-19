namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class CrewMemberCatalogItem
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public string? DocumentNumber { get; set; }

    public bool CanBeCaptain { get; set; }
    public bool CanBeSailor { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(DocumentNumber)
        ? FullName
        : $"{FullName} - {DocumentNumber}";
}