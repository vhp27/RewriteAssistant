using System.Text.Json.Serialization;

namespace RewriteAssistant.Models;

/// <summary>
/// Rewrite request sent to the Node.js backend
/// </summary>
public class RewriteRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Prompt ID for the rewrite style
    /// </summary>
    [JsonPropertyName("promptId")]
    public string PromptId { get; set; } = string.Empty;

    /// <summary>
    /// Optional prompt text override for custom prompts
    /// </summary>
    [JsonPropertyName("promptText")]
    public string? PromptText { get; set; }

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;
}

/// <summary>
/// Rewrite response from the Node.js backend
/// </summary>
public class RewriteResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("rewrittenText")]
    public string? RewrittenText { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("usedFallbackKey")]
    public bool UsedFallbackKey { get; set; }
}

/// <summary>
/// Generic IPC message wrapper
/// </summary>
public class IPCMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "rewrite_request";

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

/// <summary>
/// Generic IPC response wrapper
/// </summary>
public class IPCResponse
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Health check status
/// </summary>
public class HealthStatus
{
    [JsonPropertyName("healthy")]
    public bool Healthy { get; set; }

    [JsonPropertyName("primaryKeyValid")]
    public bool PrimaryKeyValid { get; set; }

    [JsonPropertyName("fallbackKeyValid")]
    public bool FallbackKeyValid { get; set; }

    [JsonPropertyName("uptime")]
    public long Uptime { get; set; }
}

/// <summary>
/// Configuration update request sent to the Node.js backend
/// </summary>
public class ConfigUpdate
{
    [JsonPropertyName("primaryApiKey")]
    public string? PrimaryApiKey { get; set; }

    [JsonPropertyName("fallbackApiKey")]
    public string? FallbackApiKey { get; set; }
}

/// <summary>
/// Configuration update response from the Node.js backend
/// </summary>
public class ConfigResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Prompt sync payload for updating backend prompts
/// Requirements: 4.2, 4.3
/// </summary>
public class PromptSyncPayload
{
    [JsonPropertyName("prompts")]
    public List<CustomPromptDto> Prompts { get; set; } = new();
}

/// <summary>
/// DTO for CustomPrompt to send via IPC
/// </summary>
public class CustomPromptDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("promptText")]
    public string PromptText { get; set; } = string.Empty;

    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Creates a DTO from a CustomPrompt model
    /// </summary>
    public static CustomPromptDto FromModel(CustomPrompt prompt)
    {
        return new CustomPromptDto
        {
            Id = prompt.Id,
            Name = prompt.Name,
            PromptText = prompt.PromptText,
            IsBuiltIn = prompt.IsBuiltIn
        };
    }
}

/// <summary>
/// Prompt sync response from the Node.js backend
/// </summary>
public class PromptSyncResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("promptCount")]
    public int PromptCount { get; set; }
}
