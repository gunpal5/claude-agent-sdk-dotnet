using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ClaudeAgentSdk;

/// <summary>
/// Conversation mode for ClaudeCodeChatClient.
/// </summary>
public enum ConversationMode
{
    /// <summary>
    /// Claude Code manages conversation history internally.
    /// Use ContinueConversation and Resume options.
    /// </summary>
    ClaudeCodeManaged,

    /// <summary>
    /// Application manages conversation history.
    /// Client stores full ChatMessage history and sends only latest prompt.
    /// </summary>
    AppManaged
}

/// <summary>
/// Options for configuring ClaudeCodeChatClient.
/// </summary>
public class ClaudeCodeChatClientOptions
{
    /// <summary>
    /// Conversation history management mode. Default is AppManaged.
    /// </summary>
    public ConversationMode ConversationMode { get; set; } = ConversationMode.AppManaged;

    /// <summary>
    /// MCP servers providing custom tools. Use AIFunctionMcpExtensions to convert AIFunctions.
    /// </summary>
    public Dictionary<string, object>? McpServers { get; set; }

    /// <summary>
    /// List of built-in tools to disable.
    /// </summary>
    public List<string> DisallowedTools { get; set; } = new();

    /// <summary>
    /// List of built-in tools to allow (empty = all allowed).
    /// </summary>
    public List<string> AllowedTools { get; set; } = new();

    /// <summary>
    /// Claude model to use.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Permission mode for tool execution.
    /// </summary>
    public PermissionMode? PermissionMode { get; set; }

    /// <summary>
    /// System prompt or SystemPromptPreset.
    /// </summary>
    public object? SystemPrompt { get; set; }

    /// <summary>
    /// Maximum number of turns in a conversation.
    /// </summary>
    public int? MaxTurns { get; set; }

    /// <summary>
    /// Working directory for Claude Code.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Include partial messages for streaming updates.
    /// </summary>
    public bool IncludePartialMessages { get; set; }

    /// <summary>
    /// Resume a previous session by session ID (ClaudeCodeManaged mode only).
    /// </summary>
    public string? Resume { get; set; }
}

/// <summary>
/// Microsoft.Extensions.AI IChatClient implementation for Claude Code.
/// Provides seamless integration with the Microsoft AI ecosystem while maintaining
/// full access to Claude Code's powerful features including file operations,
/// code editing, and tool execution.
/// </summary>
public class ClaudeCodeChatClient : IChatClient, IAsyncDisposable
{
    private readonly ClaudeSdkClient _client;
    private readonly ClaudeCodeChatClientOptions _options;
    private readonly List<ChatMessage> _conversationHistory = new();
    private readonly string _sessionId;
    private readonly ChatClientMetadata _metadata;
    private bool _isConnected;
    private DynamicAIFunctionMcpServer? _mcpServer;

    /// <summary>
    /// Creates a new ClaudeCodeChatClient with the specified options.
    /// </summary>
    /// <param name="options">Configuration options for the client.</param>
    public ClaudeCodeChatClient(ClaudeCodeChatClientOptions? options = null)
    {
        _options = options ?? new ClaudeCodeChatClientOptions();
        _sessionId = Guid.NewGuid().ToString();

        _metadata = new ChatClientMetadata(
            providerName: "ClaudeCode",
            providerUri: new Uri("https://claude.ai/code"));

        // Capture the MCP server instance if present for dynamic tool management
        if (_options.McpServers != null)
        {
            foreach (var server in _options.McpServers.Values)
            {
                if (server is McpSdkServerConfig sdkConfig && sdkConfig.Instance is DynamicAIFunctionMcpServer mcpServer)
                {
                    _mcpServer = mcpServer;
                    break; // Use the first DynamicAIFunctionMcpServer found
                }
            }
        }

        var sdkOptions = new ClaudeAgentOptions
        {
            ContinueConversation = _options.ConversationMode == ConversationMode.ClaudeCodeManaged,
            Resume = _options.Resume,
            McpServers = _options.McpServers,
            DisallowedTools = _options.DisallowedTools,
            AllowedTools = _options.AllowedTools,
            Model = _options.Model,
            PermissionMode = _options.PermissionMode,
            SystemPrompt = _options.SystemPrompt,
            MaxTurns = _options.MaxTurns,
            Cwd = _options.Cwd,
            IncludePartialMessages = _options.IncludePartialMessages
        };

        _client = new ClaudeSdkClient(sdkOptions);
    }

