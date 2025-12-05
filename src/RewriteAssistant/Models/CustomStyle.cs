using System.Text.Json.Serialization;

namespace RewriteAssistant.Models;

/// <summary>
/// Represents a custom rewrite style combining a prompt with an optional hotkey
/// </summary>
public class CustomStyle
{
    /// <summary>
    /// Unique identifier for this style
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the style
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the CustomPrompt used by this style
    /// </summary>
    [JsonPropertyName("promptId")]
    public string PromptId { get; set; } = string.Empty;

    /// <summary>
    /// Optional hotkey binding for this style
    /// </summary>
    [JsonPropertyName("hotkey")]
    public HotkeyConfig? Hotkey { get; set; }

    /// <summary>
    /// Whether this is a built-in default style (cannot be deleted)
    /// </summary>
    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn { get; set; } = false;
}
