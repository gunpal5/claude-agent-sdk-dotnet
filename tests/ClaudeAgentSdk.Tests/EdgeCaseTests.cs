using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace ClaudeAgentSdk.Tests;

public class EdgeCaseTests
{
    [Fact]
    public void MessageParser_EmptyContent_ShouldHandleGracefully()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("user"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "user",
                content = ""
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        message.Should().BeOfType<UserMessage>();
    }

    [Fact]
    public void MessageParser_VeryLongText_ShouldParse()
    {
        // Arrange
        var longText = new string('x', 100_000);
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content = new[] { new { type = "text", text = longText } }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        var textBlock = (TextBlock)assistantMsg.Content[0];
        textBlock.Text.Should().HaveLength(100_000);
    }

    [Fact]
    public void MessageParser_UnicodeCharacters_ShouldPreserve()
    {
        // Arrange
        var unicodeText = "Hello 世界 🌍 مرحبا привет";
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content = new[] { new { type = "text", text = unicodeText } }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        var textBlock = (TextBlock)assistantMsg.Content[0];
        textBlock.Text.Should().Be(unicodeText);
    }

    [Fact]
    public void MessageParser_SpecialCharacters_ShouldEscape()
    {
        // Arrange
        var specialText = "Line1\nLine2\tTab\"Quote\"\\Backslash";
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content = new[] { new { type = "text", text = specialText } }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        var textBlock = (TextBlock)assistantMsg.Content[0];
        textBlock.Text.Should().Contain("\n");
        textBlock.Text.Should().Contain("\t");
        textBlock.Text.Should().Contain("\"");
    }

