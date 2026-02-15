using System.Text.Json.Serialization;

namespace ClawSharp.Core.Auth;

/// <summary>
/// Represents an OAuth credential with access token, refresh token, and expiration.
/// </summary>
public class OAuthCredential
{
    /// <summary>
    /// Credential type: "oauth", "token", or "api_key".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "oauth";
    
    /// <summary>
    /// Provider name (e.g., "anthropic", "minimax-portal").
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";
    
    /// <summary>
    /// The OAuth access token.
    /// </summary>
    [JsonPropertyName("access")]
    public string? Access { get; set; }
    
    /// <summary>
    /// The OAuth refresh token.
    /// </summary>
    [JsonPropertyName("refresh")]
    public string? Refresh { get; set; }
    
    /// <summary>
    /// Unix timestamp (milliseconds) when the token expires.
    /// </summary>
    [JsonPropertyName("expires")]
    public long Expires { get; set; }
    
    /// <summary>
    /// Optional email associated with the OAuth account.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    /// <summary>
    /// Optional enterprise URL.
    /// </summary>
    [JsonPropertyName("enterpriseUrl")]
    public string? EnterpriseUrl { get; set; }
    
    /// <summary>
    /// Optional project ID.
    /// </summary>
    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }
    
    /// <summary>
    /// Optional account ID.
    /// </summary>
    [JsonPropertyName("accountId")]
    public string? AccountId { get; set; }
}

/// <summary>
/// Represents the auth-profiles.json file structure.
/// </summary>
public class AuthProfilesStore
{
    [JsonPropertyName("version")]
    public int Version { get; set; }
    
    [JsonPropertyName("profiles")]
    public Dictionary<string, OAuthCredential> Profiles { get; set; } = new();
    
    [JsonPropertyName("lastGood")]
    public Dictionary<string, string>? LastGood { get; set; }
    
    [JsonPropertyName("usageStats")]
    public Dictionary<string, ProfileUsageStats>? UsageStats { get; set; }
}

/// <summary>
/// Usage statistics for an auth profile.
/// </summary>
public class ProfileUsageStats
{
    [JsonPropertyName("lastUsed")]
    public long LastUsed { get; set; }
    
    [JsonPropertyName("errorCount")]
    public int ErrorCount { get; set; }
    
    [JsonPropertyName("lastFailureAt")]
    public long? LastFailureAt { get; set; }
}
