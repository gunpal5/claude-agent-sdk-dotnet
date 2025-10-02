using System.Text.Json;
using ClaudeAgentSdk.Transport;
using FluentAssertions;
using Moq;

namespace ClaudeAgentSdk.Tests;

public class TransportTests
{
    [Fact]
    public void SubprocessCliTransport_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessCliTransport("test prompt", options);

        // Assert
        transport.Should().NotBeNull();
        transport.IsReady.Should().BeFalse();
    }

    [Fact]
    public void SubprocessCliTransport_IsReady_ShouldBeFalseInitially()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessCliTransport("test", options);

        // Assert
        transport.IsReady.Should().BeFalse();
    }

    [Fact]
    public async Task SubprocessCliTransport_WriteAsync_WhenNotReady_ShouldThrow()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessCliTransport("test", options);

        // Act & Assert
        await Assert.ThrowsAsync<CliConnectionException>(
            async () => await transport.WriteAsync("test data"));
    }

    [Fact]
    public async Task SubprocessCliTransport_ReadMessagesAsync_WhenNotConnected_ShouldThrow()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessCliTransport("test", options);

        // Act & Assert
        await Assert.ThrowsAsync<CliConnectionException>(async () =>
        {
            await foreach (var _ in transport.ReadMessagesAsync())
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task SubprocessCliTransport_DisposeAsync_ShouldCleanupResources()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessCliTransport("test", options);

        // Act
        await transport.DisposeAsync();

        // Assert
        transport.IsReady.Should().BeFalse();
    }

    [Fact]
    public async Task SubprocessCliTransport_DisposeAsync_MultipleTimesDoesNotThrow()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessCliTransport("test", options);

        // Act & Assert
        await transport.DisposeAsync();
        await transport.DisposeAsync(); // Should not throw
    }
}

public class MockTransport : ITransport
{
    private readonly Queue<Dictionary<string, object>> _messages = new();
    private bool _isConnected;
    private readonly List<string> _writtenData = new();

    public bool IsReady => _isConnected;
    public IReadOnlyList<string> WrittenData => _writtenData;

    public void QueueMessage(Dictionary<string, object> message)
    {
        _messages.Enqueue(message);
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _isConnected = true;
        return Task.CompletedTask;
    }

    public Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new CliConnectionException("Not connected");

        _writtenData.Add(data);
        return Task.CompletedTask;
    }

    public Task EndInputAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<Dictionary<string, object>> ReadMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (_messages.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            yield return _messages.Dequeue();
            await Task.Delay(10, cancellationToken); // Simulate async operation
        }
    }

    public ValueTask DisposeAsync()
    {
        _isConnected = false;
        _messages.Clear();
        return ValueTask.CompletedTask;
    }
}

public class TransportIntegrationTests
{
    [Fact]
    public async Task MockTransport_ShouldConnectAndReadMessages()
    {
        // Arrange
        var transport = new MockTransport();
        transport.QueueMessage(new Dictionary<string, object>
        {
            ["type"] = JsonSerializer.SerializeToElement("user"),
            ["message"] = JsonSerializer.SerializeToElement(new { content = "test" })
        });

        // Act
        await transport.ConnectAsync();
        var messages = new List<Dictionary<string, object>>();
        await foreach (var msg in transport.ReadMessagesAsync())
        {
            messages.Add(msg);
        }

        // Assert
        transport.IsReady.Should().BeTrue();
        messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task MockTransport_ShouldWriteData()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        // Act
        await transport.WriteAsync("test data 1");
        await transport.WriteAsync("test data 2");

        // Assert
        transport.WrittenData.Should().HaveCount(2);
        transport.WrittenData[0].Should().Be("test data 1");
        transport.WrittenData[1].Should().Be("test data 2");
    }

    [Fact]
    public async Task MockTransport_WriteBeforeConnect_ShouldThrow()
    {
        // Arrange
        var transport = new MockTransport();

        // Act & Assert
        await Assert.ThrowsAsync<CliConnectionException>(
            async () => await transport.WriteAsync("test"));
    }

    [Fact]
    public async Task MockTransport_DisposeAsync_ShouldDisconnect()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();
        transport.IsReady.Should().BeTrue();

        // Act
        await transport.DisposeAsync();

        // Assert
        transport.IsReady.Should().BeFalse();
    }
}

public class ClaudeAgentOptionsTests
{
    [Fact]
    public void ClaudeAgentOptions_DefaultConstructor_ShouldInitializeCollections()
    {
        // Act
        var options = new ClaudeAgentOptions();

        // Assert
        options.AllowedTools.Should().NotBeNull().And.BeEmpty();
        options.DisallowedTools.Should().NotBeNull().And.BeEmpty();
        options.AddDirs.Should().NotBeNull().And.BeEmpty();
        options.Env.Should().NotBeNull().And.BeEmpty();
        options.ExtraArgs.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ClaudeAgentOptions_WithAllowedTools_ShouldSetCorrectly()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            AllowedTools = new List<string> { "Read", "Write", "Bash" }
        };

        // Assert
        options.AllowedTools.Should().HaveCount(3);
        options.AllowedTools.Should().Contain("Read");
    }

    [Fact]
    public void ClaudeAgentOptions_WithSystemPrompt_ShouldSetCorrectly()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = "You are a helpful assistant"
        };

        // Assert
        options.SystemPrompt.Should().Be("You are a helpful assistant");
    }

    [Fact]
    public void ClaudeAgentOptions_WithPermissionMode_ShouldSetCorrectly()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            PermissionMode = PermissionMode.AcceptEdits
        };

        // Assert
        options.PermissionMode.Should().Be(PermissionMode.AcceptEdits);
    }

    [Fact]
    public void ClaudeAgentOptions_WithMaxTurns_ShouldSetCorrectly()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            MaxTurns = 10
        };

        // Assert
        options.MaxTurns.Should().Be(10);
    }

    [Fact]
    public void ClaudeAgentOptions_WithCwd_ShouldSetCorrectly()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Cwd = "/test/path"
        };

        // Assert
        options.Cwd.Should().Be("/test/path");
    }

    [Fact]
    public void ClaudeAgentOptions_WithEnvironmentVariables_ShouldSetCorrectly()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Env = new Dictionary<string, string>
            {
                ["TEST_VAR"] = "test_value",
                ["ANOTHER_VAR"] = "another_value"
            }
        };

        // Assert
        options.Env.Should().HaveCount(2);
        options.Env["TEST_VAR"].Should().Be("test_value");
    }

    [Fact]
    public void ClaudeAgentOptions_WithMcpServers_ShouldSetCorrectly()
    {
        // Arrange
        var mcpConfig = new McpStdioServerConfig
        {
            Command = "python",
            Args = new List<string> { "-m", "server" }
        };

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, object>
            {
                ["test_server"] = mcpConfig
            }
        };

        // Assert
        options.McpServers.Should().NotBeNull();
    }
}
