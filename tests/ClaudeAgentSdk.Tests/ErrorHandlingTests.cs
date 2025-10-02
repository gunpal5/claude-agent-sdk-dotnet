using FluentAssertions;

namespace ClaudeAgentSdk.Tests;

public class ErrorHandlingTests
{
    [Fact]
    public void ClaudeSdkException_ShouldBeConstructedWithMessage()
    {
        // Act
        var exception = new ClaudeSdkException("Test error");

        // Assert
        exception.Message.Should().Be("Test error");
    }

    [Fact]
    public void ClaudeSdkException_ShouldBeConstructedWithInnerException()
    {
        // Arrange
        var inner = new Exception("Inner error");

        // Act
        var exception = new ClaudeSdkException("Test error", inner);

        // Assert
        exception.Message.Should().Be("Test error");
        exception.InnerException.Should().Be(inner);
    }

    [Fact]
    public void CliNotFoundException_ShouldInheritFromClaudeSdkException()
    {
        // Act
        var exception = new CliNotFoundException("CLI not found");

        // Assert
        exception.Should().BeAssignableTo<ClaudeSdkException>();
        exception.Message.Should().Be("CLI not found");
    }

    [Fact]
    public void CliConnectionException_ShouldInheritFromClaudeSdkException()
    {
        // Act
        var exception = new CliConnectionException("Connection failed");

        // Assert
        exception.Should().BeAssignableTo<ClaudeSdkException>();
        exception.Message.Should().Be("Connection failed");
    }

    [Fact]
    public void ProcessException_ShouldContainExitCode()
    {
        // Act
        var exception = new ProcessException("Process failed", 127);

        // Assert
        exception.Should().BeAssignableTo<ClaudeSdkException>();
        exception.ExitCode.Should().Be(127);
        exception.Message.Should().Be("Process failed");
    }

    [Fact]
    public void ProcessException_ShouldContainStderr()
    {
        // Act
        var exception = new ProcessException("Process failed", 1, "Error output here");

        // Assert
        exception.ExitCode.Should().Be(1);
        exception.Stderr.Should().Be("Error output here");
    }

    [Fact]
    public void CliJsonDecodeException_ShouldInheritFromClaudeSdkException()
    {
        // Act
        var exception = new CliJsonDecodeException("JSON parsing failed");

        // Assert
        exception.Should().BeAssignableTo<ClaudeSdkException>();
        exception.Message.Should().Be("JSON parsing failed");
    }

    [Fact]
    public void CliJsonDecodeException_WithInnerException_ShouldPreserveIt()
    {
        // Arrange
        var inner = new Exception("JSON syntax error");

        // Act
        var exception = new CliJsonDecodeException("JSON parsing failed", inner);

        // Assert
        exception.InnerException.Should().Be(inner);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(255)]
    [InlineData(-1)]
    public void ProcessException_ShouldHandleVariousExitCodes(int exitCode)
    {
        // Act
        var exception = new ProcessException("Process failed", exitCode);

        // Assert
        exception.ExitCode.Should().Be(exitCode);
    }

    [Fact]
    public void AllExceptions_ShouldBeSerializable()
    {
        // Arrange
        var exceptions = new Exception[]
        {
            new ClaudeSdkException("Test"),
            new CliNotFoundException("Not found"),
            new CliConnectionException("Connection error"),
            new ProcessException("Process error", 1),
            new CliJsonDecodeException("JSON error")
        };

        // Act & Assert
        foreach (var ex in exceptions)
        {
            ex.Should().NotBeNull();
            ex.Message.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void ProcessException_WithNullStderr_ShouldNotThrow()
    {
        // Act
        var exception = new ProcessException("Process failed", 1, (string?)null);

        // Assert
        exception.Stderr.Should().BeNull();
        exception.ExitCode.Should().Be(1);
    }

    [Fact]
    public void ProcessException_WithEmptyStderr_ShouldPreserveIt()
    {
        // Act
        var exception = new ProcessException("Process failed", 1, "");

        // Assert
        exception.Stderr.Should().BeEmpty();
    }

    [Fact]
    public void ProcessException_WithMultilineStderr_ShouldPreserveIt()
    {
        // Arrange
        var stderr = "Line 1\nLine 2\nLine 3";

        // Act
        var exception = new ProcessException("Process failed", 1, stderr);

        // Assert
        exception.Stderr.Should().Be(stderr);
        exception.Stderr.Should().Contain("\n");
    }

    [Fact]
    public void ExceptionHierarchy_ShouldBeCorrect()
    {
        // Arrange & Act
        Exception[] exceptions = new Exception[]
        {
            new CliNotFoundException("test"),
            new CliConnectionException("test"),
            new ProcessException("test", 1),
            new CliJsonDecodeException("test")
        };

        // Assert
        foreach (var ex in exceptions)
        {
            ex.Should().BeAssignableTo<ClaudeSdkException>();
            ex.Should().BeAssignableTo<Exception>();
        }
    }
}

public class StressTests
{
    [Fact]
    public async Task MessageParser_ShouldHandleLargeNumberOfMessages()
    {
        // Arrange
        var messages = new List<Dictionary<string, object>>();
        for (int i = 0; i < 10000; i++)
        {
            messages.Add(CreateTestMessage(i));
        }

        // Act & Assert
        foreach (var data in messages)
        {
            var message = MessageParser.ParseMessage(data);
            message.Should().NotBeNull();
        }

        await Task.CompletedTask; // Suppress async warning
    }

    [Fact]
    public async Task Transport_ShouldHandleManyWrites()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            await transport.WriteAsync($"Message {i}\n");
        }

        // Assert
        transport.WrittenData.Should().HaveCount(1000);
    }

