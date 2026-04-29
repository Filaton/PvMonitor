namespace PvMonitor.Core;

public class FeneconOptions
{
    public const string SectionName = "Fenecon";

    public string Host { get; set; } = "";
    public int Port { get; set; } = 502;
    public byte UnitId { get; set; } = 1;
    public int PollIntervalSeconds { get; set; } = 10;
}