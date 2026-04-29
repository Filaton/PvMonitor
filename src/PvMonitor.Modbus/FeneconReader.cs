using System.IO;
using System.Net;
using System.Text.Json;
using System.Runtime.InteropServices;
using FluentModbus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PvMonitor.Core;

namespace PvMonitor.Modbus;

public class FeneconReader : IModbusReader, IDisposable
{
    private const uint Float32Undefined = 0x7FC000;
    private const ushort Uint16Undefined = 0xFFFF;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly FeneconOptions _options;
    private readonly ILogger<FeneconReader> _logger;
    private readonly RegisterMap _map;
    private readonly ModbusTcpClient _client = new();

    public FeneconReader(
        IOptions<FeneconOptions> options,
        ILogger<FeneconReader> logger)
    {
        _options = options.Value;
        _logger = logger;

        var jsonPath = Path.Combine(AppContext.BaseDirectory, "registers.json");
        var json = File.ReadAllText(jsonPath);
        _map = JsonSerializer.Deserialize<RegisterMap>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to load register map");
    }

    public async Task<TelemetryReading> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
            _logger.LogInformation("Connecting to FEMS at {Host}:{Port}", _options.Host, _options.Port);
            var addresses = await Dns.GetHostAddressesAsync(_options.Host, cancellationToken);
            _client.Connect(
                new IPEndPoint(addresses[0], _options.Port),
                ModbusEndianness.BigEndian);
        }

        var soc = ReadUint16("BatterySoc");
        var battery = ReadFloat32("BatteryActivePower");
        var grid = ReadFloat32("GridActivePower");
        var production = ReadFloat32("ProductionActivePower");
        var consumption = ReadFloat32("ConsumptionActivePower");
        var gridMode = ReadEnum16("GridMode");

        return await Task.FromResult(new TelemetryReading(
            Timestamp: DateTimeOffset.UtcNow,
            BatterySoc: soc ?? 0,
            BatteryPowerWatts: battery ?? 0f,
            GridPowerWatts: grid ?? 0f,
            ProductionPowerWatts: production ?? 0f,
            ConsumptionPowerWatts: consumption ?? 0f,
            GridMode: (GridMode)(gridMode ?? 0)
        ));
    }

    private float? ReadFloat32(string name)
    {
        var def = _map.Registers[name];
        var values = _client.ReadHoldingRegisters<float>(_options.UnitId, def.Address, 1);
        var raw = MemoryMarshal.Cast<byte, uint>(MemoryMarshal.AsBytes(values))[0];
        return raw == Float32Undefined ? null : values[0];
    }

    private ushort? ReadUint16(string name)
    {
        var def = _map.Registers[name];
        var values = _client.ReadHoldingRegisters<ushort>(_options.UnitId, def.Address, 1);
        return values[0] == Uint16Undefined ? null : values[0];
    }

    private ushort? ReadEnum16(string name) => ReadUint16(name);

    public void Dispose()
    {
        if (_client.IsConnected)
            _client.Disconnect();
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}