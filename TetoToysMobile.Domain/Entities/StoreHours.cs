namespace TetoToysMobile.Domain.Entities;

/// <summary>DayOfWeek: 0 = Sunday .. 6 = Saturday.</summary>
public class StoreHours
{
    public int DayOfWeek { get; set; }
    public TimeSpan OpenTime { get; set; }
    public TimeSpan CloseTime { get; set; }
    public bool IsClosed { get; set; }
}
