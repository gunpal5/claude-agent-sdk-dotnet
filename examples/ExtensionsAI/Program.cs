using System.ComponentModel;
using ClaudeAgentSdk;
using Microsoft.Extensions.AI;

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║   Microsoft.Extensions.AI IChatClient for Claude Code     ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Choose example to run
Console.WriteLine("Select an example to run:");
Console.WriteLine("1. Basic Chat - Simple conversation");
Console.WriteLine("2. Custom Tools - AIFunction integration");
Console.WriteLine("3. App-Managed History - Full conversation context");
Console.WriteLine("4. Claude-Managed History - Let Claude handle context");
Console.WriteLine("5. Streaming Response - Real-time updates");
Console.WriteLine();
Console.Write("Enter choice (1-5): ");

var choice = Console.ReadLine();

switch (choice)
{
    case "1":
        await BasicChatExample();
        break;
    case "2":
        await CustomToolsExample();
        break;
    case "3":
        await AppManagedHistoryExample();
        break;
    case "4":
        await ClaudeManagedHistoryExample();
        break;
    case "5":
        await StreamingExample();
        break;
    default:
        Console.WriteLine("Invalid choice. Running basic example...");
        await BasicChatExample();
        break;
}

// Example 1: Basic Chat
static async Task BasicChatExample()
{
    Console.WriteLine("\n═══ Example 1: Basic Chat ═══\n");

    var options = new ClaudeCodeChatClientOptions
    {
        Model = "claude-sonnet-4",
        MaxTurns = 5
    };

    await using var client = new ClaudeCodeChatClient(options);

    var messages = new List<ChatMessage>
    {
        new(ChatRole.User, "What is 2+2? Explain your reasoning.")
    };

    Console.WriteLine("Sending: What is 2+2? Explain your reasoning.");
    Console.WriteLine();

    var response = await client.GetResponseAsync(messages);

    Console.WriteLine($"Model: {response.ModelId}");
    Console.WriteLine($"Response: {response.Text}");
    Console.WriteLine($"Tokens: {response.Usage?.TotalTokenCount ?? 0}");
    Console.WriteLine($"Finish Reason: {response.FinishReason}");
}

// Example 2: Custom Tools with AIFunction
static async Task CustomToolsExample()
{
    Console.WriteLine("\n═══ Example 2: Custom Tools (AIFunction) ═══\n");

    // Define custom tools using AIFunction
    var weatherTool = AIFunctionFactory.Create(
        ([Description("City name")] string city) =>
        {
            // Simulate weather API call
            return $"Weather in {city}: 72°F, Sunny, 10% humidity";
        },
        name: "GetWeather",
        description: "Gets current weather for a city");

    var databaseTool = AIFunctionFactory.Create(
        ([Description("SQL WHERE clause")] string query) =>
        {
            // Simulate database query
            return $"[{{\"id\": 1, \"name\": \"Alice\"}}, {{\"id\": 2, \"name\": \"Bob\"}}]";
        },
        name: "QueryDatabase",
        description: "Queries the user database");

    var calculatorTool = AIFunctionFactory.Create(
        ([Description("Mathematical expression")] string expression) =>
        {
            // Simple calculator
            var dataTable = new System.Data.DataTable();
            var result = dataTable.Compute(expression, "");
            return $"Result: {result}";
        },
        name: "Calculate",
        description: "Evaluates mathematical expressions");

    // Convert AIFunctions to MCP tools
    var aiFunctions = new[] { weatherTool, databaseTool, calculatorTool };

    var options = new ClaudeCodeChatClientOptions()
        .WithAIFunctionTools(aiFunctions, "my-tools", disableBuiltInTools: true)
        .WithModel("claude-sonnet-4")
        .WithMaxTurns(10);

    await using var client = new ClaudeCodeChatClient(options);

    var messages = new List<ChatMessage>
    {
        new(ChatRole.User, "What's the weather in Seattle? Also calculate 15 * 23.")
    };

    Console.WriteLine("Sending: What's the weather in Seattle? Also calculate 15 * 23.");
    Console.WriteLine();

    var response = await client.GetResponseAsync(messages);

    Console.WriteLine($"Response: {response.Text}");
    Console.WriteLine();
    Console.WriteLine($"Tools Available: {string.Join(", ", aiFunctions.Select(f => f.Name))}");
}

