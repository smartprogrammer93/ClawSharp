namespace ClawSharp.Core.Security;

/// <summary>
/// Security policy for command execution, path access, and sender authorization.
/// </summary>
public interface ISecurityPolicy
{
    /// <summary>Check if a shell command is allowed to execute.</summary>
    bool IsCommandAllowed(string command);

    /// <summary>Check if a file path is allowed to access.</summary>
    bool IsPathAllowed(string path);

    /// <summary>Check if a sender is authorized on a given channel.</summary>
    bool IsSenderAuthorized(string channel, string senderId);

    /// <summary>Validate a pairing token for node authentication.</summary>
    Task<bool> ValidatePairingTokenAsync(string token, CancellationToken ct = default);
}
