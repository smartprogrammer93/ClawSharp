using System.Text.Json;
using ClawSharp.Core.Tools;
using FluentAssertions;
using NSubstitute;

namespace ClawSharp.Tools.Tests;

public class ToolRegistryTests
{
    [Fact]
    public void Register_NewTool_Succeeds()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("test");

        // Act
        registry.Register(tool);

        // Assert
        registry.Get("test").Should().Be(tool);
    }

    [Fact]
    public void Register_DuplicateName_Throws()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool1 = Substitute.For<ITool>();
        tool1.Name.Returns("test");
        var tool2 = Substitute.For<ITool>();
        tool2.Name.Returns("test");
        registry.Register(tool1);

        // Act
        var act = () => registry.Register(tool2);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*already registered*");
    }

    [Fact]
    public void Get_NonExistent_ReturnsNull()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
        registry.Get("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool1 = Substitute.For<ITool>();
        tool1.Name.Returns("t1");
        var tool2 = Substitute.For<ITool>();
        tool2.Name.Returns("t2");
        registry.Register(tool1);
        registry.Register(tool2);

        // Act
        var result = registry.GetAll();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetSpecifications_ReturnsAllSpecs()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("test");
        var schema = JsonSerializer.Deserialize<JsonElement>("""{ "type": "object", "properties": {} }""");
        tool.Specification.Returns(new ToolSpec("test", "desc", schema));
        registry.Register(tool);

        // Act
        var specs = registry.GetSpecifications();

        // Assert
        specs.Should().HaveCount(1);
        specs[0].Name.Should().Be("test");
    }

    [Fact]
    public void GetSpecifications_EmptyRegistry_ReturnsEmpty()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var specs = registry.GetSpecifications();

        // Assert
        specs.Should().BeEmpty();
    }

    [Fact]
    public void GetAll_EmptyRegistry_ReturnsEmpty()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var result = registry.GetAll();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Register_NullTool_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var act = () => registry.Register(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_MultipleTools_AllAccessible()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool1 = Substitute.For<ITool>();
        tool1.Name.Returns("shell");
        var tool2 = Substitute.For<ITool>();
        tool2.Name.Returns("file_read");
        var tool3 = Substitute.For<ITool>();
        tool3.Name.Returns("file_write");

        // Act
        registry.Register(tool1);
        registry.Register(tool2);
        registry.Register(tool3);

        // Assert
        registry.Get("shell").Should().Be(tool1);
        registry.Get("file_read").Should().Be(tool2);
        registry.Get("file_write").Should().Be(tool3);
    }
}
