using System.Text.Json.Serialization;

namespace RewriteAssistant.Models;

/// <summary>
/// Represents a custom AI prompt configuration
/// </summary>
public class CustomPrompt
{
    /// <summary>
    /// Unique identifier for this prompt (GUID)
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the prompt
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full system prompt text sent to the AI model
    /// </summary>
    [JsonPropertyName("promptText")]
    public string PromptText { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a built-in default prompt (cannot be deleted)
    /// </summary>
    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn { get; set; } = false;

    /// <summary>
    /// When the prompt was created
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the prompt was last modified
    /// </summary>
    [JsonPropertyName("modifiedAt")]
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
