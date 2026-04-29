using System.Text.Json.Serialization;

namespace PvMonitor.Modbus;

public class RegisterMap
{
    public Dictionary<string, RegisterDefinition> Registers { get; set; } = new();
}

public class RegisterDefinition
{
    public ushort Address { get; set; }
    public string Type { get; set; } = "";
}