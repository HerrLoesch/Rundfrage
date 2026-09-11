using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Endpoints.Admin;
using Rundfrage.Api.Endpoints.Public;
using Rundfrage.Api.Http;
using Rundfrage.Api.Maintenance;
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
// One file in one directory. StorageSetup applies the four settings that carry requirements -
// busy timeout, journal mode, durability level and foreign keys - to every connection, which is
// why they live in one interceptor rather than in a connection string only some callers use.
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

// FR-030: state beside the storage, not inside it, so a restore cannot switch it off.
builder.Services.AddSingleton<MaintenanceState>();

builder.Services.AddScoped<BackupService>();
builder.Services.AddScoped<PollExport>();
builder.Services.AddScoped<PollImport>();
builder.Services.AddScoped<PollService>();
builder.Services.AddScoped<ResponseService>();
builder.Services.AddScoped<ResultsProjection>();
builder.Services.AddScoped<RetentionService>();
builder.Services.AddScoped<RestoreService>();

// research R-2: one gate, so a restore and the hourly sweep cannot hold the storage at once.
builder.Services.AddSingleton<RetentionSuspension>();

// FR-039c: erases what the access filter has already made unreachable.
builder.Services.AddHostedService<RetentionSweep>();

// FR-027a. In-memory partitions, so the request source is never written anywhere.
builder.Services.AddSubmissionRateLimiter(builder.Configuration);

// What the request source and scheme above are allowed to be read from when a reverse proxy
// terminates TLS in front of the application. Off unless the deployment says otherwise.
builder.Services.AddTrustedProxyHeaders(builder.Configuration);

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
        // Follows the real scheme rather than asserting Always, because http://localhost is a
        // supported way to run this and a Secure cookie there is one the browser may decline to
        // send back. "The real scheme" is only real if a proxy's X-Forwarded-Proto is read first
        // - see ReverseProxy, which is what makes this line correct behind TLS termination.
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

// --- Storage and schema (003 FR-004, FR-024) -----------------------------------------------
// Neither step may stop the host. If the directory cannot be prepared or the schema cannot be
// applied, the application still starts and serves; the admin area then says that storage is
// unavailable rather than showing an empty list, and no answer is ever confirmed.
var startupLog = app.Services.GetRequiredService<ILogger<Program>>();

StorageSetup.PrepareDirectory(dataDirectory, startupLog);

// Read before the schema is applied, because applying it is what creates the file. See the
// warning below for why anyone cares.
var storageExisted = File.Exists(StorageLocation.FileIn(dataDirectory));

await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseStartup.ApplyMigrationsAsync(
        scope.ServiceProvider.GetRequiredService<RundfrageDbContext>(),
        startupLog,
        CancellationToken.None);
}

// 003 FR-006. A first start and a start that lost its volume look identical from the outside:
// both find an empty directory, both create the schema, and both then serve an empty poll list
// without complaining. That is the one failure of a deployment that costs data while reporting
// success, so the two cases are told apart here and said out loud.
if (!storageExisted)
{
    startupLog.LogWarning(
        "No storage was present at start, so a new and empty one was created. On a first start "
        + "that is expected. On any later start it means the volume holding the data was not "
        + "mounted - the previous polls are still in it, and this instance is not writing to it. "
        + "Stop before anyone answers: starting fresh is not recoverable by restarting.");
}
else
{
    startupLog.LogInformation("Existing storage opened.");
}

// FR-007a. After the schema, because that is when the file first exists.
StorageSetup.SecureFile(dataDirectory, startupLog);

// --- Routing (FR-006a) ---------------------------------------------------------------------
// Everything under /api/v1 is the API; everything else belongs to the web application, so the
// SPA's client-side routes and the backend endpoints cannot collide on the shared origin.
// Before everything, so the rate limiter partitions by the participant and not by the proxy,
// and the session cookie's Secure flag follows the browser's scheme and not the proxy's.
app.UseTrustedProxyHeaders(startupLog);

// FR-026 to FR-028, before routing so a route added later is covered without anyone saying so.
// After the forwarded headers, so the notice is decided on the browser's request and not the
// proxy's.
app.UseMaintenanceMode();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api/v1");

// Liveness for the container runtime, before anything that could need a session or the storage.
api.MapHealthEndpoint();

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
admin.MapImportEndpoints();
admin.MapMaintenanceEndpoints();

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
