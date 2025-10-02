namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Transport interface for communicating with Claude Code.
/// </summary>
public interface ITransport : IAsyncDisposable
{
    /// <summary>
    /// Connect to the transport.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Write data to the transport.
    /// </summary>
    Task WriteAsync(string data, CancellationToken cancellationToken = default);

    /// <summary>
    /// End the input stream.
    /// </summary>
    Task EndInputAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Read messages from the transport.
    /// </summary>
    IAsyncEnumerable<Dictionary<string, object>> ReadMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if transport is ready for communication.
    /// </summary>
    bool IsReady { get; }
}
