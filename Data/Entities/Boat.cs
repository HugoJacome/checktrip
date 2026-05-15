using CheckTrip.Web.Data.Entities;

public class Boat : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? RegistrationNumber { get; set; }

    public int Capacity { get; set; } = 30;
    public int ExtraCapacity { get; set; }

    public string? OwnerName { get; set; }
    public string? OwnerRuc { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerPhone { get; set; }

    public bool IsActive { get; set; } = true;

    public List<ReservationTripCrew> TripCrews { get; set; } = [];
    public List<CrewMember> CrewMembers { get; set; } = [];
}