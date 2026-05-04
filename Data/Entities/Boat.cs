using CheckTrip.Web.Data.Entities;

public class Boat : BaseEntity
{
    public string Name { get; set; } = default!;
    public int Capacity { get; set; }
    public int ExtraCapacity { get; set; }

    public Guid? AgencyId { get; set; }
    public Agency? Agency { get; set; }

    public bool IsActive { get; set; } = true;
}