    [Fact]
    public void MessageParser_EmptyContentArray_ShouldHandle()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content = Array.Empty<object>()
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        assistantMsg.Content.Should().BeEmpty();
    }

    [Fact]
    public void MessageParser_NullValues_ShouldHandleGracefully()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("result"),
            ["subtype"] = JsonSerializer.SerializeToElement("success"),
            ["duration_ms"] = JsonSerializer.SerializeToElement(1000),
            ["duration_api_ms"] = JsonSerializer.SerializeToElement(900),
            ["is_error"] = JsonSerializer.SerializeToElement(false),
            ["num_turns"] = JsonSerializer.SerializeToElement(1),
            ["session_id"] = JsonSerializer.SerializeToElement("session_1"),
            ["total_cost_usd"] = JsonSerializer.SerializeToElement((double?)null),
            ["usage"] = JsonSerializer.SerializeToElement((object?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var resultMsg = (ResultMessage)message;
        resultMsg.TotalCostUsd.Should().BeNull();
        resultMsg.Usage.Should().BeNull();
    }

    [Fact]
    public void MessageParser_ZeroCost_ShouldParse()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("result"),
            ["subtype"] = JsonSerializer.SerializeToElement("success"),
            ["duration_ms"] = JsonSerializer.SerializeToElement(0),
            ["duration_api_ms"] = JsonSerializer.SerializeToElement(0),
            ["is_error"] = JsonSerializer.SerializeToElement(false),
            ["num_turns"] = JsonSerializer.SerializeToElement(0),
            ["session_id"] = JsonSerializer.SerializeToElement("session_1"),
            ["total_cost_usd"] = JsonSerializer.SerializeToElement(0.0)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var resultMsg = (ResultMessage)message;
        resultMsg.TotalCostUsd.Should().Be(0.0);
        resultMsg.NumTurns.Should().Be(0);
    }

    [Fact]
    public void MessageParser_VeryHighCost_ShouldParse()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("result"),
            ["subtype"] = JsonSerializer.SerializeToElement("success"),
            ["duration_ms"] = JsonSerializer.SerializeToElement(1000),
            ["duration_api_ms"] = JsonSerializer.SerializeToElement(900),
            ["is_error"] = JsonSerializer.SerializeToElement(false),
            ["num_turns"] = JsonSerializer.SerializeToElement(1),
            ["session_id"] = JsonSerializer.SerializeToElement("session_1"),
            ["total_cost_usd"] = JsonSerializer.SerializeToElement(999999.99)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var resultMsg = (ResultMessage)message;
        resultMsg.TotalCostUsd.Should().Be(999999.99);
    }

    [Fact]
    public void MessageParser_NegativeDuration_ShouldParse()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("result"),
            ["subtype"] = JsonSerializer.SerializeToElement("success"),
            ["duration_ms"] = JsonSerializer.SerializeToElement(-100),
            ["duration_api_ms"] = JsonSerializer.SerializeToElement(-50),
            ["is_error"] = JsonSerializer.SerializeToElement(false),
            ["num_turns"] = JsonSerializer.SerializeToElement(1),
            ["session_id"] = JsonSerializer.SerializeToElement("session_1")
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var resultMsg = (ResultMessage)message;
        resultMsg.DurationMs.Should().Be(-100);
        resultMsg.DurationApiMs.Should().Be(-50);
    }

    [Fact]
    public void ClaudeAgentOptions_WithNullSystemPrompt_ShouldBeAllowed()
    {
        // Act
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = null
        };

        // Assert
        options.SystemPrompt.Should().BeNull();
    }

    [Fact]
    public void ClaudeAgentOptions_WithEmptyToolsList_ShouldBeAllowed()
    {
        // Act
        var options = new ClaudeAgentOptions
        {
            AllowedTools = new List<string>()
        };

        // Assert
        options.AllowedTools.Should().BeEmpty();
    }

    [Fact]
    public void ClaudeAgentOptions_WithDuplicateTools_ShouldAllow()
    {
        // Act
        var options = new ClaudeAgentOptions
        {
            AllowedTools = new List<string> { "Read", "Read", "Write" }
        };

        // Assert
        options.AllowedTools.Should().HaveCount(3);
        options.AllowedTools.Count(t => t == "Read").Should().Be(2);
    }

    [Fact]
    public void ClaudeAgentOptions_WithVeryLongSystemPrompt_ShouldAllow()
    {
        // Arrange
        var longPrompt = new string('x', 100_000);

        // Act
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = longPrompt
        };

        // Assert
        options.SystemPrompt.Should().Be(longPrompt);
    }

    [Fact]
    public void ClaudeAgentOptions_WithSpecialCharactersInPaths_ShouldAllow()
    {
        // Act
        var options = new ClaudeAgentOptions
        {
            Cwd = "/path/with spaces/and-dashes/under_scores"
        };

        // Assert
        options.Cwd.Should().Contain(" ");
        options.Cwd.Should().Contain("-");
        options.Cwd.Should().Contain("_");
    }

    [Fact]
    public void ClaudeAgentOptions_WithMaxIntMaxTurns_ShouldAllow()
    {
        // Act
        var options = new ClaudeAgentOptions
        {
            MaxTurns = int.MaxValue
        };

        // Assert
        options.MaxTurns.Should().Be(int.MaxValue);
    }

    [Fact]
    public void ClaudeAgentOptions_WithZeroMaxTurns_ShouldAllow()
    {
        // Act
        var options = new ClaudeAgentOptions
        {
            MaxTurns = 0
        };

        // Assert
        options.MaxTurns.Should().Be(0);
    }

    [Fact]
    public void MessageParser_ToolUseWithEmptyInput_ShouldParse()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content = new[]
                {
                    new
                    {
                        type = "tool_use",
                        id = "tool_1",
                        name = "NoArgTool",
                        input = new { }
                    }
                }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        var toolUse = (ToolUseBlock)assistantMsg.Content[0];
        toolUse.Input.Should().BeEmpty();
    }

    [Fact]
    public void MessageParser_ToolUseWithNestedObjects_ShouldParse()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content = new[]
                {
                    new
                    {
                        type = "tool_use",
                        id = "tool_1",
                        name = "ComplexTool",
                        input = new
                        {
                            nested = new
                            {
                                deeply = new
                                {
                                    value = 123
                                }
                            },
                            array = new[] { 1, 2, 3 }
                        }
                    }
                }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        var toolUse = (ToolUseBlock)assistantMsg.Content[0];
        toolUse.Input.Should().ContainKey("nested");
    }

    [Fact]
    public async Task Transport_RapidConnectDisconnect_ShouldHandle()
    {
        // Arrange & Act
        for (int i = 0; i < 100; i++)
        {
            var transport = new MockTransport();
            await transport.ConnectAsync();
            await transport.DisposeAsync();
        }

        // Assert - should not throw
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Transport_WriteAfterDispose_ShouldThrow()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();
        await transport.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<CliConnectionException>(
            async () => await transport.WriteAsync("test"));
    }

    [Fact]
    public void MessageParser_HundredsOfContentBlocks_ShouldParse()
    {
        // Arrange
        var content = Enumerable.Range(0, 500)
            .Select(i => new { type = "text", text = $"Block {i}" })
            .ToArray();

        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        assistantMsg.Content.Should().HaveCount(500);
    }

    [Fact]
    public void MessageParser_WithWhitespaceOnlyText_ShouldPreserve()
    {
        // Arrange
        var whitespace = "   \t\n\r   ";
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content = new[] { new { type = "text", text = whitespace } }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        var textBlock = (TextBlock)assistantMsg.Content[0];
        textBlock.Text.Should().Be(whitespace);
    }
}

