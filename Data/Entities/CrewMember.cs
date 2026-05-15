namespace CheckTrip.Web.Data.Entities;

public class CrewMember : BaseEntity
{
    public Guid BoatId { get; set; }
    public Boat Boat { get; set; } = default!;

    public string FullName { get; set; } = default!;
    public string? DocumentNumber { get; set; }
    public string? Phone { get; set; }

    public bool CanBeCaptain { get; set; }
    public bool CanBeSailor { get; set; } = true;

    public bool IsActive { get; set; } = true;
}