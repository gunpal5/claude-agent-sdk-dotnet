using System.Text.Json;
using FluentAssertions;

namespace ClaudeAgentSdk.Tests;

public class MessageParserTests
{
    [Fact]
    public void ParseMessage_WithUserMessage_ShouldParseCorrectly()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("user"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "user",
                content = "Hello, Claude!"
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        message.Should().BeOfType<UserMessage>();
        var userMsg = (UserMessage)message;
        userMsg.ParentToolUseId.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_WithAssistantMessage_ShouldParseTextBlocks()
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
                    new { type = "text", text = "Hello!" },
                    new { type = "text", text = "How can I help?" }
                }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        message.Should().BeOfType<AssistantMessage>();
        var assistantMsg = (AssistantMessage)message;
        assistantMsg.Model.Should().Be("claude-sonnet-4");
        assistantMsg.Content.Should().HaveCount(2);
        assistantMsg.Content[0].Should().BeOfType<TextBlock>();
        ((TextBlock)assistantMsg.Content[0]).Text.Should().Be("Hello!");
    }

    [Fact]
    public void ParseMessage_WithToolUseBlock_ShouldParseCorrectly()
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
                        id = "tool_123",
                        name = "Read",
                        input = new { file_path = "/test.txt" }
                    }
                }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        assistantMsg.Content.Should().HaveCount(1);
        assistantMsg.Content[0].Should().BeOfType<ToolUseBlock>();
        var toolUse = (ToolUseBlock)assistantMsg.Content[0];
        toolUse.Id.Should().Be("tool_123");
        toolUse.Name.Should().Be("Read");
        toolUse.Input.Should().ContainKey("file_path");
    }

    [Fact]
    public void ParseMessage_WithThinkingBlock_ShouldParseCorrectly()
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
                        type = "thinking",
                        thinking = "Let me analyze this...",
                        signature = "sig_abc"
                    }
                }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        assistantMsg.Content[0].Should().BeOfType<ThinkingBlock>();
        var thinking = (ThinkingBlock)assistantMsg.Content[0];
        thinking.Thinking.Should().Be("Let me analyze this...");
        thinking.Signature.Should().Be("sig_abc");
    }

    [Fact]
    public void ParseMessage_WithResultMessage_ShouldParseCorrectly()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("result"),
            ["subtype"] = JsonSerializer.SerializeToElement("success"),
            ["duration_ms"] = JsonSerializer.SerializeToElement(1500),
            ["duration_api_ms"] = JsonSerializer.SerializeToElement(1200),
            ["is_error"] = JsonSerializer.SerializeToElement(false),
            ["num_turns"] = JsonSerializer.SerializeToElement(3),
            ["session_id"] = JsonSerializer.SerializeToElement("session_123"),
            ["total_cost_usd"] = JsonSerializer.SerializeToElement(0.0123),
            ["usage"] = JsonSerializer.SerializeToElement(new
            {
                input_tokens = 100,
                output_tokens = 50
            })
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        message.Should().BeOfType<ResultMessage>();
        var resultMsg = (ResultMessage)message;
        resultMsg.Subtype.Should().Be("success");
        resultMsg.DurationMs.Should().Be(1500);
        resultMsg.DurationApiMs.Should().Be(1200);
        resultMsg.IsError.Should().BeFalse();
        resultMsg.NumTurns.Should().Be(3);
        resultMsg.SessionId.Should().Be("session_123");
        resultMsg.TotalCostUsd.Should().Be(0.0123);
        resultMsg.Usage.Should().NotBeNull();
    }

    [Fact]
    public void ParseMessage_WithSystemMessage_ShouldParseCorrectly()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("system"),
            ["subtype"] = JsonSerializer.SerializeToElement("notification"),
            ["data"] = JsonSerializer.SerializeToElement(new
            {
                message = "Tool executed successfully",
                level = "info"
            })
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        message.Should().BeOfType<SystemMessage>();
        var sysMsg = (SystemMessage)message;
        sysMsg.Subtype.Should().Be("notification");
        sysMsg.Data.Should().NotBeNull();
    }

    [Fact]
    public void ParseMessage_WithStreamEvent_ShouldParseCorrectly()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("stream"),
            ["uuid"] = JsonSerializer.SerializeToElement("uuid_123"),
            ["session_id"] = JsonSerializer.SerializeToElement("session_456"),
            ["event"] = JsonSerializer.SerializeToElement(new
            {
                type = "content_block_delta",
                delta = new { type = "text_delta", text = "Hello" }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        message.Should().BeOfType<StreamEvent>();
        var streamEvent = (StreamEvent)message;
        streamEvent.Uuid.Should().Be("uuid_123");
        streamEvent.SessionId.Should().Be("session_456");
        streamEvent.Event.Should().NotBeNull();
    }

    [Fact]
    public void ParseMessage_WithMissingType_ShouldThrowException()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["message"] = JsonSerializer.SerializeToElement(new { content = "test" })
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => MessageParser.ParseMessage(data));
    }

    [Fact]
    public void ParseMessage_WithUnknownType_ShouldThrowException()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("unknown_type")
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => MessageParser.ParseMessage(data));
    }

    [Fact]
    public void ParseMessage_WithToolResultBlock_StringContent_ShouldParseCorrectly()
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
                        type = "tool_result",
                        tool_use_id = "tool_123",
                        content = "File contents here",
                        is_error = false
                    }
                }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        assistantMsg.Content[0].Should().BeOfType<ToolResultBlock>();
        var toolResult = (ToolResultBlock)assistantMsg.Content[0];
        toolResult.ToolUseId.Should().Be("tool_123");
        toolResult.Content.Should().Be("File contents here");
        toolResult.IsError.Should().BeFalse();
    }

    [Fact]
    public void ParseMessage_WithToolResultBlock_ArrayContent_ShouldParseCorrectly()
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
                        type = "tool_result",
                        tool_use_id = "tool_123",
                        content = new[]
                        {
                            new { type = "text", text = "Result 1" },
                            new { type = "text", text = "Result 2" }
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
        var toolResult = (ToolResultBlock)assistantMsg.Content[0];
        toolResult.Content.Should().NotBeNull();
    }

    [Fact]
    public void ParseMessage_WithMixedContentBlocks_ShouldParseAll()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content = new object[]
                {
                    new { type = "text", text = "Let me help" },
                    new { type = "thinking", thinking = "Analyzing...", signature = "sig1" },
                    new { type = "tool_use", id = "t1", name = "Read", input = new { path = "test.txt" } },
                    new { type = "tool_result", tool_use_id = "t1", content = "file content" }
                }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var message = MessageParser.ParseMessage(data);

        // Assert
        var assistantMsg = (AssistantMessage)message;
        assistantMsg.Content.Should().HaveCount(4);
        assistantMsg.Content[0].Should().BeOfType<TextBlock>();
        assistantMsg.Content[1].Should().BeOfType<ThinkingBlock>();
        assistantMsg.Content[2].Should().BeOfType<ToolUseBlock>();
        assistantMsg.Content[3].Should().BeOfType<ToolResultBlock>();
    }
}
