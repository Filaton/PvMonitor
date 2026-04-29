using PvMonitor.Core;
using PvMonitor.Modbus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<FeneconOptions>(builder.Configuration.GetSection(FeneconOptions.SectionName));
builder.Services.AddSingleton<IModbusReader, FeneconReader>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/telemetry/now", async (IModbusReader reader, CancellationToken ct) =>
{
    var reading = await reader.ReadAsync(ct);
    return Results.Ok(reading);
})
.WithName("GetCurrentTelemetry");

app.Run();
