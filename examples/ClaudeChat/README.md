# ClaudeChat - Interactive CLI Demo

A full-featured command-line application demonstrating real integration with Claude Code using the .NET SDK.

## Prerequisites

1. **.NET 8.0+** installed
2. **Claude Code CLI** installed:
   ```bash
   npm install -g @anthropic-ai/claude-code
   ```
3. **Anthropic API Key** configured (Claude Code will prompt if not set)

## Features

This demo application showcases **4 different modes** of interaction with Claude:

### 1. Simple Mode (Default)
One-shot query with detailed response metrics.

```bash
dotnet run
# or
dotnet run simple
```

**Features:**
- ✅ Single question → Single answer
- ✅ Shows thinking process
- ✅ Displays timing metrics (API time, local time)
- ✅ Shows cost and turn count
- ✅ Color-coded output
- ✅ Error handling with helpful messages

**Example:**
```
Enter your question: What is the capital of France?

🤖 Claude is thinking...

Paris is the capital of France.

✓ Completed in 1234ms (API: 1100ms, Local: 1250ms)
💰 Cost: $0.0012
🔄 Turns: 1
```

### 2. Interactive Mode
Multi-turn conversation with context preservation.

```bash
dotnet run interactive
```

**Features:**
- ✅ Continuous conversation loop
- ✅ Context maintained across turns
- ✅ Type `/exit` to quit
- ✅ Real-time response streaming
- ✅ Error handling per response

**Example:**
```
You: What is 2 + 2?
Claude: 2 + 2 equals 4.

You: What about 3 times that?
Claude: 3 times 4 equals 12.

You: /exit
Goodbye!
```

### 3. Streaming Mode
Shows partial responses as they arrive.

```bash
dotnet run streaming
```

**Features:**
- ✅ Real-time streaming with `IncludePartialMessages`
- ✅ Visual progress indicator (dots)
- ✅ Immediate response display
- ✅ Stream completion notification

**Example:**
```
Enter your question: Write a haiku about coding

🤖 Claude is responding (streaming)...

.......

Lines of code unfold,
Logic weaves through silent night,
Bugs in morning light.

✓ Stream completed - 1 turns
```

### 4. Tools Mode
Demonstrates file operations and tool usage.

```bash
dotnet run tools
```

**Features:**
- ✅ File operations (Read, Write, Edit, Bash)
- ✅ Tool use visualization
- ✅ Input/output display
- ✅ Permission mode handling
- ✅ Safety warning
- ✅ Tool use count tracking

**Example:**
```
This demo allows Claude to use file tools.
⚠️  Claude can read/write files in the current directory!

Enter your request: Create a file called test.txt with "Hello World"

🔧 Claude is working with tools enabled...

I'll create that file for you.

🔧 Using tool: Write
   Input: {"file_path":"test.txt","content":"Hello World"}
   Result: File written successfully

✓ Completed with 1 tool uses
⏱️  Duration: 2345ms
💰 Cost: $0.0023
```

## Usage Examples

### Quick Start
```bash
# Navigate to the example directory
cd examples/ClaudeChat

# Run in simple mode (default)
dotnet run

# Run in interactive mode
dotnet run interactive

# Run with streaming
dotnet run streaming

# Run with tools enabled
dotnet run tools
```

### Building the Application
```bash
# Build
dotnet build

# Run the built executable
dotnet run --no-build

# Create a release build
dotnet build -c Release
./bin/Release/net8.0/ClaudeChat
```

### Publishing for Distribution
```bash
# Publish as self-contained application (includes .NET runtime)
dotnet publish -c Release -r win-x64 --self-contained

# Publish as framework-dependent (smaller, requires .NET installed)
dotnet publish -c Release -r win-x64 --self-contained false
```

## Code Structure

```
Program.cs
├── Main()                    # Entry point, mode selection
├── RunSimpleMode()          # One-shot query with metrics
├── RunInteractiveMode()     # Multi-turn conversation
├── RunStreamingMode()       # Streaming responses
└── RunToolsDemo()           # File operations demo
```

## Features Demonstrated

### SDK Features Used

1. **ClaudeAgent.QueryAsync()** - Simple query API
   ```csharp
   await foreach (var message in ClaudeAgent.QueryAsync(question))
   {
       // Process messages
   }
   ```

2. **ClaudeSdkClient** - Interactive client
   ```csharp
   await using var client = new ClaudeSdkClient(options);
   await client.ConnectAsync();
   await client.QueryAsync(input);
   await foreach (var msg in client.ReceiveResponseAsync())
   {
       // Process response
   }
   ```

