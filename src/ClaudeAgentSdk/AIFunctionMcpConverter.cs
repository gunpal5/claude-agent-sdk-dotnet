using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace ClaudeAgentSdk;

/// <summary>
/// Converts Microsoft.Extensions.AI AIFunction tools into MCP (Model Context Protocol)
/// tool specifications for use with Claude Code.
/// </summary>
public class AIFunctionMcpConverter
{
    private readonly IEnumerable<AIFunction> _aiFunctions;
    private readonly string _serverName;

    /// <summary>
    /// Creates a new converter for the specified AIFunctions.
    /// </summary>
    /// <param name="aiFunctions">The AI functions to convert to MCP tools.</param>
    /// <param name="serverName">Name for the MCP server. Default is "ai-functions".</param>
    public AIFunctionMcpConverter(
        IEnumerable<AIFunction> aiFunctions,
        string serverName = "ai-functions")
    {
        _aiFunctions = aiFunctions ?? throw new ArgumentNullException(nameof(aiFunctions));
        _serverName = serverName ?? throw new ArgumentNullException(nameof(serverName));
    }

    /// <summary>
    /// Converts an AIFunction to MCP tool schema format.
    /// </summary>
    /// <param name="aiFunction">The AIFunction to convert.</param>
    /// <returns>MCP tool schema matching the Model Context Protocol specification.</returns>
    public static McpToolSchema ConvertToMcpToolSchema(AIFunction aiFunction)
    {
        if (aiFunction == null)
            throw new ArgumentNullException(nameof(aiFunction));

        // Get the parameter schema from the AIFunction JsonSchema
        var inputSchema = ParseJsonSchemaToMcpInputSchema(aiFunction.JsonSchema);

        return new McpToolSchema
        {
            Name = aiFunction.Name,
            Description = aiFunction.Description ?? string.Empty,
            InputSchema = inputSchema
        };
    }

    /// <summary>
    /// Parses an AIFunction JsonSchema into MCP InputSchema format.
    /// </summary>
    private static McpInputSchema ParseJsonSchemaToMcpInputSchema(JsonElement jsonSchema)
    {
        var inputSchema = new McpInputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, JsonNode>(),
            Required = new List<string>()
        };

        // Parse properties from JsonSchema
        if (jsonSchema.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                inputSchema.Properties[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            }
        }

        // Parse required array from JsonSchema
        if (jsonSchema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in required.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    inputSchema.Required.Add(item.GetString()!);
                }
            }
        }

        return inputSchema;
    }

    /// <summary>
    /// Creates an MCP server configuration that can be used with ClaudeCodeChatClient.
    /// </summary>
    /// <returns>MCP SDK server configuration ready to use.</returns>
    public McpSdkServerConfig CreateMcpServerConfig()
    {
        var mcpServer = new DynamicAIFunctionMcpServer(_aiFunctions);

        return new McpSdkServerConfig
        {
            Type = "sdk",
            Name = _serverName,
            Instance = mcpServer
        };
    }
}

/// <summary>
/// MCP Tool Schema format matching Model Context Protocol specification.
/// </summary>
public class McpToolSchema
{
    /// <summary>
    /// The unique name of the tool.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description of what the tool does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON Schema describing the tool's input parameters.
    /// </summary>
    public required McpInputSchema InputSchema { get; set; }
}

/// <summary>
/// MCP Input Schema matching Model Context Protocol specification.
/// </summary>
public class McpInputSchema
{
    /// <summary>
    /// Schema type, always "object" for MCP tools.
    /// </summary>
    public string Type { get; set; } = "object";

    /// <summary>
    /// Properties defining each parameter with its JSON Schema.
    /// </summary>
    public Dictionary<string, JsonNode> Properties { get; set; } = new();

    /// <summary>
    /// List of required parameter names.
    /// </summary>
    public List<string> Required { get; set; } = new();
}

/// <summary>
/// Dynamic MCP Server that wraps AIFunction tools and exposes them via
/// Model Context Protocol for use with Claude Code.
/// </summary>
public class DynamicAIFunctionMcpServer
{
    private readonly Dictionary<string, AIFunction> _toolsByName;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new dynamic MCP server for the specified AIFunctions.
    /// </summary>
    /// <param name="aiFunctions">The AI functions to expose as MCP tools.</param>
    public DynamicAIFunctionMcpServer(IEnumerable<AIFunction> aiFunctions)
    {
        if (aiFunctions == null)
            throw new ArgumentNullException(nameof(aiFunctions));

        _toolsByName = aiFunctions.ToDictionary(f => f.Name, f => f);
    }

