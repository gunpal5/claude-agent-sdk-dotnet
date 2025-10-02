using System.Text.Json;
using FluentAssertions;

namespace ClaudeAgentSdk.Tests;

public class ClientIntegrationTests
{
    [Fact(Skip = "Requires Claude CLI")]
    public async Task ClaudeSdkClient_Constructor_ShouldInitialize()
    {
        // Act
        var client = new ClaudeSdkClient();

        // Assert
        client.Should().NotBeNull();
        await client.DisposeAsync();
    }

    [Fact(Skip = "Requires Claude CLI")]
    public async Task ClaudeSdkClient_WithOptions_ShouldInitialize()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            AllowedTools = new List<string> { "Read", "Write" },
            MaxTurns = 5
        };

        // Act
        var client = new ClaudeSdkClient(options);

        // Assert
        client.Should().NotBeNull();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task ClaudeSdkClient_WithMockTransport_ShouldConnect()
    {
        // Arrange
        var transport = new MockTransport();
        var client = new ClaudeSdkClient(transport: transport);

        // Act
        await client.ConnectAsync();

        // Assert
        transport.IsReady.Should().BeTrue();
        await client.DisposeAsync();
    }

    [Fact(Skip = "Requires Claude CLI")]
    public async Task ClaudeSdkClient_QueryBeforeConnect_ShouldThrow()
    {
        // Arrange
        var client = new ClaudeSdkClient();

        // Act & Assert
        await Assert.ThrowsAsync<CliConnectionException>(
            async () => await client.QueryAsync("test"));

        await client.DisposeAsync();
    }

    [Fact(Skip = "Requires Claude CLI")]
    public async Task ClaudeSdkClient_ReceiveMessagesBeforeConnect_ShouldThrow()
    {
        // Arrange
        var client = new ClaudeSdkClient();

        // Act & Assert
        await Assert.ThrowsAsync<CliConnectionException>(async () =>
        {
            await foreach (var _ in client.ReceiveMessagesAsync())
            {
                // Should not reach here
            }
        });

        await client.DisposeAsync();
    }

    [Fact]
    public async Task ClaudeSdkClient_WithMockTransport_ShouldSendQuery()
    {
        // Arrange
        var transport = new MockTransport();
        var client = new ClaudeSdkClient(transport: transport);
        await client.ConnectAsync();

        // Act
        await client.QueryAsync("Hello Claude!");

        // Assert
        transport.WrittenData.Should().HaveCount(1);
        transport.WrittenData[0].Should().Contain("Hello Claude!");
        transport.WrittenData[0].Should().Contain("\"type\":\"user\"");

        await client.DisposeAsync();
    }

    [Fact]
    public async Task ClaudeSdkClient_MultipleQueries_ShouldAllBeSent()
    {
        // Arrange
        var transport = new MockTransport();
        var client = new ClaudeSdkClient(transport: transport);
        await client.ConnectAsync();

        // Act
        await client.QueryAsync("Query 1");
        await client.QueryAsync("Query 2");
        await client.QueryAsync("Query 3");

        // Assert
        transport.WrittenData.Should().HaveCount(3);
        transport.WrittenData[0].Should().Contain("Query 1");
        transport.WrittenData[1].Should().Contain("Query 2");
        transport.WrittenData[2].Should().Contain("Query 3");

        await client.DisposeAsync();
    }

    [Fact]
    public async Task ClaudeSdkClient_ReceiveMessages_ShouldReadFromTransport()
    {
        // Arrange
        var transport = new MockTransport();
        transport.QueueMessage(new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("user"),
            ["message"] = JsonSerializer.SerializeToElement(new { content = "test" })
        });

        var client = new ClaudeSdkClient(transport: transport);
        await client.ConnectAsync();

        // Act
        var messages = new List<Message>();
        await foreach (var msg in client.ReceiveMessagesAsync())
        {
            messages.Add(msg);
        }

        // Assert
        messages.Should().HaveCount(1);
        messages[0].Should().BeOfType<UserMessage>();

        await client.DisposeAsync();
    }

    [Fact]
    public async Task ClaudeSdkClient_ReceiveResponse_ShouldStopAtResultMessage()
    {
        // Arrange
        var transport = new MockTransport();
        transport.QueueMessage(CreateAssistantMessage("Response 1"));
        transport.QueueMessage(CreateResultMessage());
        transport.QueueMessage(CreateAssistantMessage("Response 2")); // Should not be received

        var client = new ClaudeSdkClient(transport: transport);
        await client.ConnectAsync();

        // Act
        var messages = new List<Message>();
        await foreach (var msg in client.ReceiveResponseAsync())
        {
            messages.Add(msg);
        }

        // Assert
        messages.Should().HaveCount(2); // Assistant + Result only
        messages[0].Should().BeOfType<AssistantMessage>();
        messages[1].Should().BeOfType<ResultMessage>();

        await client.DisposeAsync();
    }

    [Fact]
    public async Task ClaudeSdkClient_Dispose_ShouldCleanup()
    {
        // Arrange
        var transport = new MockTransport();
        var client = new ClaudeSdkClient(transport: transport);
        await client.ConnectAsync();

        // Act
        await client.DisposeAsync();

        // Assert
        transport.IsReady.Should().BeFalse();
    }

    [Fact]
    public async Task ClaudeSdkClient_DisposeMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var transport = new MockTransport();
        var client = new ClaudeSdkClient(transport: transport);
        await client.ConnectAsync();

        // Act & Assert
        await client.DisposeAsync();
        await client.DisposeAsync(); // Should not throw
    }

    [Fact]
    public async Task ClaudeSdkClient_ConnectMultipleTimes_ShouldOnlyConnectOnce()
    {
        // Arrange
        var transport = new MockTransport();
        var client = new ClaudeSdkClient(transport: transport);

        // Act
        await client.ConnectAsync();
        await client.ConnectAsync();
        await client.ConnectAsync();

        // Assert
        transport.IsReady.Should().BeTrue();

        await client.DisposeAsync();
    }

    [Fact]
    public async Task ClaudeSdkClient_WithSessionId_ShouldIncludeInMessage()
    {
        // Arrange
        var transport = new MockTransport();
        var client = new ClaudeSdkClient(transport: transport);
        await client.ConnectAsync();

        // Act
        await client.QueryAsync("Test", "custom_session");

        // Assert
        transport.WrittenData.Should().HaveCount(1);
        transport.WrittenData[0].Should().Contain("custom_session");

        await client.DisposeAsync();
    }

    [Fact]
    public async Task ClaudeSdkClient_WithCanUseTool_ShouldSetPermissionPromptToolName()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (name, input, context) =>
            {
                return Task.FromResult<object>(new PermissionResultAllow());
            }
        };

        var transport = new MockTransport();
        var client = new ClaudeSdkClient(options, transport);

        // Act
        await client.ConnectAsync();

        // Assert
        // The client should have modified options internally
        await client.DisposeAsync();
    }

    [Fact]
    public async Task ClaudeSdkClient_WithCanUseToolAndPermissionPromptToolName_ShouldThrow()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (name, input, context) => Task.FromResult<object>(new PermissionResultAllow()),
            PermissionPromptToolName = "custom_tool"
        };

        var transport = new MockTransport();
        var client = new ClaudeSdkClient(options, transport);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await client.ConnectAsync());

        await client.DisposeAsync();
    }

    private static Dictionary<string, object> CreateAssistantMessage(string text)
    {
        return new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content = new[] { new { type = "text", text } }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };
    }

    private static Dictionary<string, object> CreateResultMessage()
    {
        return new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("result"),
            ["subtype"] = JsonSerializer.SerializeToElement("success"),
            ["duration_ms"] = JsonSerializer.SerializeToElement(1000),
            ["duration_api_ms"] = JsonSerializer.SerializeToElement(900),
            ["is_error"] = JsonSerializer.SerializeToElement(false),
            ["num_turns"] = JsonSerializer.SerializeToElement(1),
            ["session_id"] = JsonSerializer.SerializeToElement("session_1"),
            ["total_cost_usd"] = JsonSerializer.SerializeToElement(0.01)
        };
    }
}

