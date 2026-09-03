using System.Buffers.Text;
using System.Security.Cryptography;

namespace Rundfrage.Api.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 from the BCL. Verifies the single operator credential (FR-003).
/// </summary>
/// <remarks>
/// FR-045a requires the deployed configuration to hold something the password cannot be
/// recovered from, so <see cref="Generate"/> exists only to produce that value once - the
/// running application never calls it.
/// <para>
/// Encoded as <c>pbkdf2-sha256$iterations$salt$hash</c> so the work factor travels with the
/// value: raising it later does not invalidate an existing configured hash (research.md R-2).
/// </para>
/// </remarks>
public static class PasswordHash
{
    private const string Algorithm = "pbkdf2-sha256";

    /// <summary>
    /// Deliberately ':' and not the conventional '$' of the PHC string format.
    /// </summary>
    /// <remarks>
    /// FR-045 configures this value through a <c>.env</c> file, and Docker Compose reads
    /// <c>$name</c> in such a value as a variable reference - it substitutes an empty string and
    /// the hash reaches the container mangled, so no password can ever verify. The configuration
    /// mechanism is fixed by the specification; the encoding is ours, so the encoding gives way.
    /// ':' is special to no shell, no .env parser and no YAML scalar.
    /// </remarks>
    public const char Separator = ':';
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>Current OWASP guidance for PBKDF2-HMAC-SHA256.</summary>
    public const int Iterations = 600_000;

    public static string Generate(string password)
    {
        Span<byte> salt = stackalloc byte[SaltBytes];
        RandomNumberGenerator.Fill(salt);

        var hash = Derive(password, salt, Iterations);

        return string.Join(
            Separator,
            Algorithm,
            Iterations,
            Base64Url.EncodeToString(salt),
            Base64Url.EncodeToString(hash));
    }

    /// <summary>
    /// Returns false for anything it cannot parse. A configuration typo must refuse everyone
    /// rather than accept anyone - which is also why a plaintext password placed in
    /// <c>ADMIN_PASSWORD_HASH</c> can never verify (SC-015).
    /// </summary>
    public static bool Verify(string password, string? configuredHash)
    {
        if (string.IsNullOrWhiteSpace(configuredHash))
        {
            return false;
        }

        var parts = configuredHash.Split(Separator);
        if (parts.Length != 4 || parts[0] != Algorithm)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Base64Url.DecodeFromChars(parts[2]);
            expected = Base64Url.DecodeFromChars(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expected.Length != HashBytes)
        {
            return false;
        }

        var actual = Derive(password, salt, iterations);

        // Fixed-time comparison: a byte-by-byte early exit would leak how much of the hash matched.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, ReadOnlySpan<byte> salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
}
