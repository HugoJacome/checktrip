namespace CheckTrip.Web.Features.Security.Models;

public class PermissionEditModel
{
    public Guid ResourceId { get; set; }
    public string ResourceCode { get; set; } = default!;
    public string ResourceName { get; set; } = default!;

    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManage { get; set; }
}