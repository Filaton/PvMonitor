# PvMonitor — Copilot Instructions

A .NET 10 application that monitors a Fenecon Home 15 PV system via Modbus TCP, persists telemetry to SQLite, and exposes a Minimal API. Personal portfolio project for learning .NET in depth.

## Stack

- **Runtime:** .NET 10 (LTS, supported until November 2028)
- **Language:** C# 14, nullable reference types enabled, file-scoped namespaces
- **Web framework:** ASP.NET Core Minimal APIs (no MVC controllers)
- **ORM:** Entity Framework Core 10 with SQLite provider
- **Modbus library:** FluentModbus
- **Logging:** `Microsoft.Extensions.Logging` via `ILogger<T>`
- **Configuration:** `IOptions<T>` / `IOptionsMonitor<T>` pattern
- **Testing:** xUnit
- **Container:** Devcontainer based on `mcr.microsoft.com/devcontainers/dotnet:10.0`

## Project Layout

The solution follows a layered domain-first architecture. Dependencies flow inward toward `PvMonitor.Core`.

```
PvMonitor.sln
├── src/
│   ├── PvMonitor.Core/      # Domain types, interfaces. No external dependencies.
│   ├── PvMonitor.Modbus/    # FluentModbus implementation of IModbusReader.
│   ├── PvMonitor.Storage/   # EF Core implementation of ITelemetryRepository.
│   └── PvMonitor.Api/       # Composition root. Minimal API + BackgroundService.
└── tests/
    └── PvMonitor.Tests/     # xUnit tests.
```

**Dependency rules:**

