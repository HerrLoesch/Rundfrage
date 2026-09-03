using System.Buffers.Text;
using System.Security.Cryptography;

namespace Rundfrage.Api.Security;

/// <summary>
/// Mints the unguessable tokens that carry every participant capability (FR-017).
/// </summary>
/// <remarks>
/// Under Principle I there is no account to check, so the token in the URL <i>is</i> the
/// authorisation. <see cref="Mint"/> deliberately takes no argument: a token that could be
/// derived from the title, the days, or a counter would be guessable from public information.
/// </remarks>
public static class CapabilityToken
{
    private const int ByteLength = 16;

    /// <summary>128 bits, above SC-006's floor of 120.</summary>
    public const int EntropyBits = ByteLength * 8;

    /// <summary>Base64url encoding of 16 bytes, without padding.</summary>
    public const int TokenLength = 22;

    public static string Mint()
    {
        Span<byte> bytes = stackalloc byte[ByteLength];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>
    /// Shape check only. It deliberately does <b>not</b> short-circuit a lookup: a malformed
    /// token must cost roughly what an unknown one costs, or the timing difference would let
    /// someone tell "never existed" from "wrong shape" - the distinction SC-012 denies
    /// (research.md R-4).
    /// </summary>
    public static bool IsWellFormed(string? candidate)
    {
        if (candidate is not { Length: TokenLength })
        {
            return false;
        }

        foreach (var c in candidate)
        {
            var allowed = c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}
