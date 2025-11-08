using FluentAssertions;
using Microsoft.Extensions.AI;

namespace ClaudeAgentSdk.Tests;

/// <summary>
/// Tests for AIFunctionMcpExtensions.
/// </summary>
public class AIFunctionMcpExtensionsTests
{
    [Fact]
    public void ToMcpServer_WithFunctions_ShouldCreateConfig()
    {
        // Arrange
        var function = AIFunctionFactory.Create(() => "test", name: "Test");
        var functions = new[] { function };

        // Act
        var config = functions.ToMcpServer("my-server");

        // Assert
        config.Should().NotBeNull();
        config.Type.Should().Be("sdk");
        config.Name.Should().Be("my-server");
        config.Instance.Should().BeOfType<DynamicAIFunctionMcpServer>();
    }

    [Fact]
    public void ToMcpServer_WithDefaultServerName_ShouldUseDefault()
    {
        // Arrange
        var function = AIFunctionFactory.Create(() => "test", name: "Test");
        var functions = new[] { function };

        // Act
        var config = functions.ToMcpServer();

        // Assert
        config.Name.Should().Be("ai-functions");
    }

    [Fact]
    public void WithAIFunctionTools_WithNullOptions_ShouldThrow()
    {
        // Arrange
        ClaudeCodeChatClientOptions? options = null;
        var functions = Array.Empty<AIFunction>();

        // Act
        Action act = () => options!.WithAIFunctionTools(functions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void WithAIFunctionTools_WithNullFunctions_ShouldThrow()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        Action act = () => options.WithAIFunctionTools(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("aiFunctions");
    }

    [Fact]
    public void WithAIFunctionTools_WithFunctions_ShouldAddMcpServer()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();
        var function = AIFunctionFactory.Create(() => "test", name: "Test");
        var functions = new[] { function };

        // Act
        var result = options.WithAIFunctionTools(functions);

        // Assert
        result.Should().BeSameAs(options);
        result.McpServers.Should().NotBeNull();
        result.McpServers.Should().ContainKey("ai-functions");
    }

    [Fact]
    public void WithAIFunctionTools_WithDisableBuiltInTools_ShouldDisableTools()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();
        var function = AIFunctionFactory.Create(() => "test", name: "Test");
        var functions = new[] { function };

        // Act
        var result = options.WithAIFunctionTools(functions, disableBuiltInTools: true);

        // Assert - When disableBuiltInTools is true, we use AllowedTools instead of DisallowedTools
        // This follows the pattern from the official Python SDK
        result.AllowedTools.Should().NotBeNull();
        result.AllowedTools.Should().NotBeEmpty();
        result.AllowedTools.Should().Contain("mcp__ai-functions__Test", "The MCP tool should be explicitly allowed");
        result.DisallowedTools.Should().BeEmpty("DisallowedTools should be cleared when using AllowedTools");
    }

    [Fact]
    public void WithAIFunctionTools_WithoutDisablingBuiltInTools_ShouldNotDisable()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();
        var function = AIFunctionFactory.Create(() => "test", name: "Test");
        var functions = new[] { function };

        // Act
        var result = options.WithAIFunctionTools(functions, disableBuiltInTools: false);

