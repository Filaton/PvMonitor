# PvMonitor

A headless .NET 10 web service that monitors a **Fenecon Home** photovoltaic system via Modbus TCP, persists telemetry to SQLite, and exposes a Minimal API for querying live and historical data.

Personal portfolio project for exploring .NET in depth — Minimal APIs, EF Core, BackgroundService, layered architecture, and FluentModbus.

## Features

- Reads live telemetry (battery SoC, battery/grid/production/consumption power, grid mode) from the Fenecon FEMS device over Modbus TCP
- Register map is data-driven (`registers.json`) — supporting a new device means a new JSON file, not new code
- Sentinel-value aware: OpenEMS undefined values (`0xFFFF`, `0x7FC000`, …) are treated as missing, not zero
- Stores readings to SQLite via EF Core
- Exposes a Minimal API with OpenAPI support

## Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 10 |
| Language | C# 14, nullable reference types |
| Web framework | ASP.NET Core Minimal APIs |
| ORM | Entity Framework Core 10 + SQLite |
| Modbus | FluentModbus |
| Logging | `Microsoft.Extensions.Logging` |
| Testing | xUnit |

## Architecture

The solution follows a layered, domain-first architecture. Dependencies flow inward toward `PvMonitor.Core`.

```
PvMonitor.sln
├── src/
│   ├── PvMonitor.Core/      # Domain types and interfaces. No external dependencies.
│   ├── PvMonitor.Modbus/    # FluentModbus implementation of IModbusReader.
│   ├── PvMonitor.Storage/   # EF Core implementation of ITelemetryRepository.
│   └── PvMonitor.Api/       # Composition root. Minimal API + BackgroundService.
└── tests/
    └── PvMonitor.Tests/     # xUnit tests.
```

`Core` → nothing · `Modbus` → `Core` · `Storage` → `Core` · `Api` → all three

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A Fenecon Home (or compatible OpenEMS device) reachable over the network
- (Optional) Dev Container — a `.devcontainer` config is included for VS Code

### Configuration

Copy the placeholder settings and fill in your device details:

```bash
cp src/PvMonitor.Api/appsettings.json src/PvMonitor.Api/appsettings.Development.json
```

Edit `appsettings.Development.json`:

```json
{
  "Fenecon": {
    "Host": "192.168.1.x",
    "Port": 502,
    "UnitId": 1,
    "PollIntervalSeconds": 10
  }
}
```

### Run

```bash
dotnet run --project src/PvMonitor.Api
```

The port is determined by `src/PvMonitor.Api/Properties/launchSettings.json`. The current defaults are `http://localhost:5137` and `https://localhost:7252`. The actual URL is printed to the console on startup.

## API

| Method | Path | Description |
|---|---|---|
| `GET` | `/telemetry/now` | Read live telemetry from the device right now |

OpenAPI / Swagger UI is available at `/openapi/v1.json` in the Development environment.

### Example response

```json
{
  "timestamp": "2026-04-29T14:32:10+00:00",
  "batterySoc": 72,
  "batteryPowerWatts": -1200.0,
  "gridPowerWatts": 0.0,
  "productionPowerWatts": 3400.0,
  "consumptionPowerWatts": 2200.0,
  "gridMode": 1
}
```

**Sign conventions:**
- `batteryPowerWatts`: negative = charging, positive = discharging
- `gridPowerWatts`: negative = export, positive = import
- `productionPowerWatts`: always ≥ 0
- `gridMode`: `0` Unknown · `1` OnGrid · `2` OffGrid · `3` OffGridGenset

## Register Map

Modbus register definitions live in `src/PvMonitor.Modbus/registers.json` and are loaded at startup:

| Register | Channel | Type |
|---|---|---|
| 302 | `_sum/EssSoc` | uint16 |
| 303 | `_sum/EssActivePower` | float32 |
| 315 | `_sum/GridActivePower` | float32 |
| 327 | `_sum/ProductionActivePower` | float32 |
| 343 | `_sum/ConsumptionActivePower` | float32 |
| 417 | `_sum/GridMode` | enum16 |

## Development

```bash
# Build everything
dotnet build

# Run tests
dotnet test

# Format the solution
dotnet format

# Add a database migration
dotnet ef migrations add <Name> \
  --project src/PvMonitor.Storage \
  --startup-project src/PvMonitor.Api

# Apply migrations
dotnet ef database update \
  --project src/PvMonitor.Storage \
  --startup-project src/PvMonitor.Api
```