    /// <summary>
    /// Gets the conversation history (AppManaged mode only).
    /// </summary>
    public IReadOnlyList<ChatMessage> ConversationHistory => _conversationHistory.AsReadOnly();

    /// <summary>
    /// Gets the current session ID.
    /// </summary>
    public string SessionId => _sessionId;

    /// <summary>
    /// Gets metadata about the chat client.
    /// </summary>
    public ChatClientMetadata Metadata => _metadata;

    /// <summary>
    /// Retrieves a service from the client or its underlying components.
    /// </summary>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(ChatClientMetadata))
        {
            return Metadata;
        }

        if (serviceType == typeof(ClaudeAgentOptions))
        {
            return _client.GetType()
                .GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_client);
        }

        if (serviceType == typeof(IReadOnlyList<ChatMessage>) &&
            _options.ConversationMode == ConversationMode.AppManaged)
        {
            return _conversationHistory.AsReadOnly();
        }

        return null;
    }

    /// <summary>
    /// Sends chat messages and returns the complete response.
    /// </summary>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        // Handle per-request tools from ChatOptions
        // NOTE: Per-request tools are currently NOT supported with Claude Code CLI.
        // The CLI process is started once with a fixed set of tools, and dynamically adding
        // tools after startup is not possible. Tools must be configured at the client level
        // using ClaudeCodeChatClientOptions.WithAIFunctionTools().
        //
        // This code is kept for potential future support when/if Claude CLI allows dynamic
        // tool registration, but for now it will not work as expected.
        List<string>? addedToolNames = null;
        if (options?.Tools != null && options.Tools.Count > 0)
        {
            // Warn that this is not currently supported
            Console.WriteLine("[WARNING] ChatOptions.Tools is not currently supported with Claude Code CLI. " +
                            "Tools must be configured at client initialization using WithAIFunctionTools(). " +
                            "Per-request tools will be ignored.");
        }

        try
        {
            // Store messages in history if app-managed
            if (_options.ConversationMode == ConversationMode.AppManaged)
            {
                foreach (var msg in chatMessages)
                {
                    if (!_conversationHistory.Any(h => h.Text == msg.Text && h.Role == h.Role))
                    {
                        _conversationHistory.Add(msg);
                    }
                }
            }

            // Get the latest user message
            var lastMessage = chatMessages.LastOrDefault(m => m.Role == ChatRole.User);
            if (lastMessage?.Text is not { } prompt)
            {
                throw new ArgumentException("No user message found in chat messages");
            }

            // Send query to Claude Code
            await _client.QueryAsync(prompt, _sessionId, cancellationToken);

        // Collect response
        var assistantContents = new List<AIContent>();
        ChatFinishReason? finishReason = null;
        UsageDetails? usage = null;
        var additionalProps = new AdditionalPropertiesDictionary();
        string? modelId = _options.Model ?? "claude-sonnet-4";

        await foreach (var msg in _client.ReceiveResponseAsync(cancellationToken))
        {
            switch (msg)
            {
                case ControlRequest:
                    // Skip control requests - these are internal Claude Code protocol messages
                    // for MCP tool operations handled transparently
                    continue;

                case AssistantMessage assistant:
                    foreach (var block in assistant.Content)
                    {
                        assistantContents.Add(ConvertContentBlock(block));
                    }
                    modelId = assistant.Model;
                    break;

                case ResultMessage result:
                    finishReason = result.IsError
                        ? ChatFinishReason.ContentFilter // Use ContentFilter instead of Error
                        : ChatFinishReason.Stop;

                    if (result.Usage != null)
                    {
                        usage = new UsageDetails
                        {
                            InputTokenCount = (int?)GetTokenCount(result.Usage, "input_tokens"),
                            OutputTokenCount = (int?)GetTokenCount(result.Usage, "output_tokens"),
                            TotalTokenCount = (int?)(GetTokenCount(result.Usage, "input_tokens") +
                                            GetTokenCount(result.Usage, "output_tokens"))
                        };
                    }

                    additionalProps["DurationMs"] = result.DurationMs;
                    additionalProps["DurationApiMs"] = result.DurationApiMs;
                    additionalProps["NumTurns"] = result.NumTurns;
                    additionalProps["SessionId"] = result.SessionId;

                    if (result.TotalCostUsd.HasValue)
                    {
                        additionalProps["TotalCostUsd"] = result.TotalCostUsd.Value;
                    }
                    break;
            }
        }

            var assistantMessage = new ChatMessage(ChatRole.Assistant, assistantContents);

            // Store assistant response in history if app-managed
            if (_options.ConversationMode == ConversationMode.AppManaged)
            {
                _conversationHistory.Add(assistantMessage);
            }

            return new ChatResponse(assistantMessage)
            {
                CreatedAt = DateTimeOffset.UtcNow,
                ModelId = modelId,
                FinishReason = finishReason,
                Usage = usage,
                AdditionalProperties = additionalProps
            };
        }
        finally
        {
            // Clean up per-request tools
            if (addedToolNames != null && addedToolNames.Count > 0 && _mcpServer != null)
            {
                _mcpServer.RemoveTools(addedToolNames);
            }
        }
    }

    /// <summary>
    /// Sends chat messages and streams the response updates.
    /// </summary>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        var lastMessage = chatMessages.LastOrDefault(m => m.Role == ChatRole.User);
        if (lastMessage?.Text is not { } prompt)
        {
            throw new ArgumentException("No user message found in chat messages");
        }

        await _client.QueryAsync(prompt, _sessionId, cancellationToken);

        await foreach (var msg in _client.ReceiveMessagesAsync(cancellationToken))
        {
            switch (msg)
            {
                case ControlRequest:
                    // Skip control requests - these are internal Claude Code protocol messages
                    // for MCP tool operations handled transparently
                    continue;

                case AssistantMessage assistant:
                    var contents = new List<AIContent>();
                    foreach (var block in assistant.Content)
                    {
                        contents.Add(ConvertContentBlock(block));
                    }

                    var update = new ChatResponseUpdate
                    {
                        Role = ChatRole.Assistant,
                        Contents = contents,
                        CreatedAt = DateTimeOffset.UtcNow,
                        ModelId = assistant.Model
                    };

                    yield return update;
                    break;

                case StreamEvent streamEvent:
                    // Handle partial streaming updates if enabled
                    if (_options.IncludePartialMessages)
                    {
                        var additionalProps = new AdditionalPropertiesDictionary
                        {
                            ["SessionId"] = streamEvent.SessionId,
                            ["Event"] = streamEvent.Event
                        };

                        yield return new ChatResponseUpdate
                        {
                            Role = ChatRole.Assistant,
                            AdditionalProperties = additionalProps
                        };
                    }
                    break;

                case ResultMessage result:
                    UsageDetails? usage = null;
                    if (result.Usage != null)
                    {
                        usage = new UsageDetails
                        {
                            InputTokenCount = (int?)GetTokenCount(result.Usage, "input_tokens"),
                            OutputTokenCount = (int?)GetTokenCount(result.Usage, "output_tokens"),
                            TotalTokenCount = (int?)(GetTokenCount(result.Usage, "input_tokens") +
                                            GetTokenCount(result.Usage, "output_tokens"))
                        };
                    }

                    var finalUpdate = new ChatResponseUpdate
                    {
                        FinishReason = result.IsError ? ChatFinishReason.ContentFilter : ChatFinishReason.Stop,
                        Contents = usage != null ? new List<AIContent> { new UsageContent(usage) } : new List<AIContent>()
                    };

                    yield return finalUpdate;
                    yield break;
            }
        }
    }

    /// <summary>
    /// Disposes the client and releases resources.
    /// </summary>
    public void Dispose()
    {
        _client.DisconnectAsync().Wait();
    }

    /// <summary>
    /// Asynchronously disposes the client and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisconnectAsync();
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (!_isConnected)
        {
            await _client.ConnectAsync(cancellationToken: cancellationToken);
            _isConnected = true;
        }
    }

    private static AIContent ConvertContentBlock(object block)
    {
        return block switch
        {
            TextBlock text => new TextContent(text.Text),
            ThinkingBlock thinking => new TextContent(thinking.Thinking)
            {
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["Type"] = "thinking",
                    ["Signature"] = thinking.Signature
                }
            },
            ToolUseBlock tool => new FunctionCallContent(
                callId: tool.Id,
                name: tool.Name,
                arguments: tool.Input != null ? tool.Input.ToDictionary(k => k.Key, k => (object?)k.Value) : null),
            ToolResultBlock result => new FunctionResultContent(
                callId: result.ToolUseId,
                result: result.Content),
            _ => throw new NotSupportedException($"Unsupported content block type: {block.GetType()}")
        };
    }

    private static long GetTokenCount(Dictionary<string, object> usage, string key)
    {
        if (usage.TryGetValue(key, out var value))
        {
            if (value is JsonElement element)
                return element.GetInt64();
            if (value is int intValue)
                return intValue;
            if (value is long longValue)
                return longValue;
        }
        return 0;
    }
}
