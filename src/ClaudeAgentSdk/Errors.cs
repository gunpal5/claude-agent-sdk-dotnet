namespace ClaudeAgentSdk;

/// <summary>
/// Base exception for all Claude SDK errors.
/// </summary>
public class ClaudeSdkException : Exception
{
    public ClaudeSdkException(string message) : base(message)
    {
    }

    public ClaudeSdkException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when Claude Code CLI is not found.
/// </summary>
public class CliNotFoundException : ClaudeSdkException
{
    public CliNotFoundException(string message) : base(message)
    {
    }

    public CliNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when connection to Claude Code fails.
/// </summary>
public class CliConnectionException : ClaudeSdkException
{
    public CliConnectionException(string message) : base(message)
    {
    }

    public CliConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when Claude Code process exits with error.
/// </summary>
public class ProcessException : ClaudeSdkException
{
    public int ExitCode { get; }
    public string? Stderr { get; }

    public ProcessException(string message, int exitCode, string? stderr = null) : base(message)
    {
        ExitCode = exitCode;
        Stderr = stderr;
    }

    public ProcessException(string message, int exitCode, Exception innerException) : base(message, innerException)
    {
        ExitCode = exitCode;
    }
}

/// <summary>
/// Exception thrown when JSON parsing fails.
/// </summary>
public class CliJsonDecodeException : ClaudeSdkException
{
    public CliJsonDecodeException(string message) : base(message)
    {
    }

    public CliJsonDecodeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
