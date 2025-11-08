using System.Text.Json;

namespace ClaudeAgentSdk;

/// <summary>
/// Parses raw message data into typed Message objects.
/// </summary>
public static class MessageParser
{
    public static Message ParseMessage(Dictionary<string, object> data)
    {
        if (!data.TryGetValue("type", out var typeObj) || typeObj is not JsonElement typeElement)
            throw new ArgumentException("Message missing 'type' field");

        var type = typeElement.GetString();

        return type switch
        {
            "user" => ParseUserMessage(data),
            "assistant" => ParseAssistantMessage(data),
            "system" => ParseSystemMessage(data),
            "result" => ParseResultMessage(data),
            "stream" or "stream_event" => ParseStreamEvent(data),
            "control_request" => ParseControlRequest(data),
            _ => throw new ArgumentException($"Unknown message type: {type}")
        };
    }

    private static UserMessage ParseUserMessage(Dictionary<string, object> data)
    {
        var message = GetJsonElement(data, "message");
        var content = message.TryGetProperty("content", out var contentProp)
            ? ParseContent(contentProp)
            : "";

        return new UserMessage
        {
            Content = content,
            ParentToolUseId = GetStringOrNull(data, "parent_tool_use_id")
        };
    }

    private static AssistantMessage ParseAssistantMessage(Dictionary<string, object> data)
    {
        var message = GetJsonElement(data, "message");
        var contentBlocks = new List<object>();

        if (message.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in contentProp.EnumerateArray())
            {
                contentBlocks.Add(ParseContentBlock(block));
            }
        }

        return new AssistantMessage
        {
            Content = contentBlocks,
            Model = message.GetProperty("model").GetString() ?? "",
            ParentToolUseId = GetStringOrNull(data, "parent_tool_use_id")
        };
    }

    private static SystemMessage ParseSystemMessage(Dictionary<string, object> data)
    {
        return new SystemMessage
        {
            Subtype = GetString(data, "subtype"),
            Data = data.TryGetValue("data", out var dataObj) && dataObj is JsonElement dataElement
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(dataElement.GetRawText()) ?? new()
                : new()
        };
    }

    private static ResultMessage ParseResultMessage(Dictionary<string, object> data)
    {
        return new ResultMessage
        {
            Subtype = GetString(data, "subtype"),
            DurationMs = GetInt(data, "duration_ms"),
            DurationApiMs = GetInt(data, "duration_api_ms"),
            IsError = GetBool(data, "is_error"),
            NumTurns = GetInt(data, "num_turns"),
            SessionId = GetString(data, "session_id"),
            TotalCostUsd = GetDoubleOrNull(data, "total_cost_usd"),
            Usage = data.TryGetValue("usage", out var usageObj) && usageObj is JsonElement usageElement
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(usageElement.GetRawText())
                : null,
            Result = GetStringOrNull(data, "result")
        };
    }

    private static StreamEvent ParseStreamEvent(Dictionary<string, object> data)
    {
        return new StreamEvent
        {
            Uuid = GetString(data, "uuid"),
            SessionId = GetString(data, "session_id"),
            Event = data.TryGetValue("event", out var eventObj) && eventObj is JsonElement eventElement
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(eventElement.GetRawText()) ?? new()
                : new(),
            ParentToolUseId = GetStringOrNull(data, "parent_tool_use_id")
        };
    }

    private static ControlRequest ParseControlRequest(Dictionary<string, object> data)
    {
        // Try to find control_type, but it might not always be present
        var controlType = GetStringOrNull(data, "control_type") ?? "unknown";

        return new ControlRequest
        {
            ControlType = controlType,
            Data = data.TryGetValue("data", out var dataObj) && dataObj is JsonElement dataElement
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(dataElement.GetRawText())
                : null,
            ParentToolUseId = GetStringOrNull(data, "parent_tool_use_id")
        };
    }

    private static object ParseContent(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString() ?? "";

        if (content.ValueKind == JsonValueKind.Array)
        {
            var blocks = new List<object>();
            foreach (var block in content.EnumerateArray())
            {
                blocks.Add(ParseContentBlock(block));
            }
            return blocks;
        }

        return "";
    }

    private static object ParseContentBlock(JsonElement block)
    {
        if (!block.TryGetProperty("type", out var typeProp))
            return new { };

        var type = typeProp.GetString();

        return type switch
        {
            "text" => new TextBlock
            {
                Text = block.GetProperty("text").GetString() ?? ""
            },
            "thinking" => new ThinkingBlock
            {
                Thinking = block.GetProperty("thinking").GetString() ?? "",
                Signature = block.GetProperty("signature").GetString() ?? ""
            },
            "tool_use" => new ToolUseBlock
            {
                Id = block.GetProperty("id").GetString() ?? "",
                Name = block.GetProperty("name").GetString() ?? "",
                Input = block.TryGetProperty("input", out var inputProp)
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(inputProp.GetRawText()) ?? new()
                    : new()
            },
            "tool_result" => new ToolResultBlock
            {
                ToolUseId = block.GetProperty("tool_use_id").GetString() ?? "",
                Content = block.TryGetProperty("content", out var contentProp)
                    ? ParseToolResultContent(contentProp)
                    : null,
                IsError = block.TryGetProperty("is_error", out var isErrorProp) && (isErrorProp.ValueKind == JsonValueKind.True || isErrorProp.ValueKind == JsonValueKind.False)
                    ? isErrorProp.GetBoolean()
                    : null
            },
            _ => new { }
        };
    }

    private static object? ParseToolResultContent(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString();

        if (content.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content.GetRawText());
        }

        return null;
    }

    private static JsonElement GetJsonElement(Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is not JsonElement element)
            throw new ArgumentException($"Missing or invalid field: {key}");
        return element;
    }

    private static string GetString(Dictionary<string, object> data, string key)
    {
        var element = GetJsonElement(data, key);
        return element.GetString() ?? "";
    }

    private static string? GetStringOrNull(Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is not JsonElement element)
            return null;
        return element.ValueKind == JsonValueKind.Null ? null : element.GetString();
    }

    private static int GetInt(Dictionary<string, object> data, string key)
    {
        var element = GetJsonElement(data, key);
        return element.GetInt32();
    }

    private static bool GetBool(Dictionary<string, object> data, string key)
    {
        var element = GetJsonElement(data, key);
        return element.GetBoolean();
    }

    private static double? GetDoubleOrNull(Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is not JsonElement element)
            return null;
        return element.ValueKind == JsonValueKind.Null ? null : element.GetDouble();
    }
}
