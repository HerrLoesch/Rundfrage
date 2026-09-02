using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Diagnostics;
using Rundfrage.Api.Endpoints;
using Rundfrage.Api.Observability;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (FR-024, FR-025) --------------------------------------------------------------
// Console.Out is a TextWriter, so the logger built here is the same one the unit tests build
// against a buffer. See LoggingSetup and research.md R-5.
var logger = LoggingSetup.CreateLogger(
    Environment.GetEnvironmentVariable(LoggingSetup.LogLevelVariable),
    Console.Out);
Log.Logger = logger;
builder.Logging.ClearProviders();
builder.Host.UseSerilog(logger, dispose: true);

// --- Data access (FR-008, research.md R-3) -------------------------------------------------
// Timeout=2 in the connection string bounds connection establishment; the probe adds a
// CancellationToken for the whole operation. No global retry strategy - see RundfrageDbContext.
var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? "Host=localhost;Port=5432;Database=rundfrage;Username=rundfrage;Password=rundfrage_dev;Timeout=2";
builder.Services.AddDbContext<RundfrageDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<DatabaseProbe>();

var app = builder.Build();

// --- Schema (FR-013) -----------------------------------------------------------------------
// Applied with bounded retries. On final failure the host still starts and the status endpoint
// reports the database as unreachable, because FR-011 requires the page to stay usable.
await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseStartup.ApplyMigrationsAsync(
        scope.ServiceProvider.GetRequiredService<RundfrageDbContext>(),
        app.Services.GetRequiredService<ILogger<Program>>(),
        CancellationToken.None);
}

// --- Routing (FR-006a) ---------------------------------------------------------------------
// Everything under /api/v1 is the API; everything else belongs to the web application, so the
// SPA's client-side routes and the backend endpoints cannot collide on the shared origin.
var api = app.MapGroup("/api/v1");
api.MapMessageEndpoint();
api.MapStatusEndpoint();

app.UseDefaultFiles();
app.UseStaticFiles();

// Unmatched API paths must 404 rather than fall through to the SPA shell. This catch-all has
// lower route precedence than the specific endpoints above, so it only sees genuine misses.
app.Map("/api/{**rest}", () => Results.NotFound());

// Every other unmatched path serves the SPA shell so client-side routing works on reload.
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Exposed so the integration tests can drive the host with WebApplicationFactory.</summary>
public partial class Program;
