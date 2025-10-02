using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk;

/// <summary>
/// Query Claude Code for one-shot or unidirectional streaming interactions.
/// </summary>
public static class ClaudeAgent
{
    /// <summary>
    /// Query Claude Code for one-shot or unidirectional streaming interactions.
    ///
    /// This function is ideal for simple, stateless queries where you don't need
    /// bidirectional communication or conversation management. For interactive,
    /// stateful conversations, use ClaudeSdkClient instead.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude. Can be a string for single-shot queries.</param>
    /// <param name="options">Optional configuration (defaults to ClaudeAgentOptions() if null).</param>
    /// <param name="transport">Optional transport implementation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of messages from the conversation.</returns>
    public static async IAsyncEnumerable<Message> QueryAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        ITransport? transport = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ClaudeAgentOptions();
        Environment.SetEnvironmentVariable("CLAUDE_CODE_ENTRYPOINT", "sdk-dotnet");

        transport ??= new SubprocessCliTransport(prompt, options);

        await using (transport)
        {
            await transport.ConnectAsync(cancellationToken);

            await foreach (var data in transport.ReadMessagesAsync(cancellationToken))
            {
                var message = MessageParser.ParseMessage(data);
                yield return message;
            }
        }
    }
}
