namespace CheckAccess.Features.Catalogs.Boat.Models
{
    public class CrewMemberEditModel
    {
        public Guid? Id { get; set; }

        public string FullName { get; set; } = string.Empty;
        public string? DocumentNumber { get; set; }
        public string? Phone { get; set; }

        public bool CanBeCaptain { get; set; }
        public bool CanBeSailor { get; set; } = true;

        public bool IsActive { get; set; } = true;
    }
}
