using SIGIC.Application.Security.Ports;
using SIGIC.Infrastructure.Security;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Security;

public class Argon2idPasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new Argon2idPasswordHasher();

    [Fact]
    public void HashPassword_GeneratesValidArgon2idFormattedHash()
    {
        // Arrange
        string password = "StrongPassword#2026!";

        // Act
        string hash = _hasher.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=4$", hash);
        string[] parts = hash.Split('$');
        Assert.True(parts.Length >= 6, "El hash debe contener formato versionado modular");
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        // Arrange
        string password = "CorrectPassword123";
        string hash = _hasher.HashPassword(password);

        // Act
        bool isValid = _hasher.VerifyPassword(password, hash);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_IncorrectPassword_ReturnsFalse()
    {
        // Arrange
        string password = "CorrectPassword123";
        string wrongPassword = "WrongPassword456";
        string hash = _hasher.HashPassword(password);

        // Act
        bool isValid = _hasher.VerifyPassword(wrongPassword, hash);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void HashPassword_SamePasswordTwice_GeneratesDifferentHashesDueToRandomSalt()
    {
        // Arrange
        string password = "IdenticalPassword789";

        // Act
        string hash1 = _hasher.HashPassword(password);
        string hash2 = _hasher.HashPassword(password);

        // Assert
        Assert.NotEqual(hash1, hash2);
        Assert.True(_hasher.VerifyPassword(password, hash1));
        Assert.True(_hasher.VerifyPassword(password, hash2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid_format")]
    [InlineData("$argon2id$v=19$m=1024,t=1,p=1$abc$def")]
    [InlineData(null)]
    public void VerifyPassword_MalformedOrManipulatedHash_ReturnsFalseSafely(string? malformedHash)
    {
        // Act & Assert
        bool result = _hasher.VerifyPassword("anyPassword", malformedHash!);
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void HashPassword_EmptyOrWhitespace_ThrowsArgumentException(string? invalidPassword)
    {
        Assert.Throws<ArgumentException>(() => _hasher.HashPassword(invalidPassword!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void VerifyPassword_EmptyOrWhitespacePassword_ReturnsFalse(string? emptyPassword)
    {
        string validHash = _hasher.HashPassword("ValidPassword123");
        bool result = _hasher.VerifyPassword(emptyPassword!, validHash);
        Assert.False(result);
    }
}
