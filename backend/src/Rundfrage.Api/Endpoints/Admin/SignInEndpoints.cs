using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.Endpoints.Admin;

/// <summary>Signing the single operator in and out (FR-001, FR-004 to FR-007).</summary>
public static class SignInEndpoints
{
    public static IEndpointRouteBuilder MapSignInEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/session", async (
            SignInRequest request,
            AdminAccount account,
            SignInThrottle throttle,
            HttpContext http,
            ILogger<AdminAccount> logger) =>
        {
            // Checked before the password, and a correct password is refused too: otherwise the
            // lockout itself would answer "was that the right password?" (FR-005a).
            if (throttle.IsLocked(out var retryAfter))
            {
                logger.LogWarning("Sign-in refused: account locked");
                return Results.Json(
                    new { code = "account_locked", retryAfterSeconds = (int)retryAfter.TotalSeconds },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            if (!account.Matches(request.User, request.Password))
            {
                throttle.RecordFailure();
                // No hint about which half was wrong, and no user name in the log (FR-004, FR-043b).
                logger.LogWarning("Sign-in failed");
                return Results.Json(new { code = "unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            throttle.RecordSuccess();

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, account.User)],
                CookieAuthenticationDefaults.AuthenticationScheme);

            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            logger.LogInformation("Sign-in succeeded");
            return Results.NoContent();
        })
        .AllowAnonymous()
        .WithName("signIn");

        routes.MapDelete("/session", async (HttpContext http) =>
        {
            // FR-007: deliberately ending a session must work.
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        })
        .AllowAnonymous()
        .WithName("signOut");

        return routes;
    }
}

public sealed record SignInRequest(string? User, string? Password);