// Example 3: App-Managed History
static async Task AppManagedHistoryExample()
{
    Console.WriteLine("\n═══ Example 3: App-Managed History ═══\n");

    var options = new ClaudeCodeChatClientOptions()
        .WithConversationMode(ConversationMode.AppManaged)
        .WithModel("claude-sonnet-4");

    await using var client = new ClaudeCodeChatClient(options);

    // Multi-turn conversation
    var messages = new List<ChatMessage>
    {
        new(ChatRole.User, "My name is Alice and I love pizza.")
    };

    Console.WriteLine("Turn 1: My name is Alice and I love pizza.");
    var response1 = await client.GetResponseAsync(messages);
    Console.WriteLine($"Claude: {response1.Text}");
    Console.WriteLine();

    // Add response to history
    messages.AddRange(response1.Messages);

    // Second turn
    messages.Add(new ChatMessage(ChatRole.User, "What's my name and favorite food?"));
    Console.WriteLine("Turn 2: What's my name and favorite food?");
    var response2 = await client.GetResponseAsync(messages);
    Console.WriteLine($"Claude: {response2.Text}");
    Console.WriteLine();

    // Show conversation history
    Console.WriteLine($"Total messages in history: {client.ConversationHistory.Count}");
    Console.WriteLine("History:");
    foreach (var msg in client.ConversationHistory)
    {
        Console.WriteLine($"  {msg.Role}: {msg.Text?[..Math.Min(50, msg.Text?.Length ?? 0)]}...");
    }
}

// Example 4: Claude-Managed History
static async Task ClaudeManagedHistoryExample()
{
    Console.WriteLine("\n═══ Example 4: Claude-Managed History ═══\n");

    var options = new ClaudeCodeChatClientOptions()
        .WithConversationMode(ConversationMode.ClaudeCodeManaged)
        .WithModel("claude-sonnet-4");

    await using var client = new ClaudeCodeChatClient(options);

    Console.WriteLine($"Session ID: {client.SessionId}");
    Console.WriteLine();

    // First message
    var messages1 = new List<ChatMessage>
    {
        new(ChatRole.User, "Remember this: My favorite number is 42.")
    };

    Console.WriteLine("Turn 1: Remember this: My favorite number is 42.");
    var response1 = await client.GetResponseAsync(messages1);
    Console.WriteLine($"Claude: {response1.Text}");
    Console.WriteLine();

    // Second message - Claude remembers context
    var messages2 = new List<ChatMessage>
    {
        new(ChatRole.User, "What's my favorite number?")
    };

    Console.WriteLine("Turn 2: What's my favorite number?");
    var response2 = await client.GetResponseAsync(messages2);
    Console.WriteLine($"Claude: {response2.Text}");
    Console.WriteLine();

    Console.WriteLine("Note: Claude Code managed the conversation history internally.");
}

// Example 5: Streaming Response
static async Task StreamingExample()
{
    Console.WriteLine("\n═══ Example 5: Streaming Response ═══\n");

    var options = new ClaudeCodeChatClientOptions()
        .WithModel("claude-sonnet-4")
        .WithPartialMessages(true);

    await using var client = new ClaudeCodeChatClient(options);

    var messages = new List<ChatMessage>
    {
        new(ChatRole.User, "Write a short poem about programming.")
    };

    Console.WriteLine("Sending: Write a short poem about programming.");
    Console.WriteLine("\nStreaming response:");
    Console.WriteLine("─────────────────────");

    await foreach (var update in client.GetStreamingResponseAsync(messages))
    {
        if (update.Role == ChatRole.Assistant)
        {
            foreach (var content in update.Contents)
            {
                if (content is TextContent text)
                {
                    Console.Write(text.Text);
                }
            }
        }

        if (update.FinishReason != null)
        {
            Console.WriteLine();
            Console.WriteLine("─────────────────────");
            Console.WriteLine($"Finish Reason: {update.FinishReason}");

            var usageContent = update.Contents.OfType<UsageContent>().FirstOrDefault();
            if (usageContent?.Details != null)
            {
                Console.WriteLine($"Total Tokens: {usageContent.Details.TotalTokenCount}");
            }
        }
    }
}

Console.WriteLine("\n\nPress any key to exit...");
Console.ReadKey();
