using System.Text.Json;
using ClaudeAgentSdk;

namespace ClaudeChat;

/// <summary>
/// Example showing how to track todo tasks and token usage statistics.
/// </summary>
public class TodoAndUsageExample
{
    public static async Task Run()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     Todo Tasks & Token Usage Tracking Demo                ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var options = new ClaudeAgentOptions
        {
            AllowedTools = new List<string> { "Read", "Write" },
            PermissionMode = PermissionMode.Default,
            MaxTurns = 10
        };

        Console.Write("Enter your request: ");
        var prompt = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(prompt))
            prompt = "Create a simple hello world web server in Python";

        Console.WriteLine();
        Console.WriteLine("🚀 Processing your request...");
        Console.WriteLine();

        var stats = new SessionStatistics();

        try
        {
            await foreach (var message in ClaudeAgent.QueryAsync(prompt, options))
            {
                switch (message)
                {
                    case SystemMessage sysMsg:
                        HandleSystemMessage(sysMsg, stats);
                        break;

                    case AssistantMessage assistantMsg:
                        HandleAssistantMessage(assistantMsg, stats);
                        break;

                    case ResultMessage resultMsg:
                        HandleResultMessage(resultMsg, stats);
                        break;

                    case StreamEvent streamEvent:
                        // Streaming updates (if enabled)
                        Console.Write(".");
                        break;
                }
            }

            // Print final statistics
            PrintFinalStatistics(stats);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static void HandleSystemMessage(SystemMessage sysMsg, SessionStatistics stats)
    {
        switch (sysMsg.Subtype)
        {
            case "todo":
                // Todo list update
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n📋 Todo List Updated:");
                Console.ResetColor();

                if (sysMsg.Data.TryGetValue("todos", out var todosObj))
                {
                    try
                    {
                        // Parse todos from the data
                        var todosJson = JsonSerializer.Serialize(todosObj);
                        var todos = JsonSerializer.Deserialize<List<TodoItem>>(todosJson);

                        if (todos != null)
                        {
                            stats.TodoItems = todos;

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

                            // Show progress
                            var completed = todos.Count(t => t.Status == "completed");
                            var total = todos.Count;
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.WriteLine($"  Progress: {completed}/{total} completed");
                            Console.ResetColor();
                        }
                    }
                    catch
                    {
                        // Fallback: just show raw data
                        Console.WriteLine($"  Raw data: {JsonSerializer.Serialize(todosObj)}");
                    }
                }
                Console.WriteLine();
                break;

            case "notification":
                // Other system notifications
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"ℹ️  {sysMsg.Data.GetValueOrDefault("message", "Notification")}");
                Console.ResetColor();
                break;
        }
    }

    private static void HandleAssistantMessage(AssistantMessage assistantMsg, SessionStatistics stats)
    {
        stats.AssistantMessages++;

        foreach (var block in assistantMsg.Content)
        {
            switch (block)
            {
                case TextBlock textBlock:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"💬 Claude: {textBlock.Text}");
                    Console.ResetColor();
                    stats.TextBlocks++;
                    break;

                case ThinkingBlock thinkingBlock:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    var preview = thinkingBlock.Thinking.Length > 80
                        ? thinkingBlock.Thinking.Substring(0, 80) + "..."
                        : thinkingBlock.Thinking;
                    Console.WriteLine($"💭 [Thinking: {preview}]");
                    Console.ResetColor();
                    stats.ThinkingBlocks++;
                    break;

                case ToolUseBlock toolUse:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"🔧 Using tool: {toolUse.Name}");
                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    Console.WriteLine($"   Input: {JsonSerializer.Serialize(toolUse.Input)}");
                    Console.ResetColor();
                    stats.ToolUses++;
                    break;

                case ToolResultBlock toolResult:
                    var resultColor = toolResult.IsError == true
                        ? ConsoleColor.Red
                        : ConsoleColor.Green;
                    Console.ForegroundColor = resultColor;
                    var resultPreview = toolResult.Content?.ToString() ?? "";
                    if (resultPreview.Length > 100)
                        resultPreview = resultPreview.Substring(0, 100) + "...";
                    Console.WriteLine($"   Result: {resultPreview}");
                    Console.ResetColor();
                    stats.ToolResults++;
                    break;
            }
        }
    }

    private static void HandleResultMessage(ResultMessage resultMsg, SessionStatistics stats)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("         SESSION COMPLETE");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine();

        // Timing metrics
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("⏱️  Timing:");
        Console.ResetColor();
        Console.WriteLine($"   Total Duration:    {resultMsg.DurationMs}ms");
        Console.WriteLine($"   API Duration:      {resultMsg.DurationApiMs}ms");
        Console.WriteLine($"   Local Processing:  {resultMsg.DurationMs - resultMsg.DurationApiMs}ms");
        Console.WriteLine();

        // Cost metrics
        if (resultMsg.TotalCostUsd.HasValue && resultMsg.TotalCostUsd > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("💰 Cost:");
            Console.ResetColor();
            Console.WriteLine($"   Total Cost:        ${resultMsg.TotalCostUsd:F6}");
            Console.WriteLine();
        }

        // Token usage
        if (resultMsg.Usage != null && resultMsg.Usage.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("📊 Token Usage:");
            Console.ResetColor();

            var usage = resultMsg.Usage;

            // Parse token counts
            var inputTokens = GetTokenCount(usage, "input_tokens");
            var outputTokens = GetTokenCount(usage, "output_tokens");
            var cacheCreationTokens = GetTokenCount(usage, "cache_creation_input_tokens");
            var cacheReadTokens = GetTokenCount(usage, "cache_read_input_tokens");

            Console.WriteLine($"   Input Tokens:            {inputTokens:N0}");
            Console.WriteLine($"   Output Tokens:           {outputTokens:N0}");

            if (cacheCreationTokens > 0)
                Console.WriteLine($"   Cache Creation Tokens:   {cacheCreationTokens:N0}");
            if (cacheReadTokens > 0)
                Console.WriteLine($"   Cache Read Tokens:       {cacheReadTokens:N0}");

            var totalTokens = inputTokens + outputTokens;
            Console.WriteLine($"   Total Tokens:            {totalTokens:N0}");

            // Token efficiency
            if (inputTokens > 0)
            {
                var outputRatio = (double)outputTokens / inputTokens;
                Console.WriteLine($"   Output/Input Ratio:      {outputRatio:F2}x");
            }

            Console.WriteLine();

            // All usage details
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("   Raw Usage Data:");
            foreach (var (key, value) in usage)
            {
                Console.WriteLine($"      {key}: {value}");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        // Conversation metrics
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("🔄 Conversation:");
        Console.ResetColor();
        Console.WriteLine($"   Number of Turns:   {resultMsg.NumTurns}");
        Console.WriteLine($"   Session ID:        {resultMsg.SessionId}");
        Console.WriteLine($"   Error Status:      {(resultMsg.IsError ? "❌ Error" : "✓ Success")}");
        if (!string.IsNullOrEmpty(resultMsg.Result))
            Console.WriteLine($"   Result:            {resultMsg.Result}");
        Console.WriteLine();

        // Store in stats
        stats.ResultMessage = resultMsg;
    }

    private static void PrintFinalStatistics(SessionStatistics stats)
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("      FINAL STATISTICS");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("📈 Activity Summary:");
        Console.ResetColor();
        Console.WriteLine($"   Assistant Messages:  {stats.AssistantMessages}");
        Console.WriteLine($"   Text Blocks:         {stats.TextBlocks}");
        Console.WriteLine($"   Thinking Blocks:     {stats.ThinkingBlocks}");
        Console.WriteLine($"   Tool Uses:           {stats.ToolUses}");
        Console.WriteLine($"   Tool Results:        {stats.ToolResults}");
        Console.WriteLine();

        // Todo summary
        if (stats.TodoItems.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("📋 Final Todo Status:");
            Console.ResetColor();

            var completed = stats.TodoItems.Count(t => t.Status == "completed");
            var inProgress = stats.TodoItems.Count(t => t.Status == "in_progress");
            var pending = stats.TodoItems.Count(t => t.Status == "pending");
            var total = stats.TodoItems.Count;

            Console.WriteLine($"   ✓ Completed:   {completed}/{total} ({(double)completed/total*100:F0}%)");
            Console.WriteLine($"   ⟳ In Progress: {inProgress}/{total}");
            Console.WriteLine($"   ○ Pending:     {pending}/{total}");
            Console.WriteLine();
        }

        // Cost summary
        if (stats.ResultMessage?.TotalCostUsd > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("💵 Cost Analysis:");
            Console.ResetColor();

            var costPerTurn = stats.ResultMessage.TotalCostUsd.Value / stats.ResultMessage.NumTurns;
            Console.WriteLine($"   Total Cost:        ${stats.ResultMessage.TotalCostUsd:F6}");
            Console.WriteLine($"   Cost per Turn:     ${costPerTurn:F6}");
            Console.WriteLine();
        }
    }

    private static int GetTokenCount(Dictionary<string, object> usage, string key)
    {
        if (usage.TryGetValue(key, out var value))
        {
            if (value is JsonElement element)
                return element.GetInt32();
            if (value is int intValue)
                return intValue;
        }
        return 0;
    }
}

/// <summary>
/// Represents a todo item from Claude.
/// </summary>
public class TodoItem
{
    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("activeForm")]
    public string ActiveForm { get; set; } = "";
}

/// <summary>
/// Tracks statistics during a session.
/// </summary>
public class SessionStatistics
{
    public int AssistantMessages { get; set; }
    public int TextBlocks { get; set; }
    public int ThinkingBlocks { get; set; }
    public int ToolUses { get; set; }
    public int ToolResults { get; set; }
    public List<TodoItem> TodoItems { get; set; } = new();
    public ResultMessage? ResultMessage { get; set; }
}
