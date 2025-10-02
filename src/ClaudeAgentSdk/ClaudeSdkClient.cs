using System.Text.Json;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk;

/// <summary>
/// Client for bidirectional, interactive conversations with Claude Code.
///
/// This client provides full control over the conversation flow with support
/// for streaming, interrupts, and dynamic message sending. For simple one-shot
/// queries, consider using the ClaudeAgent.QueryAsync() function instead.
/// </summary>
public class ClaudeSdkClient : IAsyncDisposable
{
    private readonly ClaudeAgentOptions _options;
    private readonly ITransport? _customTransport;
    private ITransport? _transport;
    private bool _connected;

    public ClaudeSdkClient(ClaudeAgentOptions? options = null, ITransport? transport = null)
    {
        _options = options ?? new ClaudeAgentOptions();
        _customTransport = transport;
        Environment.SetEnvironmentVariable("CLAUDE_CODE_ENTRYPOINT", "sdk-dotnet-client");
    }

    /// <summary>
    /// Connect to Claude with an optional initial prompt.
    /// </summary>
    public async Task ConnectAsync(string? prompt = null, CancellationToken cancellationToken = default)
    {
        if (_connected)
            return;

        // Use empty array to indicate streaming mode (interactive sessions)
        // If prompt is provided, use it directly for one-shot mode
        object actualPrompt = prompt != null ? (object)prompt : Array.Empty<object>();

        // Validate permission settings
        var options = _options;
        if (_options.CanUseTool != null)
        {
            if (_options.PermissionPromptToolName != null)
            {
                throw new ArgumentException(
                    "CanUseTool callback cannot be used with PermissionPromptToolName. " +
                    "Please use one or the other.");
            }

            options = new ClaudeAgentOptions
            {
                AllowedTools = _options.AllowedTools,
                SystemPrompt = _options.SystemPrompt,
                McpServers = _options.McpServers,
                PermissionMode = _options.PermissionMode,
                ContinueConversation = _options.ContinueConversation,
                Resume = _options.Resume,
                MaxTurns = _options.MaxTurns,
                DisallowedTools = _options.DisallowedTools,
                Model = _options.Model,
                PermissionPromptToolName = "stdio",
                Cwd = _options.Cwd,
                Settings = _options.Settings,
                AddDirs = _options.AddDirs,
                Env = _options.Env,
                ExtraArgs = _options.ExtraArgs,
                MaxBufferSize = _options.MaxBufferSize,
                Stderr = _options.Stderr,
                CanUseTool = _options.CanUseTool,
                Hooks = _options.Hooks,
                User = _options.User,
                IncludePartialMessages = _options.IncludePartialMessages,
                ForkSession = _options.ForkSession,
                Agents = _options.Agents,
                SettingSources = _options.SettingSources
            };
        }

        _transport = _customTransport ?? new SubprocessCliTransport(actualPrompt, options);
        await _transport.ConnectAsync(cancellationToken);
        _connected = true;
    }

    /// <summary>
    /// Receive all messages from Claude.
    /// </summary>
    public async IAsyncEnumerable<Message> ReceiveMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_connected || _transport == null)
            throw new CliConnectionException("Not connected. Call ConnectAsync() first.");

        await foreach (var data in _transport.ReadMessagesAsync(cancellationToken))
        {
            yield return MessageParser.ParseMessage(data);
        }
    }

    /// <summary>
    /// Send a new request in streaming mode.
    /// </summary>
    public async Task QueryAsync(string prompt, string sessionId = "default", CancellationToken cancellationToken = default)
    {
        if (!_connected || _transport == null)
            throw new CliConnectionException("Not connected. Call ConnectAsync() first.");

        var message = new
        {
            type = "user",
            message = new
            {
                role = "user",
                content = prompt
            },
            parent_tool_use_id = (string?)null,
            session_id = sessionId
        };

        var json = JsonSerializer.Serialize(message) + "\n";
        await _transport.WriteAsync(json, cancellationToken);
    }

    /// <summary>
    /// Receive messages from Claude until and including a ResultMessage.
    ///
    /// This async iterator yields all messages in sequence and automatically terminates
    /// after yielding a ResultMessage (which indicates the response is complete).
    /// </summary>
    public async IAsyncEnumerable<Message> ReceiveResponseAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReceiveMessagesAsync(cancellationToken))
        {
            yield return message;
            if (message is ResultMessage)
                yield break;
        }
    }

    /// <summary>
    /// Disconnect from Claude.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_transport != null)
        {
            await _transport.DisposeAsync();
            _transport = null;
        }
        _connected = false;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