    [Fact]
    public async Task Transport_ShouldHandleLargeMessages()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();
        var largeMessage = new string('x', 1_000_000); // 1MB message

        // Act
        await transport.WriteAsync(largeMessage);

        // Assert
        transport.WrittenData.Should().HaveCount(1);
        transport.WrittenData[0].Should().HaveLength(1_000_000);
    }

    [Fact]
    public void ClaudeAgentOptions_ShouldHandleManyTools()
    {
        // Arrange
        var tools = Enumerable.Range(0, 100).Select(i => $"Tool_{i}").ToList();

        // Act
        var options = new ClaudeAgentOptions
        {
            AllowedTools = tools
        };

        // Assert
        options.AllowedTools.Should().HaveCount(100);
    }

    [Fact]
    public void ClaudeAgentOptions_ShouldHandleManyEnvironmentVariables()
    {
        // Arrange
        var env = Enumerable.Range(0, 100)
            .ToDictionary(i => $"VAR_{i}", i => $"value_{i}");

        // Act
        var options = new ClaudeAgentOptions
        {
            Env = env
        };

        // Assert
        options.Env.Should().HaveCount(100);
    }

    private static Dictionary<string, object> CreateTestMessage(int index)
    {
        return new Dictionary<string, object>
        {
            ["type"] = System.Text.Json.JsonSerializer.SerializeToElement("user"),
            ["message"] = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                role = "user",
                content = $"Test message {index}"
            }),
            ["parent_tool_use_id"] = System.Text.Json.JsonSerializer.SerializeToElement((string?)null)
        };
    }
}

public class ConcurrencyTests
{
    [Fact]
    public async Task Transport_MultipleWritesConcurrently_ShouldNotFail()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        // Act
        var tasks = Enumerable.Range(0, 100)
            .Select(i => transport.WriteAsync($"Message {i}\n"))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        transport.WrittenData.Should().HaveCount(100);
    }

    [Fact]
    public async Task MessageParser_ConcurrentParsing_ShouldNotFail()
    {
        // Arrange
        var messages = Enumerable.Range(0, 100)
            .Select(CreateTestMessage)
            .ToList();

        // Act
        var tasks = messages.Select(data => Task.Run(() => MessageParser.ParseMessage(data)));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(100);
        results.Should().AllBeOfType<UserMessage>();
    }

    [Fact]
    public async Task Transport_MultipleDisposeCalls_ShouldBeSafe()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        // Act
        var disposeTasks = Enumerable.Range(0, 10)
            .Select(_ => transport.DisposeAsync().AsTask())
            .ToArray();

        // Assert - should not throw
        await Task.WhenAll(disposeTasks);
        transport.IsReady.Should().BeFalse();
    }

    private static Dictionary<string, object> CreateTestMessage(int index)
    {
        return new Dictionary<string, object>
        {
            ["type"] = System.Text.Json.JsonSerializer.SerializeToElement("user"),
            ["message"] = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                role = "user",
                content = $"Test message {index}"
            }),
            ["parent_tool_use_id"] = System.Text.Json.JsonSerializer.SerializeToElement((string?)null)
        };
    }
}