3. **ClaudeAgentOptions** - Configuration
   ```csharp
   var options = new ClaudeAgentOptions
   {
       AllowedTools = new List<string> { "Read", "Write" },
       PermissionMode = PermissionMode.Default,
       MaxTurns = 50,
       IncludePartialMessages = true,
       SystemPrompt = "Custom instructions..."
   };
   ```

4. **Message Types** - Typed message handling
   - `AssistantMessage` - Claude's responses
   - `UserMessage` - User inputs
   - `ResultMessage` - Query results with metrics
   - `StreamEvent` - Partial streaming updates

5. **Content Blocks** - Different content types
   - `TextBlock` - Text responses
   - `ThinkingBlock` - Internal reasoning
   - `ToolUseBlock` - Tool invocations
   - `ToolResultBlock` - Tool outputs

### UI Features

- ✅ **Color-coded output** using `Console.ForegroundColor`
- ✅ **Unicode box drawing** for header
- ✅ **Progress indicators** (dots, spinners)
- ✅ **Error handling** with helpful messages
- ✅ **Metrics display** (time, cost, turns)
- ✅ **Tool visualization** with inputs/outputs

## Error Handling

The application handles common errors gracefully:

### CLI Not Found
```
❌ Error: Claude Code CLI not found!

Please install Claude Code:
  npm install -g @anthropic-ai/claude-code
```

### API Key Not Set
Claude Code CLI will prompt you to configure your API key on first use.

### Connection Errors
```
❌ Error: Failed to start Claude Code: [error details]
```

### Tool Permission Denied
With `PermissionMode.Default`, Claude Code will prompt for approval before dangerous operations.

## Customization

### Adding New Modes

Add a new case to the switch statement in `Main()`:

```csharp
case "custom":
    await RunCustomMode();
    break;
```

Then implement your custom mode:

```csharp
static async Task RunCustomMode()
{
    var options = new ClaudeAgentOptions
    {
        // Your custom options
    };

    await foreach (var message in ClaudeAgent.QueryAsync("prompt", options))
    {
        // Your custom handling
    }
}
```

### Changing Colors

Modify the `Console.ForegroundColor` settings:

```csharp
Console.ForegroundColor = ConsoleColor.Magenta;  // Change to your preference
Console.WriteLine("Your text");
Console.ResetColor();
```

### Adding More Tools

Extend the `AllowedTools` list:

```csharp
AllowedTools = new List<string>
{
    "Read",
    "Write",
    "Edit",
    "Bash",
    "Glob",     // File search
    "Grep",     // Content search
    "WebFetch", // Web scraping
    // See Claude Code docs for full list
}
```

## Troubleshooting

### "Claude Code not found"
Install or reinstall Claude Code:
```bash
npm install -g @anthropic-ai/claude-code
```

### "Permission denied" errors
Run with appropriate permissions, or use `PermissionMode.AcceptEdits` in your options (use with caution).

### High API costs
Set `MaxTurns` to limit conversation length:
```csharp
MaxTurns = 5  // Limit to 5 back-and-forth exchanges
```

### Slow responses
This is normal for complex queries. The application shows timing metrics to help you understand performance.

## Advanced Usage

### Custom Working Directory
```csharp
var options = new ClaudeAgentOptions
{
    Cwd = "/path/to/project"
};
```

### Environment Variables
```csharp
var options = new ClaudeAgentOptions
{
    Env = new Dictionary<string, string>
    {
        ["MY_VAR"] = "value"
    }
};
```

### Session Management
```csharp
await client.QueryAsync("Hello", sessionId: "my-session");
```

## Performance Tips

1. **Use Simple Mode** for one-off queries
2. **Use Interactive Mode** for conversations
3. **Enable Streaming** for faster perceived response time
4. **Limit Tools** to only what you need
5. **Set MaxTurns** to prevent runaway costs

## Learning Resources

- [Claude Code SDK Documentation](https://docs.anthropic.com/en/docs/claude-code/sdk)
- [Python SDK Examples](../../claude-agent-sdk-python/examples/)
- [SDK Implementation Notes](../../IMPLEMENTATION.md)
- [Test Report](../../TEST_REPORT.md)

## Contributing

To add new features or modes:

1. Implement your mode function
2. Add it to the switch statement in `Main()`
3. Update this README with usage instructions
4. Test thoroughly with the real Claude Code CLI

## License

MIT - Same as parent project
