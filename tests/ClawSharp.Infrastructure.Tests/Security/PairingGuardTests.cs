using FluentAssertions;
using ClawSharp.Infrastructure.Security;
using ClawSharp.TestHelpers;
using Xunit;
using TestHelperFactory = ClawSharp.TestHelpers.TestHelpers;

namespace ClawSharp.Infrastructure.Tests.Security;

public class PairingGuardTests : IDisposable
{
    private readonly TempDirectory _tempDir;
    private readonly PairingGuard _guard;

    public PairingGuardTests()
    {
        _tempDir = TestHelperFactory.CreateTempDirectory();
        _guard = new PairingGuard(_tempDir.Path);
    }

    public void Dispose() => _tempDir.Dispose();

    [Fact]
    public void GeneratePairingToken_ReturnsNonEmptyString()
    {
        var token = _guard.GeneratePairingToken();
        token.Should().NotBeNullOrEmpty();
        token.Length.Should().BeGreaterThan(20);
    }

    [Fact]
    public void GeneratePairingToken_UniqueEachTime()
    {
        var t1 = _guard.GeneratePairingToken();
        var t2 = _guard.GeneratePairingToken();
        t1.Should().NotBe(t2);
    }

    [Fact]
    public void GeneratePairingToken_IsBase64Encoded()
    {
        var token = _guard.GeneratePairingToken();
        var decoded = Convert.FromBase64String(token);
        decoded.Length.Should().BeGreaterThanOrEqualTo(32);
    }

    [Fact]
    public async Task ValidateAndPairAsync_ValidToken_ReturnsTrue()
    {
        var token = _guard.GeneratePairingToken();
        var result = await _guard.ValidateAndPairAsync(token, "device-1");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAndPairAsync_InvalidToken_ReturnsFalse()
    {
        var result = await _guard.ValidateAndPairAsync("invalid-token", "device-1");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAndPairAsync_ExpiredToken_ReturnsFalse()
    {
        var token = _guard.GeneratePairingToken(TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);
        var result = await _guard.ValidateAndPairAsync(token, "device-1");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAndPairAsync_ReusingToken_ReturnsFalse()
    {
        var token = _guard.GeneratePairingToken();
        await _guard.ValidateAndPairAsync(token, "device-1");
        var result = await _guard.ValidateAndPairAsync(token, "device-2");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsDevicePaired_AfterPairing_ReturnsTrue()
    {
        var token = _guard.GeneratePairingToken();
        await _guard.ValidateAndPairAsync(token, "device-1");
        _guard.IsDevicePaired("device-1").Should().BeTrue();
    }

    [Fact]
    public void IsDevicePaired_UnpairedDevice_ReturnsFalse()
    {
        _guard.IsDevicePaired("random-device").Should().BeFalse();
    }

    [Fact]
    public async Task GetPairedDevices_ReturnsAllPaired()
    {
        var t1 = _guard.GeneratePairingToken();
        var t2 = _guard.GeneratePairingToken();
        await _guard.ValidateAndPairAsync(t1, "device-1");
        await _guard.ValidateAndPairAsync(t2, "device-2");

        var devices = _guard.GetPairedDevices();
        devices.Should().Contain("device-1");
        devices.Should().Contain("device-2");
        devices.Should().HaveCount(2);
    }

    [Fact]
    public async Task UnpairDevice_RemovesPairing()
    {
        var token = _guard.GeneratePairingToken();
        await _guard.ValidateAndPairAsync(token, "device-1");
        _guard.IsDevicePaired("device-1").Should().BeTrue();

        _guard.UnpairDevice("device-1");
        _guard.IsDevicePaired("device-1").Should().BeFalse();
    }

    [Fact]
    public void UnpairDevice_NonExistent_DoesNotThrow()
    {
        var act = () => _guard.UnpairDevice("nonexistent");
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Persistence_LoadsFromDisk()
    {
        var token = _guard.GeneratePairingToken();
        await _guard.ValidateAndPairAsync(token, "device-1");

        // Create new guard instance to test persistence
        var guard2 = new PairingGuard(_tempDir.Path);
        guard2.IsDevicePaired("device-1").Should().BeTrue();
    }
}
