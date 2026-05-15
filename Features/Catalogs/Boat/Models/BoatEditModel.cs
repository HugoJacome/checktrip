using CheckAccess.Features.Catalogs.Boat.Models;

public class BoatEditModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }

    public int Capacity { get; set; } = 30;
    public int ExtraCapacity { get; set; }

    public string? OwnerName { get; set; }
    public string? OwnerRuc { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerPhone { get; set; }

    public bool IsActive { get; set; } = true;

    public List<CrewMemberEditModel> CrewMembers { get; set; } = [];
}