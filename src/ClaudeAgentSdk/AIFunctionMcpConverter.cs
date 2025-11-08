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

        // Get the parameter schema from the AIFunction metadata
        var parameterSchema = aiFunction.Metadata.Parameters;

        return new McpToolSchema
        {
            Name = aiFunction.Metadata.Name,
            Description = aiFunction.Metadata.Description ?? string.Empty,
            InputSchema = new McpInputSchema
            {
                Type = "object",
                Properties = ConvertParameters(parameterSchema),
                Required = ExtractRequiredFromParameters(parameterSchema)
            }
        };
    }

    /// <summary>
    /// Converts AIFunction parameters to MCP properties format.
    /// </summary>
    private static Dictionary<string, JsonNode> ConvertParameters(IEnumerable<AIFunctionParameterMetadata>? parameters)
    {
        var properties = new Dictionary<string, JsonNode>();

        if (parameters == null)
            return properties;

        foreach (var param in parameters)
        {
            var propNode = new JsonObject
            {
                ["type"] = MapDotNetTypeToJsonSchemaType(param.ParameterType)
            };

            if (!string.IsNullOrEmpty(param.Description))
            {
                propNode["description"] = param.Description;
            }

            properties[param.Name] = propNode;
        }

        return properties;
    }

    /// <summary>
    /// Maps .NET types to JSON Schema draft 2020-12 type names.
    /// </summary>
    private static string MapDotNetTypeToJsonSchemaType(Type? type)
    {
        if (type == null)
            return "string";

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return Type.GetTypeCode(underlyingType) switch
        {
            TypeCode.Boolean => "boolean",
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => "integer",
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "number",
            TypeCode.String or TypeCode.Char => "string",
            _ => underlyingType.IsArray || underlyingType.IsAssignableTo(typeof(System.Collections.IEnumerable)) && underlyingType != typeof(string)
                ? "array"
                : underlyingType.IsClass || underlyingType.IsInterface
                ? "object"
                : "string"
        };
    }

    /// <summary>
    /// Extracts required parameter names.
    /// </summary>
    private static List<string> ExtractRequiredFromParameters(IEnumerable<AIFunctionParameterMetadata>? parameters)
    {
        var required = new List<string>();

        if (parameters == null)
            return required;

        foreach (var param in parameters)
        {
            if (param.IsRequired)
            {
                required.Add(param.Name);
            }
        }

        return required;
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

    /// <summary>
    /// Creates a new dynamic MCP server for the specified AIFunctions.
    /// </summary>
    /// <param name="aiFunctions">The AI functions to expose as MCP tools.</param>
    public DynamicAIFunctionMcpServer(IEnumerable<AIFunction> aiFunctions)
    {
        if (aiFunctions == null)
            throw new ArgumentNullException(nameof(aiFunctions));

        _toolsByName = aiFunctions.ToDictionary(f => f.Metadata.Name, f => f);
    }

    /// <summary>
    /// Lists all available AI functions as MCP tools.
    /// Implements the MCP tools/list protocol method.
    /// </summary>
    /// <returns>List of all available tools with their schemas.</returns>
    public Task<McpToolListResponse> ListToolsAsync()
    {
        var tools = _toolsByName.Values
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

        if (!_toolsByName.TryGetValue(name, out var aiFunction))
        {
            throw new InvalidOperationException($"Tool '{name}' not found");
        }

        // Convert JsonElement arguments to AIFunctionArguments
        var aiFunctionArgs = new Dictionary<string, object?>();

        if (arguments.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in arguments.EnumerateObject())
            {
                aiFunctionArgs[prop.Name] = DeserializeJsonElement(prop.Value);
            }
        }

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
