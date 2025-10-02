using ClaudeAgentSdk;

namespace QuickStart;

class Program
{
    static async Task Main(string[] args)
    {
        await BasicExample();
        await WithOptionsExample();
        await WithToolsExample();
    }

    static async Task BasicExample()
    {
        Console.WriteLine("=== Basic Example ===");

        await foreach (var message in ClaudeAgent.QueryAsync("What is 2 + 2?"))
        {
            if (message is AssistantMessage assistantMsg)
            {
                foreach (var block in assistantMsg.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                    }
                }
            }
        }
        Console.WriteLine();
    }

    static async Task WithOptionsExample()
    {
        Console.WriteLine("=== With Options Example ===");

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = "You are a helpful assistant that explains things simply.",
            MaxTurns = 1
        };

        await foreach (var message in ClaudeAgent.QueryAsync(
            "Explain what C# is in one sentence.",
            options))
        {
            if (message is AssistantMessage assistantMsg)
            {
                foreach (var block in assistantMsg.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                    }
                }
            }
        }
        Console.WriteLine();
    }

    static async Task WithToolsExample()
    {
        Console.WriteLine("=== With Tools Example ===");

        var options = new ClaudeAgentOptions
        {
            AllowedTools = new List<string> { "Read", "Write" },
            SystemPrompt = "You are a helpful file assistant."
        };

        await foreach (var message in ClaudeAgent.QueryAsync(
            "Create a file called hello.txt with 'Hello, World!' in it",
            options))
        {
            if (message is AssistantMessage assistantMsg)
            {
                foreach (var block in assistantMsg.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                    }
                }
            }
            else if (message is ResultMessage resultMsg && resultMsg.TotalCostUsd > 0)
            {
                Console.WriteLine($"\nCost: ${resultMsg.TotalCostUsd:F4}");
            }
        }
        Console.WriteLine();
    }
}
