namespace PvMonitor.Core;

public interface IModbusReader
{
    Task<TelemetryReading> ReadAsync(CancellationToken cancellationToken = default);
}