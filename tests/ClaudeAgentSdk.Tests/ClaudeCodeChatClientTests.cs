using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace ClaudeAgentSdk.Tests;

/// <summary>
/// Tests for ClaudeCodeChatClient IChatClient implementation.
/// </summary>
public class ClaudeCodeChatClientTests
{
    [Fact]
    public void Constructor_WithDefaultOptions_ShouldCreateClient()
    {
        // Act
        var client = new ClaudeCodeChatClient();

        // Assert
        client.Should().NotBeNull();
        client.Metadata.Should().NotBeNull();
        client.Metadata.ProviderName.Should().Be("ClaudeCode");
        client.Metadata.ModelId.Should().Be("claude-sonnet-4");
        client.SessionId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WithCustomOptions_ShouldUseCustomModel()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = "claude-opus-4"
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Metadata.ModelId.Should().Be("claude-opus-4");
    }

    [Fact]
    public void Metadata_ShouldReturnCorrectProviderInfo()
    {
        // Arrange
        var client = new ClaudeCodeChatClient();

        // Act
        var metadata = client.Metadata;

        // Assert
        metadata.ProviderName.Should().Be("ClaudeCode");
        metadata.ProviderUri.Should().Be(new Uri("https://claude.ai/code"));
        metadata.ModelId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetService_WithChatClientMetadata_ShouldReturnMetadata()
    {
        // Arrange
        var client = new ClaudeCodeChatClient();

        // Act
        var service = client.GetService(typeof(ChatClientMetadata));

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<ChatClientMetadata>();
    }

    [Fact]
    public void ConversationHistory_InAppManagedMode_ShouldBeEmpty()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            ConversationMode = ConversationMode.AppManaged
        };
        var client = new ClaudeCodeChatClient(options);

        // Act
        var history = client.ConversationHistory;

        // Assert
        history.Should().NotBeNull();
        history.Should().BeEmpty();
    }

    [Fact]
    public void SessionId_ShouldBeUnique()
    {
        // Arrange & Act
        var client1 = new ClaudeCodeChatClient();
        var client2 = new ClaudeCodeChatClient();

        // Assert
        client1.SessionId.Should().NotBe(client2.SessionId);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var client = new ClaudeCodeChatClient();

        // Act
        Action act = () => client.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(ConversationMode.AppManaged)]
    [InlineData(ConversationMode.ClaudeCodeManaged)]
    public void Constructor_WithConversationMode_ShouldSetCorrectly(ConversationMode mode)
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            ConversationMode = mode
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_WithMcpServers_ShouldAcceptConfiguration()
    {
        // Arrange
        var mcpServers = new Dictionary<string, object>
        {
            ["test-server"] = new McpSdkServerConfig
            {
                Name = "test",
                Instance = new object()
            }
        };

        var options = new ClaudeCodeChatClientOptions
        {
            McpServers = mcpServers
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_WithDisallowedTools_ShouldAcceptList()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            DisallowedTools = new List<string> { "Read", "Write", "Bash" }
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_WithAllowedTools_ShouldAcceptList()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            AllowedTools = new List<string> { "Read", "Glob" }
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_WithSystemPrompt_ShouldAcceptString()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            SystemPrompt = "You are a helpful assistant."
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_WithSystemPromptPreset_ShouldAcceptPreset()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            SystemPrompt = new SystemPromptPreset
            {
                Preset = "claude_code",
                Append = "Additional instructions."
            }
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_WithMaxTurns_ShouldAcceptValue()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            MaxTurns = 10
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_WithWorkingDirectory_ShouldAcceptPath()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Cwd = @"C:\Projects"
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_WithPermissionMode_ShouldAcceptMode()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            PermissionMode = PermissionMode.AcceptEdits
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_WithIncludePartialMessages_ShouldAcceptBool()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            IncludePartialMessages = true
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_WithResume_ShouldAcceptSessionId()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Resume = "previous-session-id",
            ConversationMode = ConversationMode.ClaudeCodeManaged
        };

        // Act
        var client = new ClaudeCodeChatClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void GetService_WithUnknownType_ShouldReturnNull()
    {
        // Arrange
        var client = new ClaudeCodeChatClient();

        // Act
        var service = client.GetService(typeof(string));

        // Assert
        service.Should().BeNull();
    }

    [Fact]
    public void GetService_WithConversationHistory_InAppManagedMode_ShouldReturnHistory()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            ConversationMode = ConversationMode.AppManaged
        };
        var client = new ClaudeCodeChatClient(options);

        // Act
        var service = client.GetService(typeof(IReadOnlyList<ChatMessage>));

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<IReadOnlyList<ChatMessage>>();
    }

    [Fact]
    public void GetService_WithConversationHistory_InClaudeManagedMode_ShouldReturnNull()
    {
        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            ConversationMode = ConversationMode.ClaudeCodeManaged
        };
        var client = new ClaudeCodeChatClient(options);

        // Act
        var service = client.GetService(typeof(IReadOnlyList<ChatMessage>));

        // Assert
        service.Should().BeNull();
    }
}
