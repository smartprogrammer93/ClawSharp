namespace ClawSharp.Core.Tools;

/// <summary>
/// Registry for dynamic tool registration and lookup.
/// </summary>
public interface IToolRegistry
{
    /// <summary>Register a tool.</summary>
    void Register(ITool tool);

    /// <summary>Get a tool by name, or null if not found.</summary>
    ITool? Get(string name);

    /// <summary>Get all registered tools.</summary>
    IReadOnlyList<ITool> GetAll();

    /// <summary>Get specifications for all registered tools.</summary>
    IReadOnlyList<ToolSpec> GetSpecifications();
}
