using System.Collections.Concurrent;
using ClawSharp.Core.Tools;

namespace ClawSharp.Infrastructure.Tools;

/// <summary>
/// Thread-safe in-memory tool registry.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools[tool.Specification.Name] = tool;
    }

    /// <inheritdoc />
    public ITool? Get(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;

    /// <inheritdoc />
    public IReadOnlyList<ITool> GetAll() => [.. _tools.Values];

    /// <inheritdoc />
    public IReadOnlyList<ToolSpec> GetSpecifications() =>
        [.. _tools.Values.Select(t => t.Specification)];
}
