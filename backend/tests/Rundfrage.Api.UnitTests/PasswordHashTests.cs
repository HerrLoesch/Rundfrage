using Rundfrage.Api.Security;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// FR-003 and FR-045a. The deployed configuration holds a hash, never a password, so the
/// application only ever verifies.
/// </summary>
public class PasswordHashTests
{
    [Fact]
    public void A_generated_hash_verifies_its_own_password()
    {
        var hash = PasswordHash.Generate("correct horse battery staple");

        Assert.True(PasswordHash.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void A_wrong_password_does_not_verify()
    {
        var hash = PasswordHash.Generate("correct horse battery staple");

        Assert.False(PasswordHash.Verify("Correct horse battery staple", hash));
        Assert.False(PasswordHash.Verify("", hash));
        Assert.False(PasswordHash.Verify("something else entirely", hash));
    }

    [Fact]
    public void The_same_password_hashes_differently_every_time()
    {
        // A random salt per hash. Without it, two operators with the same password would be
        // visibly identical in configuration.
        var first = PasswordHash.Generate("same password");
        var second = PasswordHash.Generate("same password");

        Assert.NotEqual(first, second);
        Assert.True(PasswordHash.Verify("same password", first));
        Assert.True(PasswordHash.Verify("same password", second));
    }

    [Fact]
    public void The_hash_string_describes_its_own_parameters()
    {
        // So the iteration count can be raised later without invalidating existing hashes.
        var hash = PasswordHash.Generate("whatever");

        var parts = hash.Split(PasswordHash.Separator);
        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.True(int.TryParse(parts[1], out var iterations));
        Assert.True(iterations >= 600_000, $"iteration count {iterations} is below current guidance");
    }

    [Fact]
    public void The_hash_survives_being_placed_in_an_env_file()
    {
        // Found by running it, not by reasoning about it. The conventional PHC encoding uses
        // '$' as its separator - and Docker Compose reads '$name' in a .env value as a variable
        // reference and substitutes an empty string. The hash arrived at the container mangled
        // and no password could ever verify.
        //
        // The configuration mechanism is fixed by FR-045; the encoding is ours to choose, so the
        // encoding gives way.
        var hash = PasswordHash.Generate("whatever");

        Assert.DoesNotContain('$', hash);
    }

    [Fact]
    public void The_hash_contains_only_characters_that_need_no_quoting_anywhere()
    {
        // Belt and braces for the same class of defect: shells, .env parsers and YAML all have
        // their own special characters.
        var hash = PasswordHash.Generate("whatever");

        Assert.Matches("^[A-Za-z0-9:_-]+$", hash);
    }

    [Fact]
    public void The_password_cannot_be_read_out_of_the_hash()
    {
        // FR-045a in its plainest form.
        const string password = "a-very-distinctive-passphrase";

        var hash = PasswordHash.Generate(password);

        Assert.DoesNotContain(password, hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("pbkdf2-sha256$notanumber$salt$hash")]
    [InlineData("pbkdf2-sha256$600000$only-three-parts")]
    [InlineData("bcrypt$12$salt$hash")]
    public void A_malformed_configured_hash_never_verifies(string configured)
    {
        // A configuration typo must refuse everyone, not accept anyone.
        Assert.False(PasswordHash.Verify("any password at all", configured));
    }

    [Fact]
    public void A_plaintext_password_is_not_usable_as_the_configured_hash()
    {
        // SC-015: putting the password itself into ADMIN_PASSWORD_HASH must not work.
        Assert.False(PasswordHash.Verify("hunter2", "hunter2"));
    }
}
