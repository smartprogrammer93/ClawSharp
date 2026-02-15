using FluentAssertions;
using ClawSharp.Infrastructure.Security;
using ClawSharp.TestHelpers;
using Xunit;
using TestHelperFactory = ClawSharp.TestHelpers.TestHelpers;

namespace ClawSharp.Infrastructure.Tests.Security;

public class SecretStoreTests : IDisposable
{
    private readonly TempDirectory _tempDir;
    private readonly SecretStore _store;

    public SecretStoreTests()
    {
        _tempDir = TestHelperFactory.CreateTempDirectory();
        _store = new SecretStore(_tempDir.Path);
    }

    public void Dispose() => _tempDir.Dispose();

    [Fact]
    public void Set_And_Get_RoundTrips()
    {
        _store.Set("api_key", "sk-test123");
        _store.Get("api_key").Should().Be("sk-test123");
    }

    [Fact]
    public void Get_NonExistentKey_ReturnsNull()
    {
        _store.Get("nonexistent").Should().BeNull();
    }

    [Fact]
    public void Remove_ExistingKey_DeletesIt()
    {
        _store.Set("key", "value");
        _store.Remove("key");
        _store.Get("key").Should().BeNull();
    }

    [Fact]
    public void Remove_NonExistentKey_DoesNotThrow()
    {
        var act = () => _store.Remove("nonexistent");
        act.Should().NotThrow();
    }

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        _store.Set("key", "value1");
        _store.Set("key", "value2");
        _store.Get("key").Should().Be("value2");
    }

    [Fact]
    public void GetAll_ReturnsAllKeys()
    {
        _store.Set("key1", "value1");
        _store.Set("key2", "value2");
        var keys = _store.GetAllKeys();
        keys.Should().Contain("key1");
        keys.Should().Contain("key2");
    }

    [Fact]
    public void Exists_ReturnsTrueForExistingKey()
    {
        _store.Set("key", "value");
        _store.Exists("key").Should().BeTrue();
    }

    [Fact]
    public void Exists_ReturnsFalseForMissingKey()
    {
        _store.Exists("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void Persistence_LoadsFromDisk()
    {
        _store.Set("persistent_key", "persistent_value");

        // Create new store instance to test persistence
        var store2 = new SecretStore(_tempDir.Path);
        store2.Get("persistent_key").Should().Be("persistent_value");
    }

    [Fact]
    public void Values_AreEncryptedOnDisk()
    {
        _store.Set("secret_key", "super-secret-value");

        // Read raw file content
        var secretsDir = Path.Combine(_tempDir.Path, "secrets");
        var files = Directory.GetFiles(secretsDir, "*.enc");
        files.Should().HaveCountGreaterThan(0);

        // The file content should not contain the plaintext value
        var content = File.ReadAllText(files[0]);
        content.Should().NotContain("super-secret-value");
    }

    [Fact]
    public void Set_EmptyKey_ThrowsArgumentException()
    {
        var act = () => _store.Set("", "value");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Set_NullKey_ThrowsArgumentNullException()
    {
        var act = () => _store.Set(null!, "value");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Set_NullValue_RemovesKey()
    {
        _store.Set("key", "value");
        _store.Set("key", null!);
        _store.Get("key").Should().BeNull();
    }

    [Fact]
    public void HandlesSpecialCharactersInKey()
    {
        _store.Set("my.special/key:name", "value");
        _store.Get("my.special/key:name").Should().Be("value");
    }

    [Fact]
    public void HandlesLargeValues()
    {
        var largeValue = new string('x', 100_000);
        _store.Set("large_key", largeValue);
        _store.Get("large_key").Should().Be(largeValue);
    }

    [Fact]
    public void HandlesUnicodeValues()
    {
        var unicodeValue = "こんにちは世界🌍";
        _store.Set("unicode_key", unicodeValue);
        _store.Get("unicode_key").Should().Be(unicodeValue);
    }

    [Fact]
    public void SecretFile_HasRestrictedPermissions()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            // Skip on Windows
            return;
        }

        _store.Set("perm_key", "value");
        var secretsDir = Path.Combine(_tempDir.Path, "secrets");
        var files = Directory.GetFiles(secretsDir, "*.enc");

        foreach (var file in files)
        {
            var info = new FileInfo(file);
            var mode = info.UnixFileMode;

            // Should be owner read/write only (600)
            mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
