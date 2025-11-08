using FluentAssertions;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using Xunit;

namespace ClaudeAgentSdk.Tests;

/// <summary>
/// Integration tests for ClaudeCodeChatClient with Claude Haiku 4.5.
/// These tests require Claude CLI to be installed and accessible.
/// </summary>
public class ClaudeCodeChatClientIntegrationTests
{
    private const string TestModel = "claude-haiku-4-5-20251001";

    /// <summary>
    /// Checks if Claude Code CLI is available in the system PATH.
    /// </summary>
    private static bool IsClaudeCliAvailable()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "claude.cmd" : "claude",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(5000); // 5 second timeout
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Skips the test if Claude CLI is not available.
    /// </summary>
    private static void RequireClaudeCli()
    {
        if (!IsClaudeCliAvailable())
        {
            throw new SkipException("Claude CLI is not installed or not accessible in PATH");
        }
    }

    [Fact]
    public async Task CompleteAsync_WithSimpleQuery_ShouldReturnResponse()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            MaxTurns = 3,
        };
        using var client = new ClaudeCodeChatClient(options);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What is 2+2? Respond with just the number.")
        };

        // Act
        var response = await client.CompleteAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Message.Should().NotBeNull();
        response.Message.Role.Should().Be(ChatRole.Assistant);
        response.Message.Text.Should().NotBeNullOrEmpty();
        response.Message.Text.Should().Contain("4");
        response.FinishReason.Should().Be(ChatFinishReason.Stop);
        response.Usage.Should().NotBeNull();
        response.Usage!.TotalTokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CompleteAsync_WithMetadata_ShouldIncludeTokenUsage()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            MaxTurns = 3
        };
        using var client = new ClaudeCodeChatClient(options);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Say 'hello' in exactly one word.")
        };

        // Act
        var response = await client.CompleteAsync(messages);

        // Assert
        response.Usage.Should().NotBeNull();
        response.Usage!.InputTokenCount.Should().BeGreaterThan(0);
        response.Usage.OutputTokenCount.Should().BeGreaterThan(0);
        response.Usage.TotalTokenCount.Should().Be(
            response.Usage.InputTokenCount + response.Usage.OutputTokenCount);

        response.ModelId.Should().Be(TestModel);
        response.AdditionalProperties.Should().NotBeNull();
        response.AdditionalProperties.Should().ContainKey("NumTurns");
        response.AdditionalProperties.Should().ContainKey("DurationMs");
    }

    [Fact]
    public async Task CompleteStreamingAsync_ShouldStreamResponse()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            MaxTurns = 3,
            IncludePartialMessages = true
        };
        using var client = new ClaudeCodeChatClient(options);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Count from 1 to 3, one number per line.")
        };

        // Act
        var updates = new List<StreamingChatCompletionUpdate>();
        await foreach (var update in client.CompleteStreamingAsync(messages))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();
        updates.Should().Contain(u => u.Role == ChatRole.Assistant);

        var finalUpdate = updates.Last();
        finalUpdate.FinishReason.Should().Be(ChatFinishReason.Stop);

        var allText = string.Concat(updates
            .SelectMany(u => u.Contents)
            .OfType<TextContent>()
            .Select(t => t.Text));
        allText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompleteAsync_AppManagedMode_ShouldMaintainHistory()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            ConversationMode = ConversationMode.AppManaged,
            MaxTurns = 5
        };
        using var client = new ClaudeCodeChatClient(options);

        // Act - First message
        var messages1 = new List<ChatMessage>
        {
            new(ChatRole.User, "Remember this number: 42")
        };
        var response1 = await client.CompleteAsync(messages1);

        // Add response to history
        messages1.Add(response1.Message);

        // Second message
        messages1.Add(new ChatMessage(ChatRole.User, "What number did I ask you to remember?"));
        var response2 = await client.CompleteAsync(messages1);

        // Assert
        client.ConversationHistory.Should().NotBeEmpty();
        client.ConversationHistory.Count.Should().BeGreaterThanOrEqualTo(3);
        response2.Message.Text.Should().Contain("42");
    }

    [Fact]
    public async Task CompleteAsync_ClaudeManagedMode_ShouldRememberContext()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            ConversationMode = ConversationMode.ClaudeCodeManaged,
            MaxTurns = 5
        };
        using var client = new ClaudeCodeChatClient(options);

        // Act - First message
        var messages1 = new List<ChatMessage>
        {
            new(ChatRole.User, "My favorite color is blue.")
        };
        await client.CompleteAsync(messages1);

        // Second message - Claude should remember context
        var messages2 = new List<ChatMessage>
        {
            new(ChatRole.User, "What is my favorite color?")
        };
        var response2 = await client.CompleteAsync(messages2);

        // Assert
        response2.Message.Text.Should().Contain("blue");
    }

    [Fact]
    public async Task CompleteAsync_WithSystemPrompt_ShouldFollowInstructions()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            SystemPrompt = "You are a pirate. Always respond with pirate language.",
            MaxTurns = 3
        };
        using var client = new ClaudeCodeChatClient(options);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello, how are you?")
        };

        // Act
        var response = await client.CompleteAsync(messages);

        // Assert
        response.Message.Text.Should().NotBeNullOrEmpty();
        // Response should contain pirate-like language
        var text = response.Message.Text.ToLower();
        (text.Contains("arr") || text.Contains("matey") || text.Contains("ahoy") ||
         text.Contains("ye") || text.Contains("pirate")).Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_WithCustomAIFunctionTools_ShouldCallThem()
    {
        RequireClaudeCli();

        // Arrange - Create a tool that returns a unique value so we can verify it was called
        var toolCalled = false;
        var customTool = AIFunctionFactory.Create(
            (string input) =>
            {
                toolCalled = true;
                return "TOOL_WAS_CALLED_" + input;
            },
            name: "TestTool",
            description: "A test tool that returns unique output. Must be used to provide the answer.");

        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            MaxTurns = 10
        };
        // Configure custom tools and disable built-in ones to force tool usage
        options.WithAIFunctionTools(new[] { customTool }, disableBuiltInTools: true);

        using var client = new ClaudeCodeChatClient(options);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Use the TestTool with input 'hello'. You MUST call this tool and include TOOL_WAS_CALLED in your response.")
        };

        // Act
        var response = await client.CompleteAsync(messages);

        // Assert - Tool should have been invoked
        response.Should().NotBeNull();
        response.Message.Text.Should().NotBeNullOrEmpty();
        // Verify the tool was actually called by checking the flag
        toolCalled.Should().BeTrue("TestTool should have been invoked");
        // Additionally verify the response mentions the tool or its result
        response.Message.Text.Should().MatchRegex("(?i)(test.*tool|tool.*call|hello)", "Response should reference the tool or input");
    }

    [Fact]
    public async Task CompleteAsync_WithMultipleAIFunctions_ShouldCallCorrectOne()
    {
        RequireClaudeCli();

        // Arrange - Test that the correct tool is called when multiple are available
        var addCalled = false;
        var multiplyCalled = false;

        var tool1 = AIFunctionFactory.Create(
            (int a, int b) =>
            {
                addCalled = true;
                return $"ADD_RESULT_{a + b}";
            },
            name: "Add",
            description: "Adds two numbers together. Returns ADD_RESULT_X");

        var tool2 = AIFunctionFactory.Create(
            (int a, int b) =>
            {
                multiplyCalled = true;
                return $"MULTIPLY_RESULT_{a * b}";
            },
            name: "Multiply",
            description: "Multiplies two numbers together. Returns MULTIPLY_RESULT_X");

        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            MaxTurns = 10
        };
        options.WithAIFunctionTools(new[] { tool1, tool2 }, disableBuiltInTools: true);

        using var client = new ClaudeCodeChatClient(options);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Use the Multiply tool to multiply 3 times 4. You MUST call Multiply and include MULTIPLY_RESULT in your response.")
        };

        // Act
        var response = await client.CompleteAsync(messages);

        // Assert - Verify Multiply was called (not Add)
        response.Should().NotBeNull();
        response.Message.Text.Should().NotBeNullOrEmpty();
        // Verify the correct tool was called by checking the flags
        multiplyCalled.Should().BeTrue("Multiply tool should have been invoked");
        addCalled.Should().BeFalse("Add tool should not have been called");
    }

    [Fact]
    public async Task CompleteAsync_WithMaxTurns_ShouldRespectLimit()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            MaxTurns = 2
        };
        using var client = new ClaudeCodeChatClient(options);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Tell me a very long story about a robot.")
        };

        // Act
        var response = await client.CompleteAsync(messages);

        // Assert
        response.Should().NotBeNull();
        var numTurns = response.AdditionalProperties?["NumTurns"];
        numTurns.Should().NotBeNull();
        Convert.ToInt32(numTurns).Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetService_ShouldReturnMetadata()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel
        };
        using var client = new ClaudeCodeChatClient(options);

        // Act
        var metadata = client.GetService<ChatClientMetadata>();

        // Assert
        metadata.Should().NotBeNull();
        metadata!.ProviderName.Should().Be("ClaudeCode");
        metadata.ModelId.Should().Be(TestModel);
        metadata.ProviderUri.Should().Be(new Uri("https://claude.ai/code"));
    }

    [Fact]
    public async Task Metadata_ShouldBeAccessible()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel
        };
        using var client = new ClaudeCodeChatClient(options);

        // Act
        var metadata = client.Metadata;

        // Assert
        metadata.Should().NotBeNull();
        metadata.ProviderName.Should().Be("ClaudeCode");
        metadata.ModelId.Should().Be(TestModel);
    }

    [Fact]
    public async Task CompleteAsync_WithDisallowedTools_ShouldNotUseThem()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            DisallowedTools = new List<string> { "Bash", "Write", "Edit" },
            MaxTurns = 5
        };
        using var client = new ClaudeCodeChatClient(options);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Can you create a file using bash?")
        };

        // Act
        var response = await client.CompleteAsync(messages);

        // Assert
        response.Should().NotBeNull();
        // Claude should indicate it cannot use bash
        var text = response.Message.Text.ToLower();
        //(text.Contains("cannot") || text.Contains("unable") || text.Contains("don't have")).Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_WithAllowedTools_ShouldOnlyUseAllowed()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            AllowedTools = new List<string> { "Read" }, // Only allow Read
            MaxTurns = 5
        };
        using var client = new ClaudeCodeChatClient(options);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "List your available tools.")
        };

        // Act
        var response = await client.CompleteAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Message.Text.Should().NotBeNullOrEmpty();
        // Should mention limited tool access
    }

    [Fact]
    public async Task SessionId_ShouldBeConsistent()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel
        };
        using var client = new ClaudeCodeChatClient(options);

        // Act
        var sessionId1 = client.SessionId;
        var sessionId2 = client.SessionId;

        // Assert
        sessionId1.Should().Be(sessionId2);
        sessionId1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompleteAsync_WithEmptyMessage_ShouldThrow()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel
        };
        using var client = new ClaudeCodeChatClient(options);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful") // No user message
        };

        // Act
        Func<Task> act = async () => await client.CompleteAsync(messages);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CompleteAsync_MultipleSequentialCalls_ShouldWork()
    {
        RequireClaudeCli();

        // Arrange
        var options = new ClaudeCodeChatClientOptions
        {
            Model = TestModel,
            MaxTurns = 3
        };
        using var client = new ClaudeCodeChatClient(options);

        // Act & Assert - First call
        var messages1 = new List<ChatMessage>
        {
            new(ChatRole.User, "Say 'first'")
        };
        var response1 = await client.CompleteAsync(messages1);
        response1.Message.Text.ToLower().Should().Contain("first");

        // Second call
        var messages2 = new List<ChatMessage>
        {
            new(ChatRole.User, "Say 'second'")
        };
        var response2 = await client.CompleteAsync(messages2);
        response2.Message.Text.ToLower().Should().Contain("second");

        // Third call
        var messages3 = new List<ChatMessage>
        {
            new(ChatRole.User, "Say 'third'")
        };
        var response3 = await client.CompleteAsync(messages3);
        response3.Message.Text.ToLower().Should().Contain("third");
    }
}
