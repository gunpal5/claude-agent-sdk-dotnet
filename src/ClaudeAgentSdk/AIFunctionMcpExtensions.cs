using Microsoft.Extensions.AI;

namespace ClaudeAgentSdk;

/// <summary>
/// Extension methods for easy conversion of AIFunctions to MCP tools
/// and integration with ClaudeCodeChatClient.
/// </summary>
public static class AIFunctionMcpExtensions
{
    /// <summary>
    /// Converts a collection of AIFunctions to an MCP server configuration.
    /// </summary>
    /// <param name="aiFunctions">The AI functions to convert.</param>
    /// <param name="serverName">Name for the MCP server. Default is "ai-functions".</param>
    /// <returns>MCP SDK server configuration ready to use with ClaudeCodeChatClient.</returns>
    /// <example>
    /// <code>
    /// var functions = new[] { myFunction1, myFunction2 };
    /// var mcpServer = functions.ToMcpServer("my-tools");
    /// </code>
    /// </example>
    public static McpSdkServerConfig ToMcpServer(
        this IEnumerable<AIFunction> aiFunctions,
        string serverName = "ai-functions")
    {
        var converter = new AIFunctionMcpConverter(aiFunctions, serverName);
        return converter.CreateMcpServerConfig();
    }

    /// <summary>
    /// Adds AIFunctions as MCP tools to ClaudeCodeChatClientOptions.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="aiFunctions">The AI functions to add as tools.</param>
    /// <param name="serverName">Name for the MCP server. Default is "ai-functions".</param>
    /// <param name="disableBuiltInTools">Whether to disable all built-in Claude Code tools. Default is true.</param>
    /// <returns>The configured options for method chaining.</returns>
    /// <example>
    /// <code>
    /// var options = new ClaudeCodeChatClientOptions()
    ///     .WithAIFunctionTools(myFunctions);
    ///
    /// var client = new ClaudeCodeChatClient(options);
    /// </code>
    /// </example>
    public static ClaudeCodeChatClientOptions WithAIFunctionTools(
        this ClaudeCodeChatClientOptions options,
        IEnumerable<AIFunction> aiFunctions,
        string serverName = "ai-functions",
        bool disableBuiltInTools = true)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (aiFunctions == null)
            throw new ArgumentNullException(nameof(aiFunctions));

        var functionList = aiFunctions.ToList();

        options.McpServers ??= new Dictionary<string, object>();
        options.McpServers[serverName] = functionList.ToMcpServer(serverName);

        if (disableBuiltInTools)
        {
            // When disabling built-in tools, explicitly allow ONLY the MCP tools
            // Tool naming convention from Python SDK: mcp__<server_name>__<tool_name>
            options.AllowedTools = functionList
                .Select(f => $"mcp__{serverName}__{f.Metadata.Name}")
                .ToList();

            // Clear any disallowed tools to ensure MCP tools work
            options.DisallowedTools.Clear();
        }

        return options;
    }

    /// <summary>
    /// Sets the conversation mode for ClaudeCodeChatClientOptions.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="mode">The conversation mode to use.</param>
    /// <returns>The configured options for method chaining.</returns>
    /// <example>
    /// <code>
    /// var options = new ClaudeCodeChatClientOptions()
    ///     .WithConversationMode(ConversationMode.ClaudeCodeManaged);
    /// </code>
    /// </example>
    public static ClaudeCodeChatClientOptions WithConversationMode(
        this ClaudeCodeChatClientOptions options,
        ConversationMode mode)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        options.ConversationMode = mode;
        return options;
    }

    /// <summary>
    /// Sets the Claude model to use.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="model">The model identifier (e.g., "claude-sonnet-4").</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ClaudeCodeChatClientOptions WithModel(
        this ClaudeCodeChatClientOptions options,
        string model)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrEmpty(model))
            throw new ArgumentException("Model cannot be null or empty", nameof(model));

        options.Model = model;
        return options;
    }

    /// <summary>
    /// Sets the permission mode for tool execution.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="mode">The permission mode.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ClaudeCodeChatClientOptions WithPermissionMode(
        this ClaudeCodeChatClientOptions options,
        PermissionMode mode)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        options.PermissionMode = mode;
        return options;
    }

    /// <summary>
    /// Sets the system prompt for the chat client.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="systemPrompt">The system prompt text or SystemPromptPreset.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ClaudeCodeChatClientOptions WithSystemPrompt(
        this ClaudeCodeChatClientOptions options,
        object systemPrompt)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        options.SystemPrompt = systemPrompt;
        return options;
    }

    /// <summary>
    /// Sets the maximum number of turns in a conversation.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="maxTurns">Maximum number of conversation turns.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ClaudeCodeChatClientOptions WithMaxTurns(
        this ClaudeCodeChatClientOptions options,
        int maxTurns)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (maxTurns <= 0)
            throw new ArgumentException("Max turns must be positive", nameof(maxTurns));

        options.MaxTurns = maxTurns;
        return options;
    }

    /// <summary>
    /// Sets the working directory for Claude Code.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="cwd">The working directory path.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ClaudeCodeChatClientOptions WithWorkingDirectory(
        this ClaudeCodeChatClientOptions options,
        string cwd)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrEmpty(cwd))
            throw new ArgumentException("Working directory cannot be null or empty", nameof(cwd));

        options.Cwd = cwd;
        return options;
    }

    /// <summary>
    /// Enables or disables partial message streaming updates.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="includePartial">Whether to include partial messages.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ClaudeCodeChatClientOptions WithPartialMessages(
        this ClaudeCodeChatClientOptions options,
        bool includePartial = true)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        options.IncludePartialMessages = includePartial;
        return options;
    }

    /// <summary>
    /// Adds specific built-in tools to the allowed list.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="tools">The tool names to allow.</param>
    /// <returns>The configured options for method chaining.</returns>
    /// <example>
    /// <code>
    /// var options = new ClaudeCodeChatClientOptions()
    ///     .WithAllowedTools("Read", "Write");
    /// </code>
    /// </example>
    public static ClaudeCodeChatClientOptions WithAllowedTools(
        this ClaudeCodeChatClientOptions options,
        params string[] tools)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (tools == null)
            throw new ArgumentNullException(nameof(tools));

        options.AllowedTools.AddRange(tools);
        return options;
    }

    /// <summary>
    /// Adds specific built-in tools to the disallowed list.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="tools">The tool names to disallow.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ClaudeCodeChatClientOptions WithDisallowedTools(
        this ClaudeCodeChatClientOptions options,
        params string[] tools)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (tools == null)
            throw new ArgumentNullException(nameof(tools));

        options.DisallowedTools.AddRange(tools);
        return options;
    }

    /// <summary>
    /// Resumes a previous Claude Code session.
    /// Only applicable in ClaudeCodeManaged conversation mode.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="sessionId">The session ID to resume.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ClaudeCodeChatClientOptions WithResumeSession(
        this ClaudeCodeChatClientOptions options,
        string sessionId)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        options.Resume = sessionId;
        options.ConversationMode = ConversationMode.ClaudeCodeManaged;
        return options;
    }
}