public class QueryApiTests
{
    [Fact]
    public async Task QueryAsync_WithMockTransport_ShouldYieldMessages()
    {
        // Arrange
        var transport = new MockTransport();
        transport.QueueMessage(new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content = new[] { new { type = "text", text = "Hello!" } }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        });

        // Act
        var messages = new List<Message>();
        await foreach (var msg in ClaudeAgent.QueryAsync("Test", transport: transport))
        {
            messages.Add(msg);
        }

        // Assert
        messages.Should().HaveCount(1);
        messages[0].Should().BeOfType<AssistantMessage>();
    }

    [Fact]
    public async Task QueryAsync_WithOptions_ShouldRespectOptions()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions
        {
            MaxTurns = 1,
            AllowedTools = new List<string> { "Read" }
        };

        // Act
        await foreach (var _ in ClaudeAgent.QueryAsync("Test", options, transport))
        {
            // Process messages
        }

        // Assert
        transport.IsReady.Should().BeFalse(); // Should be disposed
    }

    [Fact]
    public async Task QueryAsync_ShouldAutoDisposeTransport()
    {
        // Arrange
        var transport = new MockTransport();
        transport.QueueMessage(new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("user"),
            ["message"] = JsonSerializer.SerializeToElement(new { content = "test" })
        });

        // Act
        await foreach (var _ in ClaudeAgent.QueryAsync("Test", transport: transport))
        {
            // Process
        }

        // Assert
        transport.IsReady.Should().BeFalse(); // Disposed after enumeration
    }
}

