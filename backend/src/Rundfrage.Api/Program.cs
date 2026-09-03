using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Endpoints.Admin;
using Rundfrage.Api.Endpoints.Public;
using Rundfrage.Api.Http;
using Rundfrage.Api.Retention;
using Rundfrage.Api.Polls;
using Rundfrage.Api.Security;
using Rundfrage.Api.Time;
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

// --- Data access (003 FR-002, FR-007, research.md R-1) -------------------------------------
// One file in one directory. StorageSetup applies the three settings that carry requirements -
// journal mode, durability level and busy timeout - to every connection, which is why they live
// in one interceptor rather than in a connection string that only some callers use.
var dataDirectory = StorageLocation.DirectoryFrom(builder.Configuration);
builder.Services.AddDbContext<RundfrageDbContext>(options => options
    .UseSqlite(StorageLocation.ConnectionStringFor(dataDirectory))
    .AddInterceptors(StorageSetup.Interceptor));
builder.Services.AddSingleton(new StorageDirectory(dataDirectory));

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

builder.Services.AddScoped<BackupService>();
builder.Services.AddScoped<PollExport>();
builder.Services.AddScoped<PollService>();
builder.Services.AddScoped<ResponseService>();
builder.Services.AddScoped<ResultsProjection>();
builder.Services.AddScoped<RetentionService>();

// FR-039c: erases what the access filter has already made unreachable.
builder.Services.AddHostedService<RetentionSweep>();

// FR-027a. In-memory partitions, so the request source is never written anywhere.
builder.Services.AddSubmissionRateLimiter(builder.Configuration);

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
StorageSetup.PrepareDirectory(dataDirectory, app.Services.GetRequiredService<ILogger<Program>>());

await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseStartup.ApplyMigrationsAsync(
        scope.ServiceProvider.GetRequiredService<RundfrageDbContext>(),
        app.Services.GetRequiredService<ILogger<Program>>(),
        CancellationToken.None);
}

// FR-007a. After the schema, because that is when the file first exists.
StorageSetup.SecureFile(dataDirectory, app.Services.GetRequiredService<ILogger<Program>>());

// --- Routing (FR-006a) ---------------------------------------------------------------------
// Everything under /api/v1 is the API; everything else belongs to the web application, so the
// SPA's client-side routes and the backend endpoints cannot collide on the shared origin.
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api/v1");

// --- Participant routes (Principle I) -------------------------------------------------------
// No session, no account, no email. The token in the path is the authorisation.
api.MapPollEndpoints();
api.MapResponseEndpoints();

// --- Admin (FR-001, FR-048) ----------------------------------------------------------------
// The requirement is applied to the whole group, not to individual handlers. FR-048 asserts
// that *every* admin function refuses without a session, and a per-handler attribute is a
// promise someone eventually forgets to repeat on a new endpoint.
var admin = api.MapGroup("/admin").RequireAuthorization();
admin.MapSignInEndpoints();
admin.MapPollAdminEndpoints();
admin.MapBackupEndpoint();

app.UseDefaultFiles();
app.UseStaticFiles();

// Unmatched API paths must 404 rather than fall through to the SPA shell. This catch-all has
// lower route precedence than the specific endpoints above, so it only sees genuine misses.
//
// It answers with the *same* neutral payload as a token miss. A bare 404 here was
// distinguishable from `{"code":"not_found"}`, so an empty or oddly-shaped token - which does
// not match the route at all and lands here - could be told apart from a well-formed unknown
// one. That is exactly the distinction SC-012 denies.
app.Map("/api/{**rest}", () => NeutralNotFound.Result()).AllowAnonymous();

// Every other unmatched path serves the SPA shell so client-side routing works on reload.
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Exposed so the integration tests can drive the host with WebApplicationFactory.</summary>
public partial class Program;
