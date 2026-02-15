using System.Text.Json;
using ClawSharp.Core.Auth;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Core.Tests.Auth;

public class OAuthTokenManagerTests : IDisposable
{
    private readonly string _tempProfilesPath;
    private readonly string _testProfileId = "anthropic:test";
    
    public OAuthTokenManagerTests()
    {
        _tempProfilesPath = Path.Combine(Path.GetTempPath(), $"auth-profiles-{Guid.NewGuid()}.json");
    }
    
    public void Dispose()
    {
        if (File.Exists(_tempProfilesPath))
            File.Delete(_tempProfilesPath);
    }
    
    [Fact]
    public void Constructor_WithValidPath_SetsProperties()
    {
        var manager = new OAuthTokenManager(_tempProfilesPath, _testProfileId);
        
        Assert.NotNull(manager);
    }
    
    [Fact]
    public void CreateForOpenClaw_WithProfileId_ReturnsManager()
    {
        var manager = OAuthTokenManager.CreateForOpenClaw(_testProfileId);
        
        Assert.NotNull(manager);
    }
    
    [Fact]
    public async Task LoadCredentialAsync_FileNotFound_ReturnsNull()
    {
        var manager = new OAuthTokenManager(_tempProfilesPath, _testProfileId);
        
        var credential = await manager.LoadCredentialAsync();
        
        Assert.Null(credential);
    }
    
    [Fact]
    public async Task LoadCredentialAsync_ValidProfile_ReturnsCredential()
    {
        // Arrange
        var store = new AuthProfilesStore
        {
            Version = 1,
            Profiles = new Dictionary<string, OAuthCredential>
            {
                [_testProfileId] = new OAuthCredential
                {
                    Type = "oauth",
                    Provider = "anthropic",
                    Access = "sk-ant-test-access",
                    Refresh = "sk-ant-test-refresh",
                    Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()
                }
            }
        };
        var json = JsonSerializer.Serialize(store);
        await File.WriteAllTextAsync(_tempProfilesPath, json);
        
        var manager = new OAuthTokenManager(_tempProfilesPath, _testProfileId);
        
        // Act
        var credential = await manager.LoadCredentialAsync();
        
        // Assert
        Assert.NotNull(credential);
        Assert.Equal("anthropic", credential.Provider);
        Assert.Equal("sk-ant-test-access", credential.Access);
        Assert.Equal("sk-ant-test-refresh", credential.Refresh);
    }
    
    [Fact]
    public async Task LoadCredentialAsync_ProfileNotFound_ReturnsNull()
    {
        // Arrange
        var store = new AuthProfilesStore
        {
            Version = 1,
            Profiles = new Dictionary<string, OAuthCredential>
            {
                ["other:profile"] = new OAuthCredential
                {
                    Type = "oauth",
                    Provider = "anthropic",
                    Access = "sk-ant-test",
                    Refresh = "refresh"
                }
            }
        };
        var json = JsonSerializer.Serialize(store);
        await File.WriteAllTextAsync(_tempProfilesPath, json);
        
        var manager = new OAuthTokenManager(_tempProfilesPath, _testProfileId);
        
        // Act
        var credential = await manager.LoadCredentialAsync();
        
        // Assert
        Assert.Null(credential);
    }
    
    [Fact]
    public async Task IsTokenExpired_NoCachedCredential_ReturnsTrue()
    {
        var manager = new OAuthTokenManager(_tempProfilesPath, _testProfileId);
        
        var expired = manager.IsTokenExpired();
        
        // Without loading a credential, it should be considered expired
        Assert.True(expired);
    }
    
    [Fact]
    public async Task IsTokenExpired_TokenNotExpired_ReturnsFalse()
    {
        // Arrange
        var store = new AuthProfilesStore
        {
            Version = 1,
            Profiles = new Dictionary<string, OAuthCredential>
            {
                [_testProfileId] = new OAuthCredential
                {
                    Type = "oauth",
                    Provider = "anthropic",
                    Access = "sk-ant-test",
                    Refresh = "refresh",
                    Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()
                }
            }
        };
        var json = JsonSerializer.Serialize(store);
        await File.WriteAllTextAsync(_tempProfilesPath, json);
        
        var manager = new OAuthTokenManager(_tempProfilesPath, _testProfileId);
        
        // Act - LoadCredentialAsync populates the cache
        await manager.LoadCredentialAsync();
        var expired = manager.IsTokenExpired();
        
        // Assert
        Assert.False(expired);
    }
    
