using System.Text.Json.Serialization;

namespace RewriteAssistant.Models;

/// <summary>
/// Application configuration model for persistence
/// </summary>
public class AppConfiguration
{
    /// <summary>
    /// Whether the application is enabled (hotkeys active)
    /// </summary>
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether to start the application with Windows
    /// </summary>
    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Whether to show a notification when text is rewritten successfully
    /// </summary>
    [JsonPropertyName("showSuccessNotification")]
    public bool ShowSuccessNotification { get; set; } = true;

    /// <summary>
    /// Default style ID (references a CustomStyle)
    /// </summary>
    [JsonPropertyName("defaultStyleId")]
    public string DefaultStyleId { get; set; } = "grammar_fix";

    /// <summary>
    /// List of custom prompts
    /// </summary>
    [JsonPropertyName("prompts")]
    public List<CustomPrompt> Prompts { get; set; } = new();

    /// <summary>
    /// List of custom styles (replaces hotkeys list)
    /// </summary>
    [JsonPropertyName("styles")]
    public List<CustomStyle> Styles { get; set; } = new();

    /// <summary>
    /// Legacy hotkey configurations (for backward compatibility during migration)
    /// </summary>
    [JsonPropertyName("hotkeys")]
    public List<HotkeyConfig> Hotkeys { get; set; } = new();

    /// <summary>
    /// API key storage (encrypted)
    /// </summary>
    [JsonPropertyName("apiKeys")]
    public ApiKeyStorage ApiKeys { get; set; } = new();

    /// <summary>
    /// Creates a default configuration with built-in prompts and styles
    /// </summary>
    public static AppConfiguration CreateDefault()
    {
        var now = DateTime.UtcNow;
        
        return new AppConfiguration
        {
            IsEnabled = true,
            StartWithWindows = false,
            DefaultStyleId = "grammar_fix",
            Prompts = new List<CustomPrompt>
            {
                new CustomPrompt
                {
                    Id = "grammar_fix_prompt",
                    Name = "Grammar Fix",
                    PromptText = "You are a text editor that fixes grammar and spelling errors. Preserve the original meaning and tone. Return ONLY the corrected text without any explanations, comments, or formatting.",
                    IsBuiltIn = true,
                    CreatedAt = now,
                    ModifiedAt = now
                },
                new CustomPrompt
                {
                    Id = "formal_tone_prompt",
                    Name = "Formal Tone",
                    PromptText = "You are a text editor that rewrites text in a formal, professional tone. Return ONLY the rewritten text without any explanations, comments, or formatting.",
                    IsBuiltIn = true,
                    CreatedAt = now,
                    ModifiedAt = now
                },
                new CustomPrompt
                {
                    Id = "casual_tone_prompt",
                    Name = "Casual Tone",
                    PromptText = "You are a text editor that rewrites text in a casual, friendly tone. Return ONLY the rewritten text without any explanations, comments, or formatting.",
                    IsBuiltIn = true,
                    CreatedAt = now,
                    ModifiedAt = now
                },
                new CustomPrompt
                {
                    Id = "shorten_text_prompt",
                    Name = "Shorten Text",
                    PromptText = "You are a text editor that shortens text while preserving the key message. Return ONLY the shortened text without any explanations, comments, or formatting.",
                    IsBuiltIn = true,
                    CreatedAt = now,
                    ModifiedAt = now
                },
                new CustomPrompt
                {
                    Id = "expand_text_prompt",
                    Name = "Expand Text",
                    PromptText = "You are a text editor that expands text with more detail and clarity. Return ONLY the expanded text without any explanations, comments, or formatting.",
                    IsBuiltIn = true,
                    CreatedAt = now,
                    ModifiedAt = now
                }
            },
            Styles = new List<CustomStyle>
            {
                new CustomStyle
                {
                    Id = "grammar_fix",
                    Name = "Grammar Fix",
                    PromptId = "grammar_fix_prompt",
                    Hotkey = new HotkeyConfig
                    {
                        Id = "grammar_fix",
                        Modifiers = new List<string> { "ctrl", "shift" },
                        Key = "G"
                    },
                    IsBuiltIn = true
                },
                new CustomStyle
                {
                    Id = "formal_tone",
                    Name = "Formal Tone",
                    PromptId = "formal_tone_prompt",
                    Hotkey = new HotkeyConfig
                    {
                        Id = "formal_tone",
                        Modifiers = new List<string> { "ctrl", "shift" },
                        Key = "F"
                    },
                    IsBuiltIn = true
                },
                new CustomStyle
                {
                    Id = "casual_tone",
                    Name = "Casual Tone",
                    PromptId = "casual_tone_prompt",
                    Hotkey = new HotkeyConfig
                    {
                        Id = "casual_tone",
                        Modifiers = new List<string> { "ctrl", "shift" },
                        Key = "C"
                    },
                    IsBuiltIn = true
                },
                new CustomStyle
                {
                    Id = "shorten_text",
                    Name = "Shorten Text",
                    PromptId = "shorten_text_prompt",
                    Hotkey = null,
                    IsBuiltIn = true
                },
                new CustomStyle
                {
                    Id = "expand_text",
                    Name = "Expand Text",
                    PromptId = "expand_text_prompt",
                    Hotkey = null,
                    IsBuiltIn = true
                }
            },
            Hotkeys = new List<HotkeyConfig>(), // Legacy - kept empty for backward compatibility
            ApiKeys = new ApiKeyStorage()
        };
    }
}

/// <summary>
/// Storage for encrypted API keys
/// </summary>
public class ApiKeyStorage
{
    /// <summary>
    /// Encrypted primary API key
    /// </summary>
    [JsonPropertyName("primary")]
    public string? Primary { get; set; }

    /// <summary>
    /// Encrypted fallback API key
    /// </summary>
    [JsonPropertyName("fallback")]
    public string? Fallback { get; set; }
}
