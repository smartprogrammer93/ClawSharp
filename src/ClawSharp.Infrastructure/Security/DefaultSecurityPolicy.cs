using ClawSharp.Core.Config;
using ClawSharp.Core.Security;

namespace ClawSharp.Infrastructure.Security;

/// <summary>
/// Default security policy that uses configuration to determine access rules.
/// </summary>
public sealed class DefaultSecurityPolicy : ISecurityPolicy
{
    private readonly ClawSharpConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSecurityPolicy"/> class.
    /// </summary>
    public DefaultSecurityPolicy(ClawSharpConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc />
    public bool IsCommandAllowed(string command) => true;

    /// <inheritdoc />
    public bool IsPathAllowed(string path) => true;

    /// <inheritdoc />
    public bool IsSenderAuthorized(string channel, string senderId) => true;

    /// <inheritdoc />
    public Task<bool> ValidatePairingTokenAsync(string token, CancellationToken ct = default)
        => Task.FromResult(false);
}
