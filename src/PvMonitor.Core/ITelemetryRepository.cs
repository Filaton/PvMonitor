namespace PvMonitor.Core;

public interface ITelemetryRepository
{
    Task AddAsync(TelemetryReading reading, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetryReading>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}