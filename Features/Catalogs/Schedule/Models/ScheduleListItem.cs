public class ScheduleListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public TimeSpan DepartureTime { get; set; }

    public string DisplayTime => DepartureTime.ToString(@"hh\:mm");
}