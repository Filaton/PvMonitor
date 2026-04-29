namespace PvMonitor.Core;

public record TelemetryReading(
    DateTimeOffset Timestamp,
    int BatterySoc,                  // %, 0-100
    float BatteryPowerWatts,         // negative = charging, positive = discharging
    float GridPowerWatts,            // negative = export, positive = import
    float ProductionPowerWatts,      // always >= 0
    float ConsumptionPowerWatts,
    GridMode GridMode
);

public enum GridMode
{
    Unknown = 0,
    OnGrid = 1,
    OffGrid = 2,
    OffGridGenset = 3
}