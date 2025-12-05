using RewriteAssistant.Models;

namespace RewriteAssistant.Services;

/// <summary>
/// Interface for application configuration management
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Loads the application configuration from storage.
    /// Returns default configuration if file doesn't exist or is corrupted.
    /// </summary>
    AppConfiguration Load();

    /// <summary>
    /// Saves the application configuration to storage.
    /// API keys are encrypted before storage.
    /// </summary>
    void Save(AppConfiguration config);

    /// <summary>
    /// Gets the path to the configuration file
    /// </summary>
    string ConfigurationFilePath { get; }

    /// <summary>
    /// Sets the primary API key (will be encrypted before storage)
    /// </summary>
    void SetPrimaryApiKey(AppConfiguration config, string plainTextKey);

    /// <summary>
    /// Sets the fallback API key (will be encrypted before storage)
    /// </summary>
    void SetFallbackApiKey(AppConfiguration config, string plainTextKey);

    /// <summary>
    /// Gets the decrypted primary API key
    /// </summary>
    string? GetPrimaryApiKey(AppConfiguration config);

    /// <summary>
    /// Gets the decrypted fallback API key
    /// </summary>
    string? GetFallbackApiKey(AppConfiguration config);

    // Prompt CRUD operations

    /// <summary>
    /// Adds a new prompt to the configuration
    /// </summary>
    void AddPrompt(CustomPrompt prompt);

    /// <summary>
    /// Updates an existing prompt in the configuration
    /// </summary>
    void UpdatePrompt(CustomPrompt prompt);

    /// <summary>
    /// Deletes a prompt from the configuration.
    /// Handles cascade delete for styles referencing the deleted prompt.
    /// </summary>
    void DeletePrompt(string promptId);

    /// <summary>
    /// Gets a prompt by its ID
    /// </summary>
    CustomPrompt? GetPrompt(string promptId);

    // Style CRUD operations

    /// <summary>
    /// Adds a new style to the configuration
    /// </summary>
    void AddStyle(CustomStyle style);

    /// <summary>
    /// Updates an existing style in the configuration
    /// </summary>
    void UpdateStyle(CustomStyle style);

    /// <summary>
    /// Deletes a style from the configuration.
    /// Unregisters the associated hotkey.
    /// </summary>
    void DeleteStyle(string styleId);

    /// <summary>
    /// Gets a style by its ID
    /// </summary>
    CustomStyle? GetStyle(string styleId);

    // Hotkey validation

    /// <summary>
    /// Validates a hotkey configuration for conflicts with existing hotkeys
    /// </summary>
    /// <param name="hotkey">The hotkey to validate</param>
    /// <param name="excludeStyleId">Optional style ID to exclude from conflict check (for updates)</param>
    HotkeyValidationResult ValidateHotkey(HotkeyConfig hotkey, string? excludeStyleId = null);

    // Events for hot reload

    /// <summary>
    /// Raised when a prompt is added, updated, or deleted
    /// </summary>
    event EventHandler<PromptChangedEventArgs>? PromptChanged;

    /// <summary>
    /// Raised when a style is added, updated, or deleted
    /// </summary>
    event EventHandler<StyleChangedEventArgs>? StyleChanged;

    /// <summary>
    /// Raised when a hotkey is added, updated, or deleted
    /// </summary>
    event EventHandler<HotkeyChangedEventArgs>? HotkeyChanged;
}
