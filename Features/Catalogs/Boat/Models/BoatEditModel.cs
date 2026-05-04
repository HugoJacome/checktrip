public class BoatEditModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = default!;
    public int Capacity { get; set; }
    public int ExtraCapacity { get; set; }

    public Guid? AgencyId { get; set; }
}