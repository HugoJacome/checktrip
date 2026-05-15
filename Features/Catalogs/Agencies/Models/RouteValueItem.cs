namespace CheckAccess.Features.Catalogs.Agencies.Models
{
    public class RouteValueItem
    {
        public Guid RouteId { get; set; }
        public string RouteName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public decimal Value { get; set; }
    }
}