    [Fact]
    public async Task IsTokenExpired_TokenExpired_ReturnsTrue()
    {
        // Arrange
        var store = new AuthProfilesStore
        {
            Version = 1,
            Profiles = new Dictionary<string, OAuthCredential>
            {
                [_testProfileId] = new OAuthCredential
                {
                    Type = "oauth",
                    Provider = "anthropic",
                    Access = "sk-ant-test",
                    Refresh = "refresh",
                    Expires = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds()
                }
            }
        };
        var json = JsonSerializer.Serialize(store);
        await File.WriteAllTextAsync(_tempProfilesPath, json);
        
        var manager = new OAuthTokenManager(_tempProfilesPath, _testProfileId);
        
        // Act - LoadCredentialAsync populates the cache
        await manager.LoadCredentialAsync();
        var expired = manager.IsTokenExpired();
        
        // Assert
        Assert.True(expired);
    }
    
    [Fact]
    public async Task IsTokenExpired_NoExpirySet_ReturnsFalse()
    {
        // Arrange
        var store = new AuthProfilesStore
        {
            Version = 1,
            Profiles = new Dictionary<string, OAuthCredential>
            {
                [_testProfileId] = new OAuthCredential
                {
                    Type = "oauth",
                    Provider = "anthropic",
                    Access = "sk-ant-test",
                    Refresh = "refresh",
                    Expires = 0 // No expiry
                }
            }
        };
        var json = JsonSerializer.Serialize(store);
        await File.WriteAllTextAsync(_tempProfilesPath, json);
        
        var manager = new OAuthTokenManager(_tempProfilesPath, _testProfileId);
        
        // Act - LoadCredentialAsync populates the cache
        await manager.LoadCredentialAsync();
        var expired = manager.IsTokenExpired();
        
        // Assert
        Assert.False(expired);
    }
}

public class OAuthCredentialTests
{
    [Fact]
    public void OAuthCredential_DefaultValues_AreCorrect()
    {
        var credential = new OAuthCredential();
        
        Assert.Equal("oauth", credential.Type);
        Assert.Null(credential.Access);
        Assert.Null(credential.Refresh);
        Assert.Equal(0, credential.Expires);
    }
    
    [Fact]
    public void OAuthCredential_CanSetAllProperties()
    {
        var credential = new OAuthCredential
        {
            Type = "oauth",
            Provider = "anthropic",
            Access = "access-token",
            Refresh = "refresh-token",
            Expires = 1234567890123,
            Email = "test@example.com",
            EnterpriseUrl = "https://enterprise.example.com",
            ProjectId = "project-123",
            AccountId = "account-456"
        };
        
        Assert.Equal("oauth", credential.Type);
        Assert.Equal("anthropic", credential.Provider);
        Assert.Equal("access-token", credential.Access);
        Assert.Equal("refresh-token", credential.Refresh);
        Assert.Equal(1234567890123, credential.Expires);
        Assert.Equal("test@example.com", credential.Email);
        Assert.Equal("https://enterprise.example.com", credential.EnterpriseUrl);
        Assert.Equal("project-123", credential.ProjectId);
        Assert.Equal("account-456", credential.AccountId);
    }
}

public class AuthProfilesStoreTests
{
    [Fact]
    public void AuthProfilesStore_DefaultValues_AreCorrect()
    {
        var store = new AuthProfilesStore();
        
        Assert.Equal(0, store.Version);
        Assert.NotNull(store.Profiles);
        Assert.Empty(store.Profiles);
        Assert.Null(store.LastGood);
        Assert.Null(store.UsageStats);
    }
    
    [Fact]
    public void AuthProfilesStore_CanDeserialize_FromJson()
    {
        var json = """
        {
            "version": 1,
            "profiles": {
                "anthropic:default": {
                    "type": "oauth",
                    "provider": "anthropic",
                    "access": "sk-ant-test",
                    "refresh": "refresh-token",
                    "expires": 1802619357858
                }
            }
        }
        """;
        
        var store = JsonSerializer.Deserialize<AuthProfilesStore>(json);
        
        Assert.NotNull(store);
        Assert.Equal(1, store.Version);
        Assert.NotNull(store.Profiles);
        Assert.True(store.Profiles.ContainsKey("anthropic:default"));
        
        var cred = store.Profiles["anthropic:default"];
        Assert.Equal("oauth", cred.Type);
        Assert.Equal("anthropic", cred.Provider);
    }
}