        // Assert
        result.DisallowedTools.Should().BeEmpty();
    }

    [Fact]
    public void WithAIFunctionTools_WithCustomServerName_ShouldUseCustomName()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();
        var function = AIFunctionFactory.Create(() => "test", name: "Test");
        var functions = new[] { function };

        // Act
        var result = options.WithAIFunctionTools(functions, "custom-server");

        // Assert
        result.McpServers.Should().ContainKey("custom-server");
    }

    [Fact]
    public void WithConversationMode_WithNullOptions_ShouldThrow()
    {
        // Arrange
        ClaudeCodeChatClientOptions? options = null;

        // Act
        Action act = () => options!.WithConversationMode(ConversationMode.AppManaged);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void WithConversationMode_ShouldSetMode()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        var result = options.WithConversationMode(ConversationMode.ClaudeCodeManaged);

        // Assert
        result.Should().BeSameAs(options);
        result.ConversationMode.Should().Be(ConversationMode.ClaudeCodeManaged);
    }

    [Fact]
    public void WithModel_WithNullOptions_ShouldThrow()
    {
        // Arrange
        ClaudeCodeChatClientOptions? options = null;

        // Act
        Action act = () => options!.WithModel("claude-opus-4");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void WithModel_WithNullModel_ShouldThrow()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        Action act = () => options.WithModel(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("model");
    }

    [Fact]
    public void WithModel_WithEmptyModel_ShouldThrow()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        Action act = () => options.WithModel("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("model");
    }

    [Fact]
    public void WithModel_WithValidModel_ShouldSetModel()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        var result = options.WithModel("claude-opus-4");

        // Assert
        result.Should().BeSameAs(options);
        result.Model.Should().Be("claude-opus-4");
    }

    [Fact]
    public void WithPermissionMode_ShouldSetMode()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        var result = options.WithPermissionMode(PermissionMode.AcceptEdits);

        // Assert
        result.PermissionMode.Should().Be(PermissionMode.AcceptEdits);
    }

    [Fact]
    public void WithSystemPrompt_ShouldSetPrompt()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        var result = options.WithSystemPrompt("You are helpful");

        // Assert
        result.SystemPrompt.Should().Be("You are helpful");
    }

    [Fact]
    public void WithMaxTurns_WithZero_ShouldThrow()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        Action act = () => options.WithMaxTurns(0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxTurns");
    }

    [Fact]
    public void WithMaxTurns_WithNegative_ShouldThrow()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        Action act = () => options.WithMaxTurns(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxTurns");
    }

    [Fact]
    public void WithMaxTurns_WithPositive_ShouldSetValue()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        var result = options.WithMaxTurns(10);

        // Assert
        result.MaxTurns.Should().Be(10);
    }

    [Fact]
    public void WithWorkingDirectory_WithNull_ShouldThrow()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        Action act = () => options.WithWorkingDirectory(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("cwd");
    }

    [Fact]
    public void WithWorkingDirectory_WithEmpty_ShouldThrow()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        Action act = () => options.WithWorkingDirectory("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("cwd");
    }

    [Fact]
    public void WithWorkingDirectory_WithValidPath_ShouldSetPath()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        var result = options.WithWorkingDirectory(@"C:\Projects");

        // Assert
        result.Cwd.Should().Be(@"C:\Projects");
    }

    [Fact]
    public void WithPartialMessages_ShouldSetFlag()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        var result = options.WithPartialMessages(true);

        // Assert
        result.IncludePartialMessages.Should().BeTrue();
    }

    [Fact]
    public void WithAllowedTools_WithTools_ShouldAddTools()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        var result = options.WithAllowedTools("Read", "Write", "Bash");

        // Assert
        result.AllowedTools.Should().Contain("Read");
        result.AllowedTools.Should().Contain("Write");
        result.AllowedTools.Should().Contain("Bash");
    }

    [Fact]
    public void WithDisallowedTools_WithTools_ShouldAddTools()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        var result = options.WithDisallowedTools("Bash", "WebFetch");

        // Assert
        result.DisallowedTools.Should().Contain("Bash");
        result.DisallowedTools.Should().Contain("WebFetch");
    }

    [Fact]
    public void WithResumeSession_WithSessionId_ShouldSetSessionAndMode()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        var result = options.WithResumeSession("session-123");

        // Assert
        result.Resume.Should().Be("session-123");
        result.ConversationMode.Should().Be(ConversationMode.ClaudeCodeManaged);
    }

    [Fact]
    public void WithResumeSession_WithNull_ShouldThrow()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();

        // Act
        Action act = () => options.WithResumeSession(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sessionId");
    }

    [Fact]
    public void FluentChaining_ShouldAllowMultipleCalls()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions();
        var function = AIFunctionFactory.Create(() => "test", name: "Test");

        // Act
        var result = options
            .WithModel("claude-opus-4")
            .WithMaxTurns(20)
            .WithPermissionMode(PermissionMode.AcceptEdits)
            .WithAIFunctionTools(new[] { function }, "my-tools")
            .WithConversationMode(ConversationMode.AppManaged)
            .WithPartialMessages(true);

        // Assert
        result.Should().BeSameAs(options);
        result.Model.Should().Be("claude-opus-4");
        result.MaxTurns.Should().Be(20);
        result.PermissionMode.Should().Be(PermissionMode.AcceptEdits);
        result.McpServers.Should().ContainKey("my-tools");
        result.ConversationMode.Should().Be(ConversationMode.AppManaged);
        result.IncludePartialMessages.Should().BeTrue();
    }
}
