using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Endpoints.Admin;
using Rundfrage.Api.Polls;
using Rundfrage.Api.Security;
using Rundfrage.Api.Time;
using Rundfrage.Api.Diagnostics;
using Rundfrage.Api.Endpoints;
using Rundfrage.Api.Observability;
using Serilog;

// --- One-off: produce a password hash for the operator (FR-045a) ---------------------------
// Kept here rather than in a README so the operator cannot get the parameters wrong.
if (args.Contains("--hash-password"))
{
    // The prompt goes to stderr so that stdout carries nothing but the hash. Otherwise
    //   ... --hash-password > .env
    // would write "Password: pbkdf2-..." and the resulting configuration would never verify.
    Console.Error.Write("Password: ");
    var entered = Console.ReadLine() ?? string.Empty;
    Console.WriteLine(PasswordHash.Generate(entered));
    return;
}

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

// --- Time (FR-011a) ------------------------------------------------------------------------
// One authority for every day boundary. Requires zone data in the runtime image - the Alpine
// image ships none, which is why docker/Dockerfile installs tzdata (research.md R-6).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BerlinClock>();

// --- The single operator account (FR-045) --------------------------------------------------
// Resolved eagerly: a missing credential must stop the application here, not at the first
// sign-in attempt, so a misconfigured deployment fails loudly instead of quietly (SC-015).
builder.Services.AddSingleton(AdminAccount.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<SignInThrottle>();

builder.Services.AddScoped<PollService>();

// --- Authentication (FR-001, FR-006, research.md R-1) --------------------------------------
// HttpOnly so a script cannot read it; SameSite=Strict so the browser never attaches it to a
// cross-site request, which removes forged-form CSRF without a token mechanism (research.md R-10).
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "rundfrage.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // An API, not a website: answer 401 rather than redirecting to a login page, and
        // disclose nothing about what exists (FR-002).
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new { code = "unauthorized" });
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new { code = "unauthorized" });
        };
    });
builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api/v1");
api.MapMessageEndpoint();
api.MapStatusEndpoint();

// --- Admin (FR-001, FR-048) ----------------------------------------------------------------
// The requirement is applied to the whole group, not to individual handlers. FR-048 asserts
// that *every* admin function refuses without a session, and a per-handler attribute is a
// promise someone eventually forgets to repeat on a new endpoint.
var admin = api.MapGroup("/admin").RequireAuthorization();
admin.MapSignInEndpoints();
admin.MapPollAdminEndpoints();

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