public class BoundaryTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    public void ResultMessage_WithBoundaryDurations_ShouldParse(int duration)
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("result"),
            ["subtype"] = JsonSerializer.SerializeToElement("success"),
            ["duration_ms"] = JsonSerializer.SerializeToElement(duration),
            ["duration_api_ms"] = JsonSerializer.SerializeToElement(duration),
            ["is_error"] = JsonSerializer.SerializeToElement(false),
            ["num_turns"] = JsonSerializer.SerializeToElement(1),
            ["session_id"] = JsonSerializer.SerializeToElement("session_1")
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var resultMsg = (ResultMessage)message;
        resultMsg.DurationMs.Should().Be(duration);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.0001)]
    [InlineData(1.0)]
    [InlineData(999999.99)]
    [InlineData(double.MaxValue)]
    public void ResultMessage_WithBoundaryCosts_ShouldParse(double cost)
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("result"),
            ["subtype"] = JsonSerializer.SerializeToElement("success"),
            ["duration_ms"] = JsonSerializer.SerializeToElement(1000),
            ["duration_api_ms"] = JsonSerializer.SerializeToElement(900),
            ["is_error"] = JsonSerializer.SerializeToElement(false),
            ["num_turns"] = JsonSerializer.SerializeToElement(1),
            ["session_id"] = JsonSerializer.SerializeToElement("session_1"),
            ["total_cost_usd"] = JsonSerializer.SerializeToElement(cost)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var resultMsg = (ResultMessage)message;
        resultMsg.TotalCostUsd.Should().Be(cost);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("session_with_underscores")]
    [InlineData("session-with-dashes")]
    [InlineData("123456789")]
    [InlineData("very_long_session_id_that_goes_on_and_on_and_on")]
    public void ResultMessage_WithVariousSessionIds_ShouldParse(string sessionId)
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("result"),
            ["subtype"] = JsonSerializer.SerializeToElement("success"),
            ["duration_ms"] = JsonSerializer.SerializeToElement(1000),
            ["duration_api_ms"] = JsonSerializer.SerializeToElement(900),
            ["is_error"] = JsonSerializer.SerializeToElement(false),
            ["num_turns"] = JsonSerializer.SerializeToElement(1),
            ["session_id"] = JsonSerializer.SerializeToElement(sessionId)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var resultMsg = (ResultMessage)message;
        resultMsg.SessionId.Should().Be(sessionId);
    }
}