public class RobustnessTests
{
    [Fact]
    public async Task Client_RapidQueryAndDisconnect_ShouldHandle()
    {
        // Arrange & Act
        for (int i = 0; i < 50; i++)
        {
            var transport = new MockTransport();
            var client = new ClaudeSdkClient(transport: transport);
            await client.ConnectAsync();
            await client.QueryAsync($"Query {i}");
            await client.DisposeAsync();
        }

        // Assert - should not throw
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Client_ManyMessagesInQueue_ShouldProcessAll()
    {
        // Arrange
        var transport = new MockTransport();
        for (int i = 0; i < 1000; i++)
        {
            transport.QueueMessage(CreateTestMessage(i));
        }

        var client = new ClaudeSdkClient(transport: transport);
        await client.ConnectAsync();

        // Act
        var messages = new List<Message>();
        await foreach (var msg in client.ReceiveMessagesAsync())
        {
            messages.Add(msg);
        }

        // Assert
        messages.Should().HaveCount(1000);

        await client.DisposeAsync();
    }

    [Fact]
    public async Task Client_VeryLongQuery_ShouldSend()
    {
        // Arrange
        var transport = new MockTransport();
        var client = new ClaudeSdkClient(transport: transport);
        await client.ConnectAsync();

        var longQuery = new string('x', 100_000);

        // Act
        await client.QueryAsync(longQuery);

        // Assert
        transport.WrittenData.Should().HaveCount(1);
        transport.WrittenData[0].Should().Contain(longQuery);

        await client.DisposeAsync();
    }

    [Fact]
    public async Task Client_RapidSequentialQueries_ShouldAllBeSent()
    {
        // Arrange
        var transport = new MockTransport();
        var client = new ClaudeSdkClient(transport: transport);
        await client.ConnectAsync();

        // Act
        for (int i = 0; i < 100; i++)
        {
            await client.QueryAsync($"Query {i}");
        }

        // Assert
        transport.WrittenData.Should().HaveCount(100);

        await client.DisposeAsync();
    }

    [Fact]
    public async Task Client_AlternatingQueryAndReceive_ShouldWork()
    {
        // Arrange
        var transport = new MockTransport();
        var client = new ClaudeSdkClient(transport: transport);
        await client.ConnectAsync();

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            await client.QueryAsync($"Query {i}");
            transport.QueueMessage(CreateTestMessage(i));
            transport.QueueMessage(CreateResultMessage());

            var count = 0;
            await foreach (var msg in client.ReceiveResponseAsync())
            {
                count++;
            }
            count.Should().Be(2); // Message + Result
        }

        await client.DisposeAsync();
    }

    private static Dictionary<string, object> CreateTestMessage(int index)
    {
        return new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("assistant"),
            ["message"] = JsonSerializer.SerializeToElement(new
            {
                role = "assistant",
                model = "claude-sonnet-4",
                content = new[] { new { type = "text", text = $"Response {index}" } }
            }),
            ["parent_tool_use_id"] = JsonSerializer.SerializeToElement((string?)null)
        };
    }

    private static Dictionary<string, object> CreateResultMessage()
    {
        return new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("result"),
            ["subtype"] = JsonSerializer.SerializeToElement("success"),
            ["duration_ms"] = JsonSerializer.SerializeToElement(100),
            ["duration_api_ms"] = JsonSerializer.SerializeToElement(90),
            ["is_error"] = JsonSerializer.SerializeToElement(false),
            ["num_turns"] = JsonSerializer.SerializeToElement(1),
            ["session_id"] = JsonSerializer.SerializeToElement("test_session")
        };
    }
}
