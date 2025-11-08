# Claude Agent SDK for .NET (Unofficial)

[![NuGet](https://img.shields.io/nuget/v/ClaudeAgentSdk.svg)](https://www.nuget.org/packages/ClaudeAgentSdk/)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-blue)]()
[![License](https://img.shields.io/badge/license-MIT-blue)]()

Unofficial .NET SDK for [Claude Code](https://claude.com/claude-code). Build production-ready AI applications with Claude's advanced code generation, analysis, and agentic capabilities.

**🎯 Status:** Production Ready | **📖 Docs:** Complete

> **Note:** This is an unofficial community SDK. For official Anthropic SDKs, visit [anthropic.com](https://anthropic.com).

## ✨ Features

- 🚀 **Simple Query API** - One-shot queries with `ClaudeAgent.QueryAsync()`
- 💬 **Interactive Client** - Multi-turn conversations with `ClaudeSdkClient`
- 📡 **Streaming Responses** - Real-time partial message updates
- 🛠️ **Tool Support** - File operations (Read, Write, Edit), Bash commands, Web fetching
- 🔒 **Type-Safe** - Strongly-typed messages and content blocks
- ⚡ **Async/Await** - Built on modern C# async patterns with `IAsyncEnumerable<T>`
- 🎯 **Error Handling** - Comprehensive exception hierarchy
- 🧹 **Resource Management** - Automatic cleanup with `IAsyncDisposable`
- ✅ **Battle-Tested** - 117 comprehensive tests covering edge cases
- 📊 **Todo & Token Tracking** - Monitor Claude's task progress and API usage
- 📚 **Well-Documented** - Complete guides and interactive examples

## 📦 Installation

### Via NuGet Package Manager

```bash
dotnet add package ClaudeAgentSdk
```

### Prerequisites

1. **.NET 8.0 or higher**
   ```bash
   dotnet --version  # Should be 8.0+
   ```

2. **Claude Code CLI**
   ```bash
   npm install -g @anthropic-ai/claude-code
   ```

3. **Anthropic API Key** - Set as environment variable:
   ```bash
   # Windows (PowerShell)
   $env:ANTHROPIC_API_KEY = "your-api-key-here"

   # Linux/macOS
   export ANTHROPIC_API_KEY="your-api-key-here"
   ```

## 🚀 Quick Start

### Simple Query

```csharp
using ClaudeAgentSdk;

await foreach (var message in ClaudeAgent.QueryAsync("What is 2 + 2?"))
{
    if (message is AssistantMessage assistantMsg)
    {
        foreach (var block in assistantMsg.Content)
        {
            if (block is TextBlock textBlock)
            {
                Console.WriteLine(textBlock.Text);
            }
        }
    }
    else if (message is ResultMessage resultMsg)
    {
        Console.WriteLine($"✓ Completed in {resultMsg.DurationMs}ms");
    }
}
```

### Interactive Multi-Turn Conversation

```csharp
using ClaudeAgentSdk;

var options = new ClaudeAgentOptions { MaxTurns = 50 };

await using var client = new ClaudeSdkClient(options);
await client.ConnectAsync();

// First query
await client.QueryAsync("Create a Python web server");
await foreach (var msg in client.ReceiveResponseAsync())
{
    // Handle response...
}

// Follow-up query in same session
await client.QueryAsync("Now add authentication to it");
await foreach (var msg in client.ReceiveResponseAsync())
{
    // Handle response...
}
```

### With Tool Support

```csharp
var options = new ClaudeAgentOptions
{
    AllowedTools = new List<string> { "Read", "Write", "Edit", "Bash" },
    PermissionMode = PermissionMode.Default
};

await foreach (var message in ClaudeAgent.QueryAsync(
    "Create a file called hello.txt with 'Hello World'", options))
{
    if (message is AssistantMessage assistantMsg)
    {
        foreach (var block in assistantMsg.Content)
        {
            if (block is ToolUseBlock toolUse)
            {
                Console.WriteLine($"🔧 Using tool: {toolUse.Name}");
            }
        }
    }
}
```

### Tracking Todo Tasks and Token Usage

```csharp
await foreach (var message in ClaudeAgent.QueryAsync("Build a REST API with auth"))
{
    switch (message)
    {
        case SystemMessage { Subtype: "todo" } sysMsg:
            // Display Claude's todo list
            break;

        case AssistantMessage { Content: var content }:
            foreach (var block in content)
            {
                if (block is ToolUseBlock { Name: "TodoWrite" } toolUse)
                {
                    // Parse and display todos from toolUse.Input
                }
            }
            break;

        case ResultMessage resultMsg:
            // Display token usage and cost
            var inputTokens = resultMsg.Usage["input_tokens"];
            var outputTokens = resultMsg.Usage["output_tokens"];
            Console.WriteLine($"Tokens: {inputTokens + outputTokens:N0}");
            Console.WriteLine($"Cost: ${resultMsg.TotalCostUsd:F6}");
            break;
    }
}
```

## 📚 Documentation

### Guides

- **[Getting Started Guide](GETTING_STARTED.md)** - Comprehensive tutorial with code examples
- **[Tracking Guide](TRACKING_GUIDE.md)** - Monitor todos, tokens, and costs in real-time
- **[Implementation Notes](IMPLEMENTATION.md)** - Technical architecture and design decisions
- **[Project Summary](PROJECT_SUMMARY.md)** - Overview of all components

### Examples

- **[ClaudeChat](examples/ClaudeChat/)** - Full-featured interactive CLI application
  - Simple query mode
  - Interactive conversation mode
  - Streaming mode
  - Tools demo mode
  - Todo and usage tracking mode

### API Reference

#### Core Types

- **`ClaudeAgent`** - Static class for simple queries
  - `QueryAsync(prompt, options)` - Execute a one-shot query

- **`ClaudeSdkClient`** - Interactive client for multi-turn conversations
  - `ConnectAsync(prompt?)` - Connect to Claude
  - `QueryAsync(prompt)` - Send a query
  - `ReceiveResponseAsync()` - Receive messages until ResultMessage
  - `ReceiveMessagesAsync()` - Receive all messages continuously

#### Message Types

- **`Message`** - Base class for all messages
- **`AssistantMessage`** - Messages from Claude
- **`UserMessage`** - Messages from user
- **`SystemMessage`** - System notifications (including todos)
- **`ResultMessage`** - Query completion with metrics
- **`StreamEvent`** - Streaming progress updates

#### Content Blocks

- **`TextBlock`** - Text content
- **`ThinkingBlock`** - Claude's internal reasoning
- **`ToolUseBlock`** - Tool invocation
- **`ToolResultBlock`** - Tool execution result

#### Options

```csharp
public class ClaudeAgentOptions
{
    public List<string> AllowedTools { get; set; }       // ["Read", "Write", "Bash"]
    public List<string> DisallowedTools { get; set; }
    public string? SystemPrompt { get; set; }
    public PermissionMode? PermissionMode { get; set; }  // Default, AllowAll, Deny
    public int? MaxTurns { get; set; }                   // Default: 25
    public string? Model { get; set; }                   // Default: claude-sonnet-4
    public bool IncludePartialMessages { get; set; }     // Enable streaming
    public string? Cwd { get; set; }                     // Working directory
    public Dictionary<string, string> Env { get; set; }  // Environment variables
    // ... and more
}
```

## 🎯 Use Cases

### Code Generation
```csharp
await foreach (var msg in ClaudeAgent.QueryAsync(
    "Create a REST API for a blog with CRUD operations in C#"))
{
    // Claude will generate the code with proper structure
}
```

### Code Analysis
```csharp
var options = new ClaudeAgentOptions
{
    AllowedTools = new List<string> { "Read", "Grep" }
};

await foreach (var msg in ClaudeAgent.QueryAsync(
    "Analyze the code in src/ directory for potential bugs", options))
{
    // Claude will read and analyze your code
}
```

### File Operations
```csharp
var options = new ClaudeAgentOptions
{
    AllowedTools = new List<string> { "Read", "Write", "Edit" }
};

await foreach (var msg in ClaudeAgent.QueryAsync(
    "Refactor all .cs files to use nullable reference types", options))
{
    // Claude will read, analyze, and modify files
}
```

### Testing and QA
```csharp
var options = new ClaudeAgentOptions
{
    AllowedTools = new List<string> { "Read", "Write", "Bash" }
};

await foreach (var msg in ClaudeAgent.QueryAsync(
    "Create unit tests for MyService.cs and run them", options))
{
    // Claude will write tests and execute them
}
```

## 🔧 Advanced Features

### Custom Transport

```csharp
public class MyCustomTransport : ITransport
{
    public bool IsReady => true;

    public async Task ConnectAsync(CancellationToken ct) { /* ... */ }

    public async IAsyncEnumerable<Dictionary<string, object>> ReadMessagesAsync(
        CancellationToken ct)
    {
        // Custom message reading logic
        yield return message;
    }

    public async Task WriteAsync(string data, CancellationToken ct) { /* ... */ }
}

var client = new ClaudeSdkClient(options, new MyCustomTransport());
```

### Permission Callbacks

```csharp
var options = new ClaudeAgentOptions
{
    CanUseTool = (toolName, toolInput) =>
    {
        Console.WriteLine($"Allow tool {toolName}?");
        var response = Console.ReadLine();
        return response?.ToLower() == "yes"
            ? PermissionBehavior.Allow
            : PermissionBehavior.Deny;
    }
};
```

### Error Handling

```csharp
try
{
    await foreach (var msg in ClaudeAgent.QueryAsync("Your query"))
    {
        // Process messages
    }
}
catch (CliNotFoundException ex)
{
    Console.WriteLine("Claude Code CLI not found. Install with:");
    Console.WriteLine("  npm install -g @anthropic-ai/claude-code");
}
catch (CliConnectionException ex)
{
    Console.WriteLine($"Connection error: {ex}");
}
catch (ProcessException ex)
{
    Console.WriteLine($"Process failed: {ex.ExitCode}");
}
```

## 📦 Building NuGet Package

```bash
cd src/ClaudeAgentSdk
dotnet pack --configuration Release
```

The package will be created in `bin/Release/ClaudeAgentSdk.0.1.0.nupkg`

### Publishing to NuGet

```bash
dotnet nuget push bin/Release/ClaudeAgentSdk.0.1.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Setup

```bash
# Clone repository
git clone https://github.com/gunpal5/claude-agent-sdk-dotnet.git
cd claude-agent-sdk-dotnet

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run example
cd examples/ClaudeChat
dotnet run
```

## 📄 License

MIT License - see [LICENSE](LICENSE) file for details.

## 🔗 Links

- **NuGet Package:** https://www.nuget.org/packages/ClaudeAgentSdk/
- **GitHub Repository:** https://github.com/gunpal5/claude-agent-sdk-dotnet
- **Official Python SDK:** https://github.com/anthropics/claude-agent-sdk-python
- **Claude Code Documentation:** https://docs.claude.com/claude-code
- **Anthropic Website:** https://anthropic.com

## 💡 Support

- **Documentation:** See guides in this repository
- **Issues:** Report bugs via [GitHub Issues](https://github.com/gunpal5/claude-agent-sdk-dotnet/issues)
- **Discussions:** Join [GitHub Discussions](https://github.com/gunpal5/claude-agent-sdk-dotnet/discussions)
- **Claude Code Help:** https://docs.claude.com/claude-code

## 🎓 Example Projects

Check out the `examples/` directory for complete working examples:

- **ClaudeChat** - Interactive CLI with 5 modes (simple, interactive, streaming, tools, stats)
- **TodoAndUsageExample** - Real-time tracking of todos and token usage

## ⚡ Performance

- Streaming responses with minimal latency
- Efficient JSON parsing with System.Text.Json
- Async/await throughout for non-blocking I/O
- Automatic resource cleanup
- Subprocess communication optimized for large outputs (1MB buffer)

## 🔐 Security

- API keys handled securely via environment variables
- Permission modes for tool usage control
- Custom tool approval callbacks
- No sensitive data logged by default
- Process isolation for CLI execution

## 📊 Metrics & Monitoring

Track your usage:
```csharp
if (message is ResultMessage result)
{
    // Token usage
    var tokens = result.Usage["input_tokens"] + result.Usage["output_tokens"];

    // Cost in USD
    var cost = result.TotalCostUsd;

    // Performance
    var duration = result.DurationMs;
    var apiTime = result.DurationApiMs;

    // Conversation
    var turns = result.NumTurns;
}
```

## 🌟 Why This SDK?

- **Production-Ready:** Complete port of the official Python SDK
- **Idiomatic C#:** Async/await, IAsyncEnumerable, IAsyncDisposable patterns
- **Type-Safe:** Strong typing throughout with nullable reference types
- **Well-Tested:** 117 comprehensive tests ensure reliability
- **Community-Driven:** Open source and community maintained
- **Great Documentation:** Multiple guides and working examples

---

*This is an unofficial community SDK. For official Anthropic SDKs, visit [anthropic.com](https://anthropic.com).*