- `Core` depends on nothing
- `Modbus` and `Storage` depend only on `Core`
- `Api` depends on all three (it's the composition root)
- Never reference `Modbus` or `Storage` from `Core`
- Never reference `Api` from anywhere

## Domain Types (Core)

The canonical domain record:

```csharp
public record TelemetryReading(
    DateTimeOffset Timestamp,
    int BatterySoc,                  // %, 0-100
    float BatteryPowerWatts,         // negative = charging, positive = discharging
    float GridPowerWatts,            // negative = export, positive = import
    float ProductionPowerWatts,      // always >= 0
    float ConsumptionPowerWatts,
    GridMode GridMode
);

public enum GridMode { Unknown = 0, OnGrid = 1, OffGrid = 2, OffGridGenset = 3 }
```

**Sign conventions are part of the contract.** Do not change them silently. Any new code that deals with battery/grid power must respect these signs.

## Hardware Specifics

The target device is a **Fenecon Home 15** running OpenEMS, exposing Modbus TCP on port 502. The relevant registers all live in the OpenEMS `_sum` block:

| Address | Channel | Type | Notes |
|---------|---------|------|-------|
| 302 | `_sum/EssSoc` | uint16 | Battery state of charge, 0-100 |
| 303 | `_sum/EssActivePower` | float32 | Battery power, signed |
| 315 | `_sum/GridActivePower` | float32 | Grid power, signed |
| 327 | `_sum/ProductionActivePower` | float32 | PV production, always >= 0 |
| 343 | `_sum/ConsumptionActivePower` | float32 | House consumption |
| 417 | `_sum/GridMode` | enum16 | 1=On-Grid, 2=Off-Grid, 3=Off-Grid Genset |

**Register-map data is configuration, not code.** It lives in `src/PvMonitor.Modbus/registers.json` and is loaded at runtime. New devices = new JSON, not new code.

**Undefined value sentinels** (per OpenEMS spec):

- `uint16` / `enum16`: `0xFFFF`
- `uint32`: `0xFFFFFFFF`
- `float32`: `0x7FC000`
- `float64`: `0x7FF8000000`

Any value matching the sentinel must be treated as missing, not as zero or as the literal numeric value.

**Modbus endianness:** big-endian word order. FluentModbus's typed read methods handle this when configured with `ModbusEndianness.BigEndian` at connect time.

## .NET Conventions

### Async

- Every IO method is `async` and returns `Task` or `Task<T>`
- Always include a `CancellationToken` parameter on async methods, with a default value of `default`
- Never call `.Result` or `.Wait()` — always `await`
- Method names end with `Async`

### Dependency Injection

- Constructor injection only. No service locator, no static state.
- Register services in `Program.cs` via the appropriate extension method:
  - `AddSingleton<T>` for stateless or expensive services
  - `AddScoped<T>` for per-request services (DbContext, repositories)
  - `AddTransient<T>` for cheap, stateless objects
- Never inject a scoped service into a singleton

### Configuration

- Strongly-typed config classes live in `Core` (e.g., `FeneconOptions`)
- Each config class defines a `public const string SectionName`
- Bind in `Program.cs` via `builder.Services.Configure<T>(builder.Configuration.GetSection(T.SectionName))`
- Inject as `IOptions<T>` for static config, `IOptionsMonitor<T>` for hot-reloadable config in long-running services
- Never read `IConfiguration` directly outside of `Program.cs`

### Logging

- Use `ILogger<T>` injected via constructor. Never `Console.WriteLine` outside of throwaway tests.
- Use **structured logging** with named placeholders:
  ```csharp
  _logger.LogInformation("Connecting to FEMS at {Host}:{Port}", host, port);
  ```
  not
  ```csharp
  _logger.LogInformation($"Connecting to FEMS at {host}:{port}");  // bad
  ```
- Use appropriate levels: `Trace` < `Debug` < `Information` < `Warning` < `Error` < `Critical`. Reserve `Error` for actual failures, not for expected branches.

### Naming

- `_camelCase` for private fields (configured in `.editorconfig`)
- `PascalCase` for everything else (types, methods, properties, public fields, constants)
- Interfaces start with `I` (`IModbusReader`, `ITelemetryRepository`)
- Async method names end with `Async`

### Types

- Prefer `record` over `class` for data-shaped types (DTOs, domain models, configuration where appropriate)
- Use `DateTimeOffset` for all timestamps. Never use `DateTime`.
- Prefer `IReadOnlyList<T>` / `IReadOnlyCollection<T>` over `List<T>` for return types
- Enable nullable reference types in every project; treat warnings as errors

### EF Core

- DbContext lives in `PvMonitor.Storage`
- Use migrations for schema changes (`dotnet ef migrations add ...`)
- Never expose `DbContext` outside of `Storage`. Always go through repository interfaces defined in `Core`.
- Use `AsNoTracking()` for read-only queries
- Async methods only (`ToListAsync`, `FirstOrDefaultAsync`, etc.)

### Background Services

- Long-running work belongs in a `BackgroundService` registered via `AddHostedService<T>`
- Honor the `CancellationToken` passed to `ExecuteAsync` — break out cleanly on shutdown
- Use `IOptionsMonitor<T>` for configuration that should be hot-reloadable

## Layering Rules (Strict)

`Core` must remain free of:

- ASP.NET Core types
- EF Core types
- FluentModbus types
- Microsoft.Extensions.Hosting types

`Core` may use:

- `Microsoft.Extensions.Options` (just for `IOptions<T>` typing — preferred location for config classes)
- `Microsoft.Extensions.Logging.Abstractions` if a domain service needs logging

If you find yourself wanting to put a framework type in `Core`, the design is wrong. Define an interface in `Core` and implement it in the appropriate infrastructure project.

## Testing Conventions

- One test class per production class, named `{ClassName}Tests`
- Test method names use the `MethodName_Scenario_ExpectedResult` pattern, e.g. `ReadAsync_WhenDeviceUnreachable_ThrowsConnectionException`
- Use xUnit's `[Fact]` for single-case tests and `[Theory]` + `[InlineData]` for parameterized tests
- No `Moq` for now — hand-rolled fakes are fine for a project this size
- Tests live in `tests/PvMonitor.Tests/` mirroring the source structure

## Configuration Files

- `appsettings.json` is committed and contains placeholder values (e.g., `"Host": "REPLACE_ME"`)
- `appsettings.Development.json` is gitignored and contains the real local values
- Never commit real device IPs, serial numbers, or local network topology

## Code Style

- Enforced via `.editorconfig` at the repo root
- 4-space indentation for `.cs`, 2-space for `.json`/`.yaml`
- LF line endings
- Trailing whitespace trimmed
- Final newline required
- File-scoped namespaces (`namespace Foo;`) over block-scoped

## What NOT to Do

- Don't use OmniSharp; use the Roslyn-based language server from C# Dev Kit
- Don't use `.NET 9`; the project targets `net10.0` exclusively
- Don't add WinForms/WPF/MAUI references; this is a headless web service
- Don't add `Newtonsoft.Json`; use `System.Text.Json`
- Don't bypass the repository abstraction by injecting `DbContext` into API endpoints
- Don't put register addresses into C# constants; they belong in `registers.json`
- Don't use `localhost` or hardcoded IPs anywhere; everything goes through `FeneconOptions`

## Useful Commands

```bash
# Build everything
dotnet build

# Run the API
dotnet run --project src/PvMonitor.Api

# Add an EF Core migration
dotnet ef migrations add <Name> --project src/PvMonitor.Storage --startup-project src/PvMonitor.Api

# Apply migrations
dotnet ef database update --project src/PvMonitor.Storage --startup-project src/PvMonitor.Api

# Run tests
dotnet test

# Format the entire solution
dotnet format
```