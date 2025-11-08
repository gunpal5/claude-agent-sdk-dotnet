using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Subprocess transport using Claude Code CLI.
/// </summary>
public class SubprocessCliTransport : ITransport
{
    private const int DefaultMaxBufferSize = 1024 * 1024; // 1MB
    private const string SdkVersion = "0.1.0";

    private readonly object _prompt;
    private readonly bool _isStreaming;
    private readonly ClaudeAgentOptions _options;
    private readonly string _cliPath;
    private readonly string? _cwd;
    private readonly int _maxBufferSize;

    private Process? _process;
    private StreamWriter? _stdinWriter;
    private StreamReader? _stdoutReader;
    private StreamReader? _stderrReader;
    private Task? _stderrTask;
    private CancellationTokenSource? _stderrCts;
    private bool _ready;
    private Exception? _exitError;
    private McpStdioBridge? _mcpBridge;
    private Task? _mcpBridgeTask;
    private CancellationTokenSource? _mcpBridgeCts;
    private readonly List<Process> _bridgeProcesses = new();

    public bool IsReady => _ready;

    public SubprocessCliTransport(
        object prompt,
        ClaudeAgentOptions options,
        string? cliPath = null)
    {
        _prompt = prompt;
        _isStreaming = prompt is not string;
        _options = options;
        _cliPath = cliPath ?? FindCli();
        _cwd = options.Cwd;
        _maxBufferSize = options.MaxBufferSize ?? DefaultMaxBufferSize;
    }

    private static string FindCli()
    {
        // Check PATH
        var cliName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "claude.cmd" : "claude";
        var pathVar = Environment.GetEnvironmentVariable("PATH");

        if (pathVar != null)
        {
            var paths = pathVar.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(path, cliName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }

        // Check common locations
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var locations = new[]
        {
            Path.Combine(home, ".npm-global", "bin", cliName),
            Path.Combine("/usr", "local", "bin", cliName),
            Path.Combine(home, ".local", "bin", cliName),
            Path.Combine(home, "node_modules", ".bin", cliName),
            Path.Combine(home, ".yarn", "bin", cliName)
        };

        foreach (var location in locations)
        {
            if (File.Exists(location))
                return location;
        }

        throw new CliNotFoundException(
            "Claude Code not found. Install with:\n" +
            "  npm install -g @anthropic-ai/claude-code\n" +
            "\nIf already installed locally, try:\n" +
            "  export PATH=\"$HOME/node_modules/.bin:$PATH\"\n" +
            "\nOr specify the path when creating transport");
    }

    private List<string> BuildCommand()
    {
        var cmd = new List<string> { "--output-format", "stream-json", "--verbose" };

        // System prompt
        if (_options.SystemPrompt is string systemPromptStr)
        {
            cmd.Add("--system-prompt");
            cmd.Add(systemPromptStr);
        }
        else if (_options.SystemPrompt is SystemPromptPreset preset && preset.Append != null)
        {
            cmd.Add("--append-system-prompt");
            cmd.Add(preset.Append);
        }

        // Tools
        if (_options.AllowedTools.Count > 0)
        {
            cmd.Add("--allowedTools");
            cmd.Add(string.Join(",", _options.AllowedTools));
        }

        if (_options.MaxTurns.HasValue)
        {
            cmd.Add("--max-turns");
            cmd.Add(_options.MaxTurns.Value.ToString());
        }

        if (_options.DisallowedTools.Count > 0)
        {
            cmd.Add("--disallowedTools");
            cmd.Add(string.Join(",", _options.DisallowedTools));
        }

        if (_options.Model != null)
        {
            cmd.Add("--model");
            cmd.Add(_options.Model);
        }

        if (_options.PermissionPromptToolName != null)
        {
            cmd.Add("--permission-prompt-tool");
            cmd.Add(_options.PermissionPromptToolName);
        }

        if (_options.PermissionMode.HasValue)
        {
            cmd.Add("--permission-mode");
            cmd.Add(_options.PermissionMode.Value.ToString().ToLowerInvariant());
        }

        if (_options.ContinueConversation)
            cmd.Add("--continue");

        if (_options.Resume != null)
        {
            cmd.Add("--resume");
            cmd.Add(_options.Resume);
        }

        if (_options.Settings != null)
        {
            cmd.Add("--settings");
            cmd.Add(_options.Settings);
        }

        foreach (var dir in _options.AddDirs)
        {
            cmd.Add("--add-dir");
            cmd.Add(dir);
        }

        // MCP servers
        if (_options.McpServers != null)
        {
            if (_options.McpServers is Dictionary<string, object> mcpDict)
            {
                var serversForCli = new Dictionary<string, object>();

                // Start HTTP bridge for SDK MCP servers
                if (_mcpBridge == null && mcpDict.Values.OfType<McpSdkServerConfig>().Any())
                {
                    _mcpBridge = new McpStdioBridge();
                    _mcpBridgeCts = new CancellationTokenSource();
                    _mcpBridgeTask = Task.Run(async () =>
                    {
                        try
                        {
                            await _mcpBridge.StartHttpServerAsync(_mcpBridgeCts.Token);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DEBUG] MCP bridge error: {ex.Message}");
                        }
                    });

                    // Wait for the server to be ready before continuing
                    _mcpBridge.WaitForServerReadyAsync().Wait();
                }

                foreach (var (name, config) in mcpDict)
                {
                    if (config is McpSdkServerConfig sdkConfig)
                    {
                        try
                        {
                            // SDK MCP servers require a stdio bridge
                            var bridgeInfo = $"[DEBUG] Creating stdio bridge for SDK MCP server: {name}";
                            Console.WriteLine(bridgeInfo);
                            try { File.AppendAllText("claude_sdk_debug.log", bridgeInfo + Environment.NewLine); } catch { }

                            _mcpBridge!.RegisterServer(name, sdkConfig.Instance);

                            // Create Node.js bridge script
                            var (nodeCmd, nodeArgs, scriptPath) = McpStdioBridge.CreateNodeBridgeScript(name, _mcpBridge.Port);

                            // Register as stdio MCP server for Claude CLI
                            // Create anonymous object with lowercase properties matching MCP schema
                            serversForCli[name] = new Dictionary<string, object>
                            {
                                ["type"] = "stdio",
                                ["command"] = nodeCmd,
                                ["args"] = nodeArgs
                            };

                            Console.WriteLine($"[DEBUG] Registered bridge for {name} on port {_mcpBridge.Port}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DEBUG] Error setting up SDK MCP server bridge {name}: {ex.Message}");
                        }
                    }
                    else
                    {
                        serversForCli[name] = config;
                    }
                }

                if (serversForCli.Count > 0)
                {
                    cmd.Add("--mcp-config");
                    cmd.Add(JsonSerializer.Serialize(new { mcpServers = serversForCli }));
                }
            }
            else
            {
                cmd.Add("--mcp-config");
                cmd.Add(_options.McpServers.ToString()!);
            }
        }

        if (_options.IncludePartialMessages)
            cmd.Add("--include-partial-messages");

        if (_options.ForkSession)
            cmd.Add("--fork-session");

        if (_options.Agents != null)
        {
            cmd.Add("--agents");
            cmd.Add(JsonSerializer.Serialize(_options.Agents));
        }

        if (_options.SettingSources != null)
        {
            cmd.Add("--setting-sources");
            cmd.Add(string.Join(",", _options.SettingSources.Select(s => s.ToString().ToLowerInvariant())));
        }
        else
        {
            cmd.Add("--setting-sources");
            cmd.Add("");
        }

        // Extra args
        foreach (var (flag, value) in _options.ExtraArgs)
        {
            if (value == null)
            {
                cmd.Add($"--{flag}");
            }
            else
            {
                cmd.Add($"--{flag}");
                cmd.Add(value);
            }
        }

        // Prompt handling
        if (_isStreaming)
        {
            cmd.Add("--input-format");
            cmd.Add("stream-json");
        }
        else
        {
            cmd.Add("--print");
            cmd.Add("--");
            cmd.Add(_prompt.ToString()!);
        }

        return cmd;
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_process != null)
            return Task.CompletedTask;

        var args = BuildCommand();

        // Debug: Log the command being executed
        var debugInfo = $"[DEBUG] Executing: {_cliPath} {string.Join(" ", args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a))}";
        Console.WriteLine(debugInfo);
        try { File.AppendAllText("claude_sdk_debug.log", debugInfo + Environment.NewLine); } catch { }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _cliPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Always redirect stderr for debugging
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _cwd ?? Environment.CurrentDirectory
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            // Set environment variables
            foreach (var (key, value) in _options.Env)
            {
                startInfo.Environment[key] = value;
            }
            startInfo.Environment["CLAUDE_CODE_ENTRYPOINT"] = "sdk-dotnet";
            startInfo.Environment["CLAUDE_AGENT_SDK_VERSION"] = SdkVersion;

            if (_cwd != null)
                startInfo.Environment["PWD"] = _cwd;

            _process = Process.Start(startInfo);
            if (_process == null)
                throw new CliConnectionException("Failed to start Claude Code process");

            var pidInfo = $"[DEBUG] Process started with PID: {_process.Id}";
            Console.WriteLine(pidInfo);
            try { File.AppendAllText("claude_sdk_debug.log", pidInfo + Environment.NewLine); } catch { }
            _stdoutReader = _process.StandardOutput;

            if (_isStreaming)
            {
                _stdinWriter = _process.StandardInput;
            }
            else
            {
                _process.StandardInput.Close();
            }

            // Handle stderr if needed
            if (startInfo.RedirectStandardError)
            {
                _stderrReader = _process.StandardError;
                _stderrCts = new CancellationTokenSource();
                _stderrTask = Task.Run(() => HandleStderrAsync(_stderrCts.Token), _stderrCts.Token);
            }

            _ready = true;
            return Task.CompletedTask;
        }
        catch (Exception ex) when (ex is not CliConnectionException and not CliNotFoundException)
        {
            if (_cwd != null && !Directory.Exists(_cwd))
            {
                _exitError = new CliConnectionException($"Working directory does not exist: {_cwd}");
                throw _exitError;
            }

            _exitError = new CliConnectionException($"Failed to start Claude Code: {ex.Message}", ex);
            throw _exitError;
        }
    }

    private async Task HandleStderrAsync(CancellationToken cancellationToken)
    {
        if (_stderrReader == null)
            return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _stderrReader.ReadLineAsync(cancellationToken);
                if (line == null)
                    break;

                if (!string.IsNullOrWhiteSpace(line))
                {
                    // Always log to console and file for debugging
                    var stderrInfo = $"[CLI STDERR] {line}";
                    Console.WriteLine(stderrInfo);
                    try { File.AppendAllText("claude_sdk_debug.log", stderrInfo + Environment.NewLine); } catch { }
                    _options.Stderr?.Invoke(line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch
        {
            // Ignore errors during stderr reading
        }
    }

    public async Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        if (!_ready || _stdinWriter == null)
            throw new CliConnectionException("Transport is not ready for writing");

        if (_process?.HasExited == true)
            throw new CliConnectionException($"Cannot write to terminated process (exit code: {_process.ExitCode})");

        if (_exitError != null)
            throw new CliConnectionException($"Cannot write to process that exited with error: {_exitError.Message}", _exitError);

        try
        {
            await _stdinWriter.WriteAsync(data.AsMemory(), cancellationToken);
            await _stdinWriter.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _ready = false;
            _exitError = new CliConnectionException($"Failed to write to process stdin: {ex.Message}", ex);
            throw _exitError;
        }
    }

    public async Task EndInputAsync(CancellationToken cancellationToken = default)
    {
        if (_stdinWriter != null)
        {
            try
            {
                await _stdinWriter.DisposeAsync();
                _stdinWriter = null;
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    public async IAsyncEnumerable<Dictionary<string, object>> ReadMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_process == null || _stdoutReader == null)
            throw new CliConnectionException("Not connected");

        var jsonBuffer = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await _stdoutReader.ReadLineAsync(cancellationToken);
            if (line == null)
                break;

            line = line.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            jsonBuffer.Append(line);

            if (jsonBuffer.Length > _maxBufferSize)
            {
                var bufferLength = jsonBuffer.Length;
                jsonBuffer.Clear();
                throw new CliJsonDecodeException(
                    $"JSON message exceeded maximum buffer size of {_maxBufferSize} bytes");
            }

            Dictionary<string, object>? data = null;
            try
            {
                data = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBuffer.ToString());
                jsonBuffer.Clear();
            }
            catch (JsonException)
            {
                // Continue accumulating partial JSON
                continue;
            }

            if (data != null)
                yield return data;
        }

        // Check process completion
        if (_process != null && !_process.HasExited)
        {
            try
            {
                await _process.WaitForExitAsync(cancellationToken);
            }
            catch
            {
                // Ignore
            }
        }

        if (_process?.ExitCode != null && _process.ExitCode != 0)
        {
            var exitInfo = $"[DEBUG] Process exited with code: {_process.ExitCode}, HasExited: {_process.HasExited}";
            Console.WriteLine(exitInfo);
            try { File.AppendAllText("claude_sdk_debug.log", exitInfo + Environment.NewLine); } catch { }
            _exitError = new ProcessException(
                $"Command failed with exit code {_process.ExitCode}",
                _process.ExitCode,
                "Check stderr output for details");
            throw _exitError;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _ready = false;

        // Stop MCP bridge
        if (_mcpBridgeCts != null)
        {
            try
            {
                await _mcpBridgeCts.CancelAsync();
                _mcpBridge?.Stop();
                if (_mcpBridgeTask != null)
                {
                    await _mcpBridgeTask;
                }
            }
            catch
            {
                // Ignore
            }
        }

        // Cancel and wait for stderr task
        if (_stderrCts != null)
        {
            try
            {
                await _stderrCts.CancelAsync();
                if (_stderrTask != null)
                    await _stderrTask;
            }
            catch
            {
                // Ignore
            }
            finally
            {
                _stderrCts?.Dispose();
                _stderrCts = null;
                _stderrTask = null;
            }
        }

        // Close streams
        if (_stdinWriter != null)
        {
            try
            {
                await _stdinWriter.DisposeAsync();
            }
            catch
            {
                // Ignore
            }
            _stdinWriter = null;
        }

        _stdoutReader?.Dispose();
        _stdoutReader = null;

        _stderrReader?.Dispose();
        _stderrReader = null;

        // Terminate process
        if (_process != null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync();
                }
                _process.Dispose();
            }
            catch
            {
                // Ignore
            }
            _process = null;
        }

        _exitError = null;
    }
}
