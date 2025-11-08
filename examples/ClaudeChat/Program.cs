using ClaudeAgentSdk;

/// <summary>
/// Interactive CLI chat application using Claude Code.
/// This demonstrates real integration with the Claude Code CLI.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          Claude Chat - Interactive CLI Demo               ║");
        Console.WriteLine("║                                                            ║");
        Console.WriteLine("║  Commands:                                                 ║");
        Console.WriteLine("║    /help    - Show available commands                      ║");
        Console.WriteLine("║    /tools   - Enable file tools (Read, Write, Bash)        ║");
        Console.WriteLine("║    /reset   - Start a new conversation                     ║");
        Console.WriteLine("║    /exit    - Exit the application                         ║");
        Console.WriteLine("║                                                            ║");
        Console.WriteLine("║  Prerequisites:                                            ║");
        Console.WriteLine("║    - Claude Code CLI must be installed                     ║");
        Console.WriteLine("║      npm install -g @anthropic-ai/claude-code             ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var mode = args.Length > 0 ? args[0].ToLower() : "simple";

        switch (mode)
        {
            case "simple":
                await RunSimpleMode();
                break;
            case "interactive":
                await RunInteractiveMode();
                break;
            case "streaming":
                await RunStreamingMode();
                break;
            case "tools":
                await RunToolsDemo();
                break;
            case "stats":
            case "usage":
            case "todo":
                await ClaudeChat.TodoAndUsageExample.Run();
                break;
            default:
                Console.WriteLine($"Unknown mode: {mode}");
                Console.WriteLine("Usage: ClaudeChat [simple|interactive|streaming|tools|stats]");
                break;
        }
    }

    /// <summary>
    /// Simple one-shot query mode.
    /// </summary>
    static async Task RunSimpleMode()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("         SIMPLE QUERY MODE");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine();

        Console.Write("Enter your question: ");
        var question = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(question))
        {
            Console.WriteLine("No question provided.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("🤖 Claude is thinking...");
        Console.WriteLine();

        try
        {
            var startTime = DateTime.Now;

            await foreach (var message in ClaudeAgent.QueryAsync(question))
            {
                if (message is AssistantMessage assistantMsg)
                {
                    foreach (var block in assistantMsg.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine(textBlock.Text);
                            Console.ResetColor();
                        }
                        else if (block is ThinkingBlock thinkingBlock)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.WriteLine($"[Thinking: {thinkingBlock.Thinking.Substring(0, Math.Min(50, thinkingBlock.Thinking.Length))}...]");
                            Console.ResetColor();
                        }
                    }
                }
                else if (message is ResultMessage resultMsg)
                {
                    var duration = DateTime.Now - startTime;
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Completed in {resultMsg.DurationMs}ms (API: {resultMsg.DurationApiMs}ms, Local: {duration.TotalMilliseconds:F0}ms)");
                    if (resultMsg.TotalCostUsd.HasValue && resultMsg.TotalCostUsd > 0)
                    {
                        Console.WriteLine($"💰 Cost: ${resultMsg.TotalCostUsd:F4}");
                    }
                    Console.WriteLine($"🔄 Turns: {resultMsg.NumTurns}");
                    Console.ResetColor();
                }
            }
        }
        catch (CliNotFoundException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Error: Claude Code CLI not found!");
            Console.WriteLine();
            Console.WriteLine("Please install Claude Code:");
            Console.WriteLine("  npm install -g @anthropic-ai/claude-code");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error: {ex}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Interactive multi-turn conversation mode.
    /// </summary>
    static async Task RunInteractiveMode()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("      INTERACTIVE CHAT MODE");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("Type your messages and press Enter.");
        Console.WriteLine("Type '/exit' to quit.");
        Console.WriteLine();

        var options = new ClaudeAgentOptions
        {
            MaxTurns = 50
        };

        try
        {
            await using var client = new ClaudeSdkClient(options);
            await client.ConnectAsync();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("You: ");
                Console.ResetColor();
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (input.Trim().ToLower() == "/exit")
                    break;

                await client.QueryAsync(input);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Claude: ");
                Console.ResetColor();

                await foreach (var msg in client.ReceiveResponseAsync())
                {
                    if (msg is SystemMessage sysMsg)
                    {
                        HandleSystemMessage(sysMsg);
                    }
                    else if (msg is AssistantMessage assistantMsg)
                    {
                        foreach (var block in assistantMsg.Content)
                        {
                            if (block is TextBlock textBlock)
                            {
                                Console.WriteLine(textBlock.Text);
                            }
                            else if (block is ToolUseBlock toolUse)
                            {
                                if (toolUse.Name == "TodoWrite")
                                {
                                    // Display todo list from TodoWrite tool
                                    DisplayTodoList(toolUse.Input);
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Magenta;
                                    Console.WriteLine($"🔧 Using tool: {toolUse.Name}");
                                    Console.ResetColor();
                                }
                            }
                        }
                    }
                    else if (msg is ResultMessage resultMsg)
                    {
                        if (resultMsg.IsError)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[Error: {resultMsg.Result}]");
                            Console.ResetColor();
                        }
                        else
                        {
                            // Show token usage summary
                            if (resultMsg.Usage != null)
                            {
                                var inputTokens = GetTokenCount(resultMsg.Usage, "input_tokens");
                                var outputTokens = GetTokenCount(resultMsg.Usage, "output_tokens");
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine($"[Tokens: {inputTokens + outputTokens:N0} | Duration: {resultMsg.DurationMs}ms]");
                                Console.ResetColor();
                            }
                        }
                    }
                }

                Console.WriteLine();
            }

            Console.WriteLine("Goodbye!");
        }
        catch (CliNotFoundException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Error: Claude Code CLI not found!");
            Console.WriteLine("Please install: npm install -g @anthropic-ai/claude-code");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error: {ex}");
            Console.ResetColor();
        }
    }

    private static void DisplayTodoList(Dictionary<string, object> input)
    {
        try
        {
            if (input.TryGetValue("todos", out var todosObj))
            {
                var todosJson = System.Text.Json.JsonSerializer.Serialize(todosObj);
                var todos = System.Text.Json.JsonSerializer.Deserialize<List<ClaudeChat.TodoItem>>(todosJson);

                if (todos != null && todos.Count > 0)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("📋 Todo List:");
                    Console.ResetColor();

                    foreach (var todo in todos)
                    {
                        var statusIcon = todo.Status switch
                        {
                            "completed" => "✓",
                            "in_progress" => "⟳",
                            "pending" => "○",
                            _ => "?"
                        };

                        var color = todo.Status switch
                        {
                            "completed" => ConsoleColor.Green,
                            "in_progress" => ConsoleColor.Cyan,
                            "pending" => ConsoleColor.Gray,
                            _ => ConsoleColor.White
                        };

                        Console.ForegroundColor = color;
                        Console.WriteLine($"  {statusIcon} {todo.Content}");
                        Console.ResetColor();
                    }

                    var completed = todos.Count(t => t.Status == "completed");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  Progress: {completed}/{todos.Count} completed");
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }
    }

    private static void HandleSystemMessage(SystemMessage sysMsg)
    {
        if (sysMsg.Subtype == "todo")
        {
            if (sysMsg.Data.TryGetValue("todos", out var todosObj))
            {
                try
                {
                    var todosJson = System.Text.Json.JsonSerializer.Serialize(todosObj);
                    var todos = System.Text.Json.JsonSerializer.Deserialize<List<ClaudeChat.TodoItem>>(todosJson);

                    if (todos != null && todos.Count > 0)
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("📋 Todo List:");
                        Console.ResetColor();

                        foreach (var todo in todos)
                        {
                            var statusIcon = todo.Status switch
                            {
                                "completed" => "✓",
                                "in_progress" => "⟳",
                                "pending" => "○",
                                _ => "?"
                            };

                            var color = todo.Status switch
                            {
                                "completed" => ConsoleColor.Green,
                                "in_progress" => ConsoleColor.Cyan,
                                "pending" => ConsoleColor.Gray,
                                _ => ConsoleColor.White
                            };

                            Console.ForegroundColor = color;
                            Console.WriteLine($"  {statusIcon} {todo.Content}");
                            Console.ResetColor();
                        }

                        var completed = todos.Count(t => t.Status == "completed");
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"  Progress: {completed}/{todos.Count} completed");
                        Console.ResetColor();
                        Console.WriteLine();
                    }
                }
                catch
                {
                    // Ignore parsing errors
                }
            }
        }
    }

    private static int GetTokenCount(Dictionary<string, object> usage, string key)
    {
        if (usage.TryGetValue(key, out var value))
        {
            if (value is System.Text.Json.JsonElement element)
                return element.GetInt32();
            if (value is int intValue)
                return intValue;
        }
        return 0;
    }

    /// <summary>
    /// Streaming mode showing partial responses.
    /// </summary>
    static async Task RunStreamingMode()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("         STREAMING MODE");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine();

        Console.Write("Enter your question: ");
        var question = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(question))
            return;

        Console.WriteLine();
        Console.WriteLine("🤖 Claude is responding (streaming)...");
        Console.WriteLine();

        var options = new ClaudeAgentOptions
        {
            IncludePartialMessages = true
        };

        try
        {
            await foreach (var message in ClaudeAgent.QueryAsync(question, options))
            {
                if (message is StreamEvent streamEvent)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.Write(".");
                    Console.ResetColor();
                }
                else if (message is AssistantMessage assistantMsg)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    foreach (var block in assistantMsg.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.WriteLine(textBlock.Text);
                        }
                    }
                    Console.ResetColor();
                }
                else if (message is ResultMessage resultMsg)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Stream completed - {resultMsg.NumTurns} turns");
                    Console.ResetColor();
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error: {ex}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Demo mode showing tool usage (file operations).
    /// </summary>
    static async Task RunToolsDemo()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("         TOOLS DEMO MODE");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("This demo allows Claude to use file tools.");
        Console.WriteLine("⚠️  Claude can read/write files in the current directory!");
        Console.WriteLine();

        Console.Write("Enter your request: ");
        var request = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(request))
            return;

        Console.WriteLine();
        Console.WriteLine("🔧 Claude is working with tools enabled...");
        Console.WriteLine();

        var options = new ClaudeAgentOptions
        {
            AllowedTools = new List<string> { "Read", "Write", "Edit", "Bash" },
            PermissionMode = PermissionMode.Default, // Will prompt for dangerous operations
            SystemPrompt = "You are a helpful assistant with access to file operations. Be careful and ask before making changes."
        };

        try
        {
            var toolUseCount = 0;

            await foreach (var message in ClaudeAgent.QueryAsync(request, options))
            {
                if (message is AssistantMessage assistantMsg)
                {
                    foreach (var block in assistantMsg.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine(textBlock.Text);
                            Console.ResetColor();
                        }
                        else if (block is ToolUseBlock toolUse)
                        {
                            toolUseCount++;
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"🔧 Using tool: {toolUse.Name}");
                            Console.ForegroundColor = ConsoleColor.DarkMagenta;
                            Console.WriteLine($"   Input: {System.Text.Json.JsonSerializer.Serialize(toolUse.Input)}");
                            Console.ResetColor();
                        }
                        else if (block is ToolResultBlock toolResult)
                        {
                            Console.ForegroundColor = toolResult.IsError == true ? ConsoleColor.Red : ConsoleColor.Green;
                            var contentPreview = toolResult.Content?.ToString() ?? "";
                            if (contentPreview.Length > 100)
                                contentPreview = contentPreview.Substring(0, 100) + "...";
                            Console.WriteLine($"   Result: {contentPreview}");
                            Console.ResetColor();
                        }
                    }
                }
                else if (message is ResultMessage resultMsg)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Completed with {toolUseCount} tool uses");
                    Console.WriteLine($"⏱️  Duration: {resultMsg.DurationMs}ms");
                    if (resultMsg.TotalCostUsd.HasValue)
                        Console.WriteLine($"💰 Cost: ${resultMsg.TotalCostUsd:F4}");
                    Console.ResetColor();
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error: {ex}");
            Console.ResetColor();
        }
    }
}