    /// <summary>
    /// Adds an AIFunction to the available tools.
    /// </summary>
    /// <param name="aiFunction">The AI function to add.</param>
    public void AddTool(AIFunction aiFunction)
    {
        if (aiFunction == null)
            throw new ArgumentNullException(nameof(aiFunction));

        lock (_lock)
        {
            _toolsByName[aiFunction.Name] = aiFunction;
        }
    }

    /// <summary>
    /// Adds multiple AIFunctions to the available tools.
    /// </summary>
    /// <param name="aiFunctions">The AI functions to add.</param>
    public void AddTools(IEnumerable<AIFunction> aiFunctions)
    {
        if (aiFunctions == null)
            throw new ArgumentNullException(nameof(aiFunctions));

        lock (_lock)
        {
            foreach (var func in aiFunctions)
            {
                _toolsByName[func.Name] = func;
            }
        }
    }

    /// <summary>
    /// Removes an AIFunction from the available tools.
    /// </summary>
    /// <param name="toolName">The name of the tool to remove.</param>
    /// <returns>True if the tool was removed, false if it didn't exist.</returns>
    public bool RemoveTool(string toolName)
    {
        lock (_lock)
        {
            return _toolsByName.Remove(toolName);
        }
    }

    /// <summary>
    /// Removes multiple AIFunctions from the available tools.
    /// </summary>
    /// <param name="toolNames">The names of the tools to remove.</param>
    public void RemoveTools(IEnumerable<string> toolNames)
    {
        if (toolNames == null)
            throw new ArgumentNullException(nameof(toolNames));

        lock (_lock)
        {
            foreach (var name in toolNames)
            {
                _toolsByName.Remove(name);
            }
        }
    }

    /// <summary>
    /// Lists all available AI functions as MCP tools.
    /// Implements the MCP tools/list protocol method.
    /// </summary>
    /// <returns>List of all available tools with their schemas.</returns>
    public Task<McpToolListResponse> ListToolsAsync()
    {
        List<AIFunction> functionsCopy;
        lock (_lock)
        {
            functionsCopy = _toolsByName.Values.ToList();
        }

        var tools = functionsCopy
            .Select(f => AIFunctionMcpConverter.ConvertToMcpToolSchema(f))
            .ToList();

        return Task.FromResult(new McpToolListResponse { Tools = tools });
    }

    /// <summary>
    /// Calls an AI function with the provided arguments.
    /// Implements the MCP tools/call protocol method.
    /// </summary>
    /// <param name="name">The name of the tool to call.</param>
    /// <param name="arguments">The arguments to pass to the tool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool execution result as JSON string.</returns>
    public async Task<string> CallToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Tool name cannot be null or empty", nameof(name));

        AIFunction? aiFunction;
        lock (_lock)
        {
            if (!_toolsByName.TryGetValue(name, out aiFunction))
            {
                throw new InvalidOperationException($"Tool '{name}' not found");
            }
        }

        // Convert JsonElement arguments to AIFunctionArguments
        var argsDict = new Dictionary<string, object?>();

        if (arguments.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in arguments.EnumerateObject())
            {
                argsDict[prop.Name] = DeserializeJsonElement(prop.Value);
            }
        }

        var aiFunctionArgs = new AIFunctionArguments(argsDict);

        // Invoke the AIFunction
        var result = await aiFunction.InvokeAsync(aiFunctionArgs, cancellationToken);

        // Return result as JSON string
        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    /// <summary>
    /// Gets the number of tools available.
    /// </summary>
    public int ToolCount => _toolsByName.Count;

    /// <summary>
    /// Gets the names of all available tools.
    /// </summary>
    public IEnumerable<string> ToolNames => _toolsByName.Keys;

    private static object? DeserializeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal :
                                   element.TryGetInt64(out var longVal) ? longVal :
                                   element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(DeserializeJsonElement)
                .ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => DeserializeJsonElement(p.Value)),
            _ => element.GetRawText()
        };
    }
}

/// <summary>
/// Response for MCP tools/list method.
/// </summary>
public class McpToolListResponse
{
    /// <summary>
    /// List of available tools.
    /// </summary>
    public List<McpToolSchema> Tools { get; set; } = new();

    /// <summary>
    /// Optional cursor for pagination.
    /// </summary>
    public string? NextCursor { get; set; }
}
