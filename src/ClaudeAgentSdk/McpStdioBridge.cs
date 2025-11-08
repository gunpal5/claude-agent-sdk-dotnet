using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace ClaudeAgentSdk;

/// <summary>
/// Creates a stdio MCP server bridge using Node.js that forwards tool calls
/// to the .NET process via HTTP.
/// </summary>
internal class McpStdioBridge
{
    private readonly Dictionary<string, object> _serverInstances = new();
    private HttpListener? _httpListener;
    private readonly int _port;
    private readonly TaskCompletionSource<bool> _serverReady = new();

    public McpStdioBridge(int port = 0)
    {
        _port = port == 0 ? FindAvailablePort() : port;
    }

    public int Port => _port;

    /// <summary>
    /// Waits for the HTTP server to be ready and listening.
    /// </summary>
    public Task WaitForServerReadyAsync() => _serverReady.Task;

    public void RegisterServer(string name, object instance)
    {
        var logMsg = $"[DEBUG] RegisterServer: {name} -> {instance.GetType().Name}";
        Console.WriteLine(logMsg);
        try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

        _serverInstances[name] = instance;

        logMsg = $"[DEBUG] Total registered servers: {_serverInstances.Count}";
        Console.WriteLine(logMsg);
        try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }
    }

    /// <summary>
    /// Starts the HTTP server that handles tool calls from the Node.js bridge.
    /// </summary>
    public async Task StartHttpServerAsync(CancellationToken cancellationToken = default)
    {
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://localhost:{_port}/");
        _httpListener.Start();

        var logMsg = $"[DEBUG] MCP HTTP bridge listening on port {_port}";
        Console.WriteLine(logMsg);
        try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

        // Signal that the server is ready
        _serverReady.TrySetResult(true);

        logMsg = $"[DEBUG] Starting request loop on port {_port}";
        Console.WriteLine(logMsg);
        try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                logMsg = $"[DEBUG] Waiting for HTTP request on port {_port}...";
                Console.WriteLine(logMsg);
                try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

                var context = await _httpListener.GetContextAsync();

                logMsg = $"[DEBUG] Received HTTP request";
                Console.WriteLine(logMsg);
                try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

                _ = Task.Run(async () => await HandleRequestAsync(context, cancellationToken), cancellationToken);
            }
            catch (HttpListenerException ex) when (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"[DEBUG] HTTP listener cancelled");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] HTTP listener error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var logMsg = "";
        try
        {
            logMsg = "[DEBUG] HandleRequestAsync called";
            Console.WriteLine(logMsg);
            try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

            var request = context.Request;
            var response = context.Response;

            logMsg = $"[DEBUG] Request method: {request.HttpMethod}";
            Console.WriteLine(logMsg);
            try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

            if (request.HttpMethod != "POST")
            {
                response.StatusCode = 405;
                response.Close();
                return;
            }

            using var reader = new StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();

            logMsg = $"[DEBUG] Request body: {body}";
            Console.WriteLine(logMsg);
            try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

            var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            var action = root.GetProperty("action").GetString();
            var serverName = root.GetProperty("server").GetString();

            logMsg = $"[DEBUG] Action: {action}, Server: {serverName}";
            Console.WriteLine(logMsg);
            try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

            if (!_serverInstances.TryGetValue(serverName!, out var serverInstance))
            {
                logMsg = $"[DEBUG] Server not found: {serverName}";
                Console.WriteLine(logMsg);
                try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

                response.StatusCode = 404;
                response.ContentType = "application/json";
                await using var errorWriter = new StreamWriter(response.OutputStream);
                await errorWriter.WriteAsync(JsonSerializer.Serialize(new { error = "Server not found" }));
                await errorWriter.FlushAsync();
                response.Close();
                return;
            }

            logMsg = $"[DEBUG] Server found, executing action: {action}";
            Console.WriteLine(logMsg);
            try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

            object result = action switch
            {
                "list_tools" => await ListToolsAsync(serverInstance, cancellationToken),
                "call_tool" => await CallToolAsync(serverInstance, root, cancellationToken),
                _ => new { error = "Unknown action" }
            };

            var resultJson = JsonSerializer.Serialize(result);
            logMsg = $"[DEBUG] Result JSON: {resultJson}";
            Console.WriteLine(logMsg);
            try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

            response.StatusCode = 200;
            response.ContentType = "application/json";
            await using var writer = new StreamWriter(response.OutputStream);
            await writer.WriteAsync(resultJson);
            await writer.FlushAsync();

            logMsg = "[DEBUG] Response written and flushed, closing";
            Console.WriteLine(logMsg);
            try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

            response.Close();

            logMsg = "[DEBUG] Response closed successfully";
            Console.WriteLine(logMsg);
            try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }
        }
        catch (Exception ex)
        {
            logMsg = $"[DEBUG] Error handling HTTP request: {ex.Message}\n{ex.StackTrace}";
            Console.WriteLine(logMsg);
            try { File.AppendAllText("claude_sdk_debug.log", logMsg + Environment.NewLine); } catch { }

            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // Ignore
            }
        }
    }

    private async Task<object> ListToolsAsync(object serverInstance, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[DEBUG] ListToolsAsync called for {serverInstance.GetType().Name}");

        var listToolsMethod = serverInstance.GetType().GetMethod("ListToolsAsync");
        if (listToolsMethod == null)
        {
            Console.WriteLine($"[DEBUG] ListToolsAsync method not found");
            return new { tools = Array.Empty<object>() };
        }

        // DynamicAIFunctionMcpServer.ListToolsAsync() doesn't take any parameters
        var task = listToolsMethod.Invoke(serverInstance, null);
        if (task is Task<McpToolListResponse> toolsTask)
        {
            var toolsResponse = await toolsTask;
            var tools = toolsResponse.Tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                inputSchema = new
                {
                    type = t.InputSchema.Type,
                    properties = t.InputSchema.Properties.ToDictionary(
                        kvp => kvp.Key,
                        kvp => JsonSerializer.Deserialize<object>(kvp.Value.ToJsonString())
                    ),
                    required = t.InputSchema.Required
                }
            }).ToList();

            Console.WriteLine($"[DEBUG] ListToolsAsync returning {tools.Count} tools: {string.Join(", ", tools.Select(t => t.name))}");
            return new { tools };
        }

        Console.WriteLine($"[DEBUG] ListToolsAsync invalid return type: {task?.GetType().Name}");
        return new { tools = Array.Empty<object>() };
    }

    private async Task<object> CallToolAsync(object serverInstance, JsonElement root, CancellationToken cancellationToken)
    {
        var toolName = root.GetProperty("tool_name").GetString();
        var arguments = root.TryGetProperty("arguments", out var args) ? args : (JsonElement?)null;

        Console.WriteLine($"[DEBUG] CallToolAsync: tool_name={toolName}, has_args={arguments.HasValue}");

        var callToolMethod = serverInstance.GetType().GetMethod("CallToolAsync");
        if (callToolMethod == null)
        {
            Console.WriteLine($"[DEBUG] CallToolAsync method not found on {serverInstance.GetType().Name}");
            return new { error = "CallToolAsync method not found" };
        }

        var argsElement = arguments.HasValue ? arguments.Value : JsonDocument.Parse("{}").RootElement;
        var task = callToolMethod.Invoke(serverInstance, new object[] { toolName!, argsElement, cancellationToken });

        if (task is Task<string> resultTask)
        {
            var result = await resultTask;
            Console.WriteLine($"[DEBUG] CallToolAsync result: {result}");
            return new { result };
        }

        Console.WriteLine($"[DEBUG] CallToolAsync invalid return type: {task?.GetType().Name}");
        return new { error = "Invalid return type" };
    }

    public void Stop()
    {
        _httpListener?.Stop();
        _httpListener?.Close();
    }

    private static int FindAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Creates a Node.js MCP stdio bridge script and returns the command to run it.
    /// </summary>
    public static (string Command, string[] Args, string ScriptPath) CreateNodeBridgeScript(string serverName, int httpPort)
    {
        var script = $$"""
const http = require('http');
const readline = require('readline');
const fs = require('fs');
const path = require('path');

const SERVER_NAME = '{{serverName}}';
const HTTP_PORT = {{httpPort}};

// Create log file for debugging
const logFile = path.join(require('os').tmpdir(), 'node-bridge-debug.log');
const log = (msg) => {
  try {
    fs.appendFileSync(logFile, `[${new Date().toISOString()}] ${msg}\n`);
  } catch (e) {}
};

log(`Starting bridge for ${SERVER_NAME} on port ${HTTP_PORT}`);

// MCP JSON-RPC message handling
const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
  terminal: false
});

function sendResponse(response) {
  console.log(JSON.stringify(response));
}

function sendError(id, code, message) {
  sendResponse({
    jsonrpc: '2.0',
    id: id,
    error: { code, message }
  });
}

async function httpPost(data) {
  return new Promise((resolve, reject) => {
    const postData = JSON.stringify(data);
    log(`HTTP POST to localhost:${HTTP_PORT}: ${postData}`);
    const options = {
      hostname: 'localhost',
      port: HTTP_PORT,
      path: '/',
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(postData)
      }
    };

    const req = http.request(options, (res) => {
      let data = '';
      res.on('data', (chunk) => { data += chunk; });
      res.on('end', () => {
        log(`HTTP Response: ${data}`);
        try {
          if (!data) {
            reject(new Error('Empty response from HTTP server'));
            return;
          }
          resolve(JSON.parse(data));
        } catch (e) {
          log(`JSON parse error: ${e.message}, data: ${data}`);
          reject(e);
        }
      });
    });

    req.on('error', (e) => {
      log(`HTTP error: ${e.message}`);
      reject(e);
    });
    req.write(postData);
    req.end();
  });
}

rl.on('line', async (line) => {
  try {
    log(`Received: ${line.substring(0, 200)}`);
    const request = JSON.parse(line);
    const { method, params, id } = request;
    log(`Method: ${method}, ID: ${id}`);

    switch (method) {
      case 'initialize':
        sendResponse({
          jsonrpc: '2.0',
          id: id,
          result: {
            protocolVersion: '2024-11-05',
            capabilities: { tools: {} },
            serverInfo: { name: SERVER_NAME, version: '1.0.0' }
          }
        });
        break;

      case 'tools/list':
        try {
          log(`Listing tools for ${SERVER_NAME}`);
          const result = await httpPost({ action: 'list_tools', server: SERVER_NAME });
          log(`Got ${result.tools.length} tools`);
          sendResponse({
            jsonrpc: '2.0',
            id: id,
            result: { tools: result.tools }
          });
        } catch (e) {
          log(`Error listing tools: ${e.message}`);
          sendError(id, -32603, `Failed to list tools: ${e.message}`);
        }
        break;

      case 'tools/call':
        try {
          const { name, arguments: args } = params;
          log(`Calling tool ${name} for ${SERVER_NAME}`);
          const result = await httpPost({
            action: 'call_tool',
            server: SERVER_NAME,
            tool_name: name,
            arguments: args || {}
          });

          if (result.error) {
            log(`Tool call error: ${result.error}`);
            sendError(id, -32603, result.error);
          } else {
            log(`Tool call success: ${result.result}`);
            sendResponse({
              jsonrpc: '2.0',
              id: id,
              result: {
                content: [{ type: 'text', text: result.result }]
              }
            });
          }
        } catch (e) {
          log(`Tool call exception: ${e.message}`);
          sendError(id, -32603, `Tool call failed: ${e.message}`);
        }
        break;

      default:
        sendError(id, -32601, `Method not found: ${method}`);
    }
  } catch (e) {
    // Invalid JSON or other error
  }
});

process.on('SIGTERM', () => process.exit(0));
process.on('SIGINT', () => process.exit(0));
""";

        var tempDir = Path.Combine(Path.GetTempPath(), "claude-sdk-mcp-bridge");
        Directory.CreateDirectory(tempDir);

        var scriptPath = Path.Combine(tempDir, $"bridge-{serverName}-{httpPort}.js");
        File.WriteAllText(scriptPath, script);

        return ("node", new[] { scriptPath }, scriptPath);
    }
}

