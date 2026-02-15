using System.Collections.Concurrent;
using ClawSharp.Core.Tools;

namespace ClawSharp.Tools;

/// <summary>
/// Registry for dynamic tool registration and lookup.
/// Thread-safe implementation.
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new();

    /// <inheritdoc />
    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (!_tools.TryAdd(tool.Name, tool))
        {
            throw new InvalidOperationException(
                $"Tool '{tool.Name}' is already registered. Use Get to check if a tool exists before registering.");
        }
    }

    /// <inheritdoc />
    public ITool? Get(string name)
    {
        return _tools.GetValueOrDefault(name);
    }

    /// <inheritdoc />
    public IReadOnlyList<ITool> GetAll()
    {
        return _tools.Values.ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolSpec> GetSpecifications()
    {
        return _tools.Values.Select(t => t.Specification).ToList();
    }
}
