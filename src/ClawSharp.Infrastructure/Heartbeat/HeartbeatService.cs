using ClawSharp.Core.Config;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Tools;
using Microsoft.Extensions.Logging;
using ProviderToolSpec = ClawSharp.Core.Providers.ToolSpec;

namespace ClawSharp.Infrastructure.Heartbeat;

/// <summary>
/// Service that executes periodic heartbeat checks.
/// Reads HEARTBEAT.md from the workspace and triggers agent actions.
/// </summary>
public sealed class HeartbeatService
{
    private readonly ClawSharpConfig _config;
    private readonly ILlmProvider _provider;
    private readonly IToolRegistry _tools;
    private readonly ILogger<HeartbeatService> _logger;
    private DateTimeOffset? _lastExecutionTime;

    private const string HeartbeatOkResponse = "HEARTBEAT_OK";
    private const string HeartbeatMdFileName = "HEARTBEAT.md";

    /// <summary>
    /// Creates a new HeartbeatService.
    /// </summary>
    public HeartbeatService(
        ClawSharpConfig config,
        ILlmProvider provider,
        IToolRegistry tools,
        ILogger<HeartbeatService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Whether heartbeat is enabled in the configuration.
    /// </summary>
    public bool IsEnabled => _config.Heartbeat.Enabled;

    /// <summary>
    /// The configured interval between heartbeats.
    /// </summary>
    public TimeSpan Interval => TimeSpan.FromSeconds(_config.Heartbeat.IntervalSeconds);

    /// <summary>
    /// The time of the last heartbeat execution, or null if never executed.
    /// </summary>
    public DateTimeOffset? LastExecutionTime => _lastExecutionTime;

    /// <summary>
    /// Executes a heartbeat check.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the heartbeat execution.</returns>
    public async Task<HeartbeatResult> ExecuteHeartbeatAsync(CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("Heartbeat is disabled, skipping");
            return HeartbeatResult.CreateSkipped();
        }

        ct.ThrowIfCancellationRequested();

        try
        {
            var messages = BuildMessages();
            var toolSpecs = BuildToolSpecs();

            var request = new LlmRequest
            {
                Model = _config.DefaultModel ?? "gpt-4o",
                Messages = messages,
                Tools = toolSpecs,
                Temperature = _config.DefaultTemperature
            };

            _logger.LogDebug("Executing heartbeat with {MessageCount} messages", messages.Count);

            var response = await _provider.CompleteAsync(request, ct);
            var content = response.Content ?? string.Empty;

            _lastExecutionTime = DateTimeOffset.UtcNow;

            var requiresDelivery = !IsHeartbeatOk(content);

            _logger.LogDebug("Heartbeat completed: {Response}, RequiresDelivery: {RequiresDelivery}",
                content.Length > 50 ? content[..50] + "..." : content, requiresDelivery);

            return HeartbeatResult.CreateSuccess(content, requiresDelivery);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Heartbeat execution failed");
            _lastExecutionTime = DateTimeOffset.UtcNow;
            return HeartbeatResult.CreateError(ex.Message);
        }
    }

    private List<LlmMessage> BuildMessages()
    {
        var messages = new List<LlmMessage>();

        // System message with heartbeat prompt
        var systemContent = _config.Heartbeat.Prompt;

        // Try to read HEARTBEAT.md
        var heartbeatMdContent = TryReadHeartbeatMd();
        if (!string.IsNullOrEmpty(heartbeatMdContent))
        {
            systemContent += $"\n\n## HEARTBEAT.md Content:\n{heartbeatMdContent}";
        }

        messages.Add(new LlmMessage("system", systemContent));
        messages.Add(new LlmMessage("user", "Execute heartbeat check."));

        return messages;
    }

    private string? TryReadHeartbeatMd()
    {
        try
        {
            var heartbeatMdPath = Path.Combine(_config.WorkspaceDir, HeartbeatMdFileName);
            if (File.Exists(heartbeatMdPath))
            {
                return File.ReadAllText(heartbeatMdPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read HEARTBEAT.md");
        }

        return null;
    }

    private List<ProviderToolSpec>? BuildToolSpecs()
    {
        var specs = _tools.GetSpecifications();
        if (specs.Count == 0)
        {
            return null;
        }

        return specs.Select(s => new ProviderToolSpec(s.Name, s.Description, s.ParametersSchema)).ToList();
    }

    private static bool IsHeartbeatOk(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        // Check for exact match or if the response contains HEARTBEAT_OK
        return response.Trim().Equals(HeartbeatOkResponse, StringComparison.OrdinalIgnoreCase) ||
               response.Contains(HeartbeatOkResponse, StringComparison.OrdinalIgnoreCase);
    }
}
