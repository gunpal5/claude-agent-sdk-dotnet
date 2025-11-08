using FluentAssertions;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace ClaudeAgentSdk.Tests;

/// <summary>
/// Tests for AIFunctionMcpConverter.
/// </summary>
public class AIFunctionMcpConverterTests
{
    [Fact]
    public void Constructor_WithNullFunctions_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new AIFunctionMcpConverter(null!, "test");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("aiFunctions");
    }

    [Fact]
    public void Constructor_WithNullServerName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var functions = Array.Empty<AIFunction>();

        // Act
        Action act = () => new AIFunctionMcpConverter(functions, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serverName");
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateConverter()
    {
        // Arrange
        var functions = Array.Empty<AIFunction>();

        // Act
        var converter = new AIFunctionMcpConverter(functions, "test-server");

        // Assert
        converter.Should().NotBeNull();
    }

    [Fact]
    public void ConvertToMcpToolSchema_WithNullFunction_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => AIFunctionMcpConverter.ConvertToMcpToolSchema(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("aiFunction");
    }

    [Fact]
    public void ConvertToMcpToolSchema_WithSimpleFunction_ShouldConvert()
    {
        // Arrange
        var function = AIFunctionFactory.Create(
            (string input) => $"Result: {input}",
            name: "TestFunction",
            description: "A test function");

        // Act
        var schema = AIFunctionMcpConverter.ConvertToMcpToolSchema(function);

        // Assert
        schema.Should().NotBeNull();
        schema.Name.Should().Be("TestFunction");
        schema.Description.Should().Be("A test function");
        schema.InputSchema.Should().NotBeNull();
        schema.InputSchema.Type.Should().Be("object");
    }

    [Fact]
    public void ConvertToMcpToolSchema_WithParameterizedFunction_ShouldIncludeParameters()
    {
        // Arrange
        var function = AIFunctionFactory.Create(
            ([Description("The city name")] string city,
             [Description("Temperature unit")] string unit) => $"{city}: 72°{unit}",
            name: "GetWeather",
            description: "Gets weather for a city");

        // Act
        var schema = AIFunctionMcpConverter.ConvertToMcpToolSchema(function);

        // Assert
        schema.Name.Should().Be("GetWeather");
        schema.Description.Should().Be("Gets weather for a city");
        schema.InputSchema.Properties.Should().ContainKey("city");
        schema.InputSchema.Properties.Should().ContainKey("unit");
    }

    [Fact]
    public void ConvertToMcpToolSchema_ShouldCreateValidInputSchema()
    {
        // Arrange
        var function = AIFunctionFactory.Create(
            (int number) => number * 2,
            name: "Double",
            description: "Doubles a number");

        // Act
        var schema = AIFunctionMcpConverter.ConvertToMcpToolSchema(function);

        // Assert
        schema.InputSchema.Type.Should().Be("object");
        schema.InputSchema.Properties.Should().NotBeNull();
        schema.InputSchema.Required.Should().NotBeNull();
    }

    [Fact]
    public void CreateMcpServerConfig_ShouldReturnValidConfig()
    {
        // Arrange
        var function = AIFunctionFactory.Create(
            () => "test",
            name: "Test");
        var converter = new AIFunctionMcpConverter(new[] { function }, "my-server");

        // Act
        var config = converter.CreateMcpServerConfig();

        // Assert
        config.Should().NotBeNull();
        config.Type.Should().Be("sdk");
        config.Name.Should().Be("my-server");
        config.Instance.Should().NotBeNull();
        config.Instance.Should().BeOfType<DynamicAIFunctionMcpServer>();
    }

    [Fact]
    public void DynamicAIFunctionMcpServer_Constructor_WithNullFunctions_ShouldThrow()
    {
        // Act
        Action act = () => new DynamicAIFunctionMcpServer(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DynamicAIFunctionMcpServer_Constructor_WithFunctions_ShouldCreate()
    {
        // Arrange
        var function = AIFunctionFactory.Create(() => "test", name: "Test");

        // Act
        var server = new DynamicAIFunctionMcpServer(new[] { function });

        // Assert
        server.Should().NotBeNull();
        server.ToolCount.Should().Be(1);
        server.ToolNames.Should().Contain("Test");
    }

    [Fact]
    public async Task DynamicAIFunctionMcpServer_ListToolsAsync_ShouldReturnAllTools()
    {
        // Arrange
        var func1 = AIFunctionFactory.Create(() => "test1", name: "Tool1");
        var func2 = AIFunctionFactory.Create(() => "test2", name: "Tool2");
        var server = new DynamicAIFunctionMcpServer(new[] { func1, func2 });

        // Act
        var response = await server.ListToolsAsync();

        // Assert
        response.Should().NotBeNull();
        response.Tools.Should().HaveCount(2);
        response.Tools.Should().Contain(t => t.Name == "Tool1");
        response.Tools.Should().Contain(t => t.Name == "Tool2");
    }

    [Fact]
    public async Task DynamicAIFunctionMcpServer_CallToolAsync_WithValidTool_ShouldExecute()
    {
        // Arrange
        var function = AIFunctionFactory.Create(
            (string input) => $"Echo: {input}",
            name: "Echo");
        var server = new DynamicAIFunctionMcpServer(new[] { function });

        var args = JsonDocument.Parse("""{"input": "hello"}""").RootElement;

        // Act
        var result = await server.CallToolAsync("Echo", args);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("Echo: hello");
    }

    [Fact]
    public async Task DynamicAIFunctionMcpServer_CallToolAsync_WithInvalidTool_ShouldThrow()
    {
        // Arrange
        var function = AIFunctionFactory.Create(() => "test", name: "Test");
        var server = new DynamicAIFunctionMcpServer(new[] { function });
        var args = JsonDocument.Parse("{}").RootElement;

        // Act
        Func<Task> act = async () => await server.CallToolAsync("NonExistent", args);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DynamicAIFunctionMcpServer_CallToolAsync_WithEmptyName_ShouldThrow()
    {
        // Arrange
        var function = AIFunctionFactory.Create(() => "test", name: "Test");
        var server = new DynamicAIFunctionMcpServer(new[] { function });
        var args = JsonDocument.Parse("{}").RootElement;

        // Act
        Func<Task> act = async () => await server.CallToolAsync("", args);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void McpToolSchema_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var schema = new McpToolSchema
        {
            Name = "test",
            Description = "desc",
            InputSchema = new McpInputSchema()
        };

        // Assert
        schema.Name.Should().Be("test");
        schema.Description.Should().Be("desc");
        schema.InputSchema.Should().NotBeNull();
    }

    [Fact]
    public void McpInputSchema_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var schema = new McpInputSchema();

        // Assert
        schema.Type.Should().Be("object");
        schema.Properties.Should().NotBeNull();
        schema.Properties.Should().BeEmpty();
        schema.Required.Should().NotBeNull();
        schema.Required.Should().BeEmpty();
    }

    [Fact]
    public void McpToolListResponse_ShouldInitializeToolsList()
    {
        // Arrange & Act
        var response = new McpToolListResponse();

        // Assert
        response.Tools.Should().NotBeNull();
        response.Tools.Should().BeEmpty();
        response.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task DynamicAIFunctionMcpServer_CallToolAsync_WithComplexArguments_ShouldWork()
    {
        // Arrange
        var function = AIFunctionFactory.Create(
            (string name, int age, bool active) => $"{name} is {age} years old and {(active ? "active" : "inactive")}",
            name: "FormatPerson");
        var server = new DynamicAIFunctionMcpServer(new[] { function });

        var args = JsonDocument.Parse("""
        {
            "name": "Alice",
            "age": 30,
            "active": true
        }
        """).RootElement;

        // Act
        var result = await server.CallToolAsync("FormatPerson", args);

        // Assert
        result.Should().Contain("Alice");
        result.Should().Contain("30");
        result.Should().Contain("active");
    }

    [Fact]
    public void DynamicAIFunctionMcpServer_ToolNames_ShouldReturnAllNames()
    {
        // Arrange
        var func1 = AIFunctionFactory.Create(() => "1", name: "First");
        var func2 = AIFunctionFactory.Create(() => "2", name: "Second");
        var func3 = AIFunctionFactory.Create(() => "3", name: "Third");
        var server = new DynamicAIFunctionMcpServer(new[] { func1, func2, func3 });

        // Act
        var names = server.ToolNames.ToList();

        // Assert
        names.Should().HaveCount(3);
        names.Should().Contain("First");
        names.Should().Contain("Second");
        names.Should().Contain("Third");
    }

    [Fact]
    public void DynamicAIFunctionMcpServer_ToolCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var functions = Enumerable.Range(0, 5)
            .Select(i => AIFunctionFactory.Create(() => "test", name: $"Tool{i}"))
            .ToArray();
        var server = new DynamicAIFunctionMcpServer(functions);

        // Act
        var count = server.ToolCount;

        // Assert
        count.Should().Be(5);
    }
}
