using System.Text.Json;
using ClawSharp.Core.Security;
using ClawSharp.Core.Tools;
using FluentAssertions;
using NSubstitute;

namespace ClawSharp.Tools.Tests;

public class FileReadToolTests
{
    private readonly ISecurityPolicy _securityPolicy;

    public FileReadToolTests()
    {
        _securityPolicy = Substitute.For<ISecurityPolicy>();
        _securityPolicy.IsPathAllowed(Arg.Any<string>()).Returns(true);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingFile_ReturnsContent()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "line1\nline2\nline3");
            var tool = new FileReadTool(_securityPolicy);
            var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": """ + path.Replace("\\", "\\\\") + @"""}");

            var result = await tool.ExecuteAsync(args);

            result.Success.Should().BeTrue();
            result.Output.Should().Contain("line1");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentFile_ReturnsError()
    {
        var tool = new FileReadTool(_securityPolicy);
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": ""/nonexistent/file.txt""}");

        var result = await tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WithOffsetAndLimit_ReturnsSubset()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "line1\nline2\nline3\nline4\nline5");
            var tool = new FileReadTool(_securityPolicy);
            var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": """ + path.Replace("\\", "\\\\") + @""", ""offset"": 2, ""limit"": 2}");

            var result = await tool.ExecuteAsync(args);

            result.Output.Should().Contain("line2");
            result.Output.Should().Contain("line3");
            result.Output.Should().NotContain("line1");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DisallowedPath_ReturnsError()
    {
        var security = Substitute.For<ISecurityPolicy>();
        security.IsPathAllowed(Arg.Any<string>()).Returns(false);
        var tool = new FileReadTool(security);
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": ""/etc/passwd""}");

        var result = await tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not allowed");
    }

    [Fact]
    public async Task ExecuteAsync_MissingPathParameter_ReturnsError()
    {
        var tool = new FileReadTool(_securityPolicy);
        var args = JsonSerializer.Deserialize<JsonElement>("{}");

        var result = await tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("path");
    }

    [Fact]
    public void Specification_HasCorrectSchema()
    {
        var tool = new FileReadTool(_securityPolicy);

        tool.Name.Should().Be("read_file");
        tool.Specification.Name.Should().Be("read_file");
    }
}

public class FileWriteToolTests
{
    private readonly ISecurityPolicy _securityPolicy;

    public FileWriteToolTests()
    {
        _securityPolicy = Substitute.For<ISecurityPolicy>();
        _securityPolicy.IsPathAllowed(Arg.Any<string>()).Returns(true);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesFileAndParentDirs()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var path = Path.Combine(dir, "sub", "file.txt");
        try
        {
            var tool = new FileWriteTool(_securityPolicy);
            var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": """ + path.Replace("\\", "\\\\") + @""", ""content"": ""hello""}");

            var result = await tool.ExecuteAsync(args);

            result.Success.Should().BeTrue();
            File.ReadAllText(path).Should().Be("hello");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_OverwritesExistingFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "original");
            var tool = new FileWriteTool(_securityPolicy);
            var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": """ + path.Replace("\\", "\\\\") + @""", ""content"": ""updated""}");

            var result = await tool.ExecuteAsync(args);

            result.Success.Should().BeTrue();
            File.ReadAllText(path).Should().Be("updated");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DisallowedPath_ReturnsError()
    {
        var security = Substitute.For<ISecurityPolicy>();
        security.IsPathAllowed(Arg.Any<string>()).Returns(false);
        var tool = new FileWriteTool(security);
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": ""/etc/passwd"", ""content"": ""test""}");

        var result = await tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not allowed");
    }

    [Fact]
    public async Task ExecuteAsync_MissingPathParameter_ReturnsError()
    {
        var tool = new FileWriteTool(_securityPolicy);
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""content"": ""test""}");

        var result = await tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("path");
    }

    [Fact]
    public void Specification_HasCorrectSchema()
    {
        var tool = new FileWriteTool(_securityPolicy);

        tool.Name.Should().Be("write_file");
        tool.Specification.Name.Should().Be("write_file");
    }
}

public class EditFileToolTests
{
    private readonly ISecurityPolicy _securityPolicy;

    public EditFileToolTests()
    {
        _securityPolicy = Substitute.For<ISecurityPolicy>();
        _securityPolicy.IsPathAllowed(Arg.Any<string>()).Returns(true);
    }

    [Fact]
    public async Task ExecuteAsync_ReplacesExactText()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "Hello World");
            var tool = new EditFileTool(_securityPolicy);
            var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": """ + path.Replace("\\", "\\\\") + @""", ""old_string"": ""World"", ""new_string"": ""DotClaw""}");

            var result = await tool.ExecuteAsync(args);

            result.Success.Should().BeTrue();
            File.ReadAllText(path).Should().Be("Hello DotClaw");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MultipleMatches_ReturnsError()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "foo bar foo");
            var tool = new EditFileTool(_securityPolicy);
            var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": """ + path.Replace("\\", "\\\\") + @""", ""old_string"": ""foo"", ""new_string"": ""baz""}");

            var result = await tool.ExecuteAsync(args);

            result.Success.Should().BeFalse();
            result.Error.Should().Contain("2 times");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NoMatch_ReturnsError()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "Hello World");
            var tool = new EditFileTool(_securityPolicy);
            var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": """ + path.Replace("\\", "\\\\") + @""", ""old_string"": ""xyz"", ""new_string"": ""abc""}");

            var result = await tool.ExecuteAsync(args);

            result.Success.Should().BeFalse();
            result.Error.Should().Contain("not found");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DisallowedPath_ReturnsError()
    {
        var security = Substitute.For<ISecurityPolicy>();
        security.IsPathAllowed(Arg.Any<string>()).Returns(false);
        var tool = new EditFileTool(security);
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": ""/etc/passwd"", ""old_string"": ""test"", ""new_string"": ""new""}");

        var result = await tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not allowed");
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredParameters_ReturnsError()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "Hello");
            var tool = new EditFileTool(_securityPolicy);
            var args = JsonSerializer.Deserialize<JsonElement>(@"{""path"": """ + path.Replace("\\", "\\\\") + @"""}");

            var result = await tool.ExecuteAsync(args);

            result.Success.Should().BeFalse();
            result.Error.Should().Contain("old_string");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Specification_HasCorrectSchema()
    {
        var tool = new EditFileTool(_securityPolicy);

        tool.Name.Should().Be("edit_file");
        tool.Specification.Name.Should().Be("edit_file");
    }
}
