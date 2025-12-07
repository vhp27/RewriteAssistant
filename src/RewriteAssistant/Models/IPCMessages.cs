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
    /// Prompt ID for the rewrite style (kept for logging/debugging)
    /// </summary>
    [JsonPropertyName("promptId")]
    public string PromptId { get; set; } = string.Empty;

    /// <summary>
    /// The complete prompt text to use for the rewrite operation.
    /// Backend uses this directly without any lookup.
    /// </summary>
    [JsonPropertyName("promptText")]
    public string PromptText { get; set; } = string.Empty;

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

    /// <summary>
    /// Selected AI model for rewrite operations
    /// Requirements: 6.3
    /// </summary>
    [JsonPropertyName("selectedModel")]
    public string? SelectedModel { get; set; }
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
/// List models request sent to the Node.js backend
/// Requirements: 6.1
/// </summary>
public class ListModelsRequest
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Cerebras model information
/// Requirements: 6.1, 6.2
/// </summary>
public class CerebrasModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "model";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; set; } = string.Empty;
}

/// <summary>
/// List models response from the Node.js backend
/// Requirements: 6.1
/// </summary>
public class ListModelsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("models")]
    public List<CerebrasModel>? Models { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}


