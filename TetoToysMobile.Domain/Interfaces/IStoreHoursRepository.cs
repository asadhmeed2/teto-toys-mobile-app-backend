using TetoToysMobile.Domain.Entities;

namespace TetoToysMobile.Domain.Interfaces;

public interface IStoreHoursRepository
{
    /// <summary>All seven weekdays ordered 0 (Sunday) .. 6 (Saturday).</summary>
    Task<List<StoreHours>> GetAllAsync();
}
