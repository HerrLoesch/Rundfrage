namespace Rundfrage.Api.Security;

/// <summary>
/// The single operator account, configured at deployment (FR-045). There is no user table,
/// because there is no user management.
/// </summary>
public sealed class AdminAccount
{
    public const string UserVariable = "ADMIN_USER";
    public const string PasswordHashVariable = "ADMIN_PASSWORD_HASH";

    public required string User { get; init; }

    public required string PasswordHash { get; init; }

    /// <summary>
    /// Reads the account from configuration, refusing anything incomplete.
    /// </summary>
    /// <remarks>
    /// Deliberately has no fallback. Feature 001 could start on built-in defaults because it
    /// exposed nothing; an admin area that creates polls must not. A guessable built-in
    /// credential would be worse than no protection, because it would look like protection.
    /// </remarks>
    public static AdminAccount FromConfiguration(IConfiguration configuration)
    {
        var user = configuration[UserVariable];
        var hash = configuration[PasswordHashVariable];

        if (string.IsNullOrWhiteSpace(user))
        {
            throw new InvalidOperationException(
                $"{UserVariable} is not set. The admin area has exactly one account and no default; "
                + "see .env.example.");
        }

        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new InvalidOperationException(
                $"{PasswordHashVariable} is not set. Generate one with: "
                + "dotnet Rundfrage.Api.dll --hash-password");
        }

        return new AdminAccount { User = user, PasswordHash = hash };
    }

    /// <summary>
    /// Verifies a sign-in attempt. Both the user name and the password are checked in a way that
    /// takes the same work either way, so a failure cannot reveal which half was wrong (FR-004).
    /// </summary>
    public bool Matches(string? user, string? password)
    {
        var userMatches = string.Equals(user, User, StringComparison.Ordinal);

        // The hash is verified even when the user name is already wrong: skipping it would make
        // a wrong user measurably faster than a wrong password.
        var passwordMatches = Security.PasswordHash.Verify(password ?? string.Empty, PasswordHash);

        return userMatches && passwordMatches;
    }
}
