using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RewriteAssistant.Models;

namespace RewriteAssistant.Services;

/// <summary>
/// Manages application configuration with encrypted API key storage.
/// Uses Windows DPAPI for encryption (user-scoped protection).
/// </summary>
public class ConfigurationManager : IConfigurationManager
{
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private AppConfiguration? _cachedConfig;

    /// <inheritdoc />
    public event EventHandler<PromptChangedEventArgs>? PromptChanged;

    /// <inheritdoc />
    public event EventHandler<StyleChangedEventArgs>? StyleChanged;

    /// <inheritdoc />
    public event EventHandler<HotkeyChangedEventArgs>? HotkeyChanged;

    /// <summary>
    /// Creates a new ConfigurationManager with the default config path
    /// </summary>
    public ConfigurationManager() : this(GetDefaultConfigPath())
    {
    }

    /// <summary>
    /// Creates a new ConfigurationManager with a custom config path
    /// </summary>
    public ConfigurationManager(string configFilePath)
    {
        _configFilePath = configFilePath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    public string ConfigurationFilePath => _configFilePath;

    /// <inheritdoc />
    public AppConfiguration Load()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                _cachedConfig = AppConfiguration.CreateDefault();
                return _cachedConfig;
            }

            var json = File.ReadAllText(_configFilePath);
            
            if (string.IsNullOrWhiteSpace(json))
            {
                _cachedConfig = AppConfiguration.CreateDefault();
                return _cachedConfig;
            }

            var config = JsonSerializer.Deserialize<AppConfiguration>(json, _jsonOptions);

            if (config == null)
            {
                _cachedConfig = AppConfiguration.CreateDefault();
                return _cachedConfig;
            }

            // Ensure collections are initialized
            config.Hotkeys ??= new List<HotkeyConfig>();
            config.Prompts ??= new List<CustomPrompt>();
            config.Styles ??= new List<CustomStyle>();
            config.ApiKeys ??= new ApiKeyStorage();

            _cachedConfig = config;
            return config;
        }
        catch (JsonException)
        {
            // Configuration file is corrupted, return defaults
            _cachedConfig = AppConfiguration.CreateDefault();
            return _cachedConfig;
        }
        catch (IOException)
        {
            // File access error, return defaults
            _cachedConfig = AppConfiguration.CreateDefault();
            return _cachedConfig;
        }
        catch (UnauthorizedAccessException)
        {
            // Permission error, return defaults
            _cachedConfig = AppConfiguration.CreateDefault();
            return _cachedConfig;
        }
    }

    /// <inheritdoc />
    public void Save(AppConfiguration config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(_configFilePath, json);
            _cachedConfig = config;
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Permission denied when saving configuration: {ex.Message}", ex);
        }
    }


    #region Prompt CRUD Operations

    /// <inheritdoc />
    public void AddPrompt(CustomPrompt prompt)
    {
        if (prompt == null)
            throw new ArgumentNullException(nameof(prompt));

        if (string.IsNullOrWhiteSpace(prompt.Id))
            throw new ArgumentException("Prompt ID cannot be empty", nameof(prompt));

        var config = GetOrLoadConfig();

        if (config.Prompts.Any(p => p.Id == prompt.Id))
            throw new InvalidOperationException($"Prompt with ID '{prompt.Id}' already exists");

        prompt.CreatedAt = DateTime.UtcNow;
        prompt.ModifiedAt = DateTime.UtcNow;

        config.Prompts.Add(prompt);
        Save(config);

        PromptChanged?.Invoke(this, new PromptChangedEventArgs(ChangeType.Added, prompt));
    }

    /// <inheritdoc />
    public void UpdatePrompt(CustomPrompt prompt)
    {
        if (prompt == null)
            throw new ArgumentNullException(nameof(prompt));

        if (string.IsNullOrWhiteSpace(prompt.Id))
            throw new ArgumentException("Prompt ID cannot be empty", nameof(prompt));

        var config = GetOrLoadConfig();

        var existingIndex = config.Prompts.FindIndex(p => p.Id == prompt.Id);
        if (existingIndex < 0)
            throw new InvalidOperationException($"Prompt with ID '{prompt.Id}' not found");

        var existing = config.Prompts[existingIndex];
        
        // Preserve creation date, update modification date
        prompt.CreatedAt = existing.CreatedAt;
        prompt.ModifiedAt = DateTime.UtcNow;

        config.Prompts[existingIndex] = prompt;
        Save(config);

        PromptChanged?.Invoke(this, new PromptChangedEventArgs(ChangeType.Updated, prompt, prompt.Id));
    }

    /// <inheritdoc />
    public void DeletePrompt(string promptId)
    {
        if (string.IsNullOrWhiteSpace(promptId))
            throw new ArgumentException("Prompt ID cannot be empty", nameof(promptId));

        var config = GetOrLoadConfig();

        var prompt = config.Prompts.FirstOrDefault(p => p.Id == promptId);
        if (prompt == null)
            throw new InvalidOperationException($"Prompt with ID '{promptId}' not found");

        if (prompt.IsBuiltIn)
            throw new InvalidOperationException("Cannot delete built-in prompts");

        // Handle cascade: update styles that reference this prompt
        var defaultPromptId = config.Prompts.FirstOrDefault(p => p.IsBuiltIn)?.Id ?? "";
        foreach (var style in config.Styles.Where(s => s.PromptId == promptId))
        {
            style.PromptId = defaultPromptId;
        }

        config.Prompts.Remove(prompt);
        Save(config);

        PromptChanged?.Invoke(this, new PromptChangedEventArgs(ChangeType.Deleted, prompt));
    }

    /// <inheritdoc />
    public CustomPrompt? GetPrompt(string promptId)
    {
        if (string.IsNullOrWhiteSpace(promptId))
            return null;

        var config = GetOrLoadConfig();
        return config.Prompts.FirstOrDefault(p => p.Id == promptId);
    }

    #endregion

    #region Style CRUD Operations

    /// <inheritdoc />
    public void AddStyle(CustomStyle style)
    {
        if (style == null)
            throw new ArgumentNullException(nameof(style));

        if (string.IsNullOrWhiteSpace(style.Id))
            throw new ArgumentException("Style ID cannot be empty", nameof(style));

        var config = GetOrLoadConfig();

        if (config.Styles.Any(s => s.Id == style.Id))
            throw new InvalidOperationException($"Style with ID '{style.Id}' already exists");

        // Validate hotkey if present
        if (style.Hotkey != null)
        {
            var validation = ValidateHotkey(style.Hotkey);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.ErrorMessage);
        }

        config.Styles.Add(style);
        Save(config);

        StyleChanged?.Invoke(this, new StyleChangedEventArgs(ChangeType.Added, style));

        // Fire hotkey event if hotkey was added
        if (style.Hotkey != null)
        {
            HotkeyChanged?.Invoke(this, new HotkeyChangedEventArgs(ChangeType.Added, style.Id, null, style.Hotkey));
        }
    }

    /// <inheritdoc />
    public void UpdateStyle(CustomStyle style)
    {
        if (style == null)
            throw new ArgumentNullException(nameof(style));

        if (string.IsNullOrWhiteSpace(style.Id))
            throw new ArgumentException("Style ID cannot be empty", nameof(style));

        var config = GetOrLoadConfig();

        var existingIndex = config.Styles.FindIndex(s => s.Id == style.Id);
        if (existingIndex < 0)
            throw new InvalidOperationException($"Style with ID '{style.Id}' not found");

        var existing = config.Styles[existingIndex];
        var oldHotkey = existing.Hotkey;

        // Validate hotkey if present (exclude current style from conflict check)
        if (style.Hotkey != null)
        {
            var validation = ValidateHotkey(style.Hotkey, style.Id);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.ErrorMessage);
        }

        config.Styles[existingIndex] = style;
        Save(config);

        StyleChanged?.Invoke(this, new StyleChangedEventArgs(ChangeType.Updated, style, oldHotkey));

        // Fire hotkey event if hotkey changed
        if (!HotkeysEqual(oldHotkey, style.Hotkey))
        {
            var changeType = (oldHotkey == null, style.Hotkey == null) switch
            {
                (true, false) => ChangeType.Added,
                (false, true) => ChangeType.Deleted,
                _ => ChangeType.Updated
            };
            HotkeyChanged?.Invoke(this, new HotkeyChangedEventArgs(changeType, style.Id, oldHotkey, style.Hotkey));
        }
    }

    /// <inheritdoc />
    public void DeleteStyle(string styleId)
    {
        if (string.IsNullOrWhiteSpace(styleId))
            throw new ArgumentException("Style ID cannot be empty", nameof(styleId));

        var config = GetOrLoadConfig();

        var style = config.Styles.FirstOrDefault(s => s.Id == styleId);
        if (style == null)
            throw new InvalidOperationException($"Style with ID '{styleId}' not found");

        if (style.IsBuiltIn)
            throw new InvalidOperationException("Cannot delete built-in styles");

        var oldHotkey = style.Hotkey;

        config.Styles.Remove(style);

        // Update default style if it was the deleted one
        if (config.DefaultStyleId == styleId)
        {
            config.DefaultStyleId = config.Styles.FirstOrDefault(s => s.IsBuiltIn)?.Id ?? "";
        }

        Save(config);

        StyleChanged?.Invoke(this, new StyleChangedEventArgs(ChangeType.Deleted, style, oldHotkey));

        // Fire hotkey event if style had a hotkey
        if (oldHotkey != null)
        {
            HotkeyChanged?.Invoke(this, new HotkeyChangedEventArgs(ChangeType.Deleted, styleId, oldHotkey, null));
        }
    }

    /// <inheritdoc />
    public CustomStyle? GetStyle(string styleId)
    {
        if (string.IsNullOrWhiteSpace(styleId))
            return null;

        var config = GetOrLoadConfig();
        return config.Styles.FirstOrDefault(s => s.Id == styleId);
    }

    #endregion

    #region Hotkey Validation

    /// <inheritdoc />
    public HotkeyValidationResult ValidateHotkey(HotkeyConfig hotkey, string? excludeStyleId = null)
    {
        if (hotkey == null)
            return HotkeyValidationResult.Invalid("Hotkey cannot be null");

        if (string.IsNullOrWhiteSpace(hotkey.Key))
            return HotkeyValidationResult.Invalid("Hotkey must have a key assigned");

        if (hotkey.Modifiers == null || hotkey.Modifiers.Count == 0)
            return HotkeyValidationResult.Invalid("Hotkey must have at least one modifier key");

        var config = GetOrLoadConfig();

        // Check for conflicts with existing hotkeys
        foreach (var style in config.Styles)
        {
            // Skip the style being updated
            if (excludeStyleId != null && style.Id == excludeStyleId)
                continue;

            if (style.Hotkey == null)
                continue;

            if (HotkeysEqual(style.Hotkey, hotkey))
            {
                return HotkeyValidationResult.Conflict(style.Id, style.Name);
            }
        }

        return HotkeyValidationResult.Valid();
    }

    private static bool HotkeysEqual(HotkeyConfig? a, HotkeyConfig? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        if (!string.Equals(a.Key, b.Key, StringComparison.OrdinalIgnoreCase))
            return false;

        var aModifiers = a.Modifiers?.Select(m => m.ToLowerInvariant()).OrderBy(m => m).ToList() ?? new List<string>();
        var bModifiers = b.Modifiers?.Select(m => m.ToLowerInvariant()).OrderBy(m => m).ToList() ?? new List<string>();

        return aModifiers.SequenceEqual(bModifiers);
    }

    #endregion


    #region API Key Management

    /// <inheritdoc />
    public void SetPrimaryApiKey(AppConfiguration config, string plainTextKey)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        
        config.ApiKeys ??= new ApiKeyStorage();
        config.ApiKeys.Primary = string.IsNullOrEmpty(plainTextKey) 
            ? null 
            : EncryptString(plainTextKey);
    }

    /// <inheritdoc />
    public void SetFallbackApiKey(AppConfiguration config, string plainTextKey)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        
        config.ApiKeys ??= new ApiKeyStorage();
        config.ApiKeys.Fallback = string.IsNullOrEmpty(plainTextKey) 
            ? null 
            : EncryptString(plainTextKey);
    }

    /// <inheritdoc />
    public string? GetPrimaryApiKey(AppConfiguration config)
    {
        if (config?.ApiKeys?.Primary == null)
        {
            return null;
        }

        return DecryptString(config.ApiKeys.Primary);
    }

    /// <inheritdoc />
    public string? GetFallbackApiKey(AppConfiguration config)
    {
        if (config?.ApiKeys?.Fallback == null)
        {
            return null;
        }

        return DecryptString(config.ApiKeys.Fallback);
    }

    #endregion

    #region Private Helpers

    private AppConfiguration GetOrLoadConfig()
    {
        return _cachedConfig ?? Load();
    }

    /// <summary>
    /// Encrypts a string using Windows DPAPI (user-scoped)
    /// </summary>
    private static string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(
            plainBytes, 
            null, 
            DataProtectionScope.CurrentUser);
        
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Decrypts a string using Windows DPAPI (user-scoped)
    /// </summary>
    private static string? DecryptString(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
        {
            return null;
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = ProtectedData.Unprotect(
                encryptedBytes, 
                null, 
                DataProtectionScope.CurrentUser);
            
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            // Invalid base64, return null
            return null;
        }
        catch (CryptographicException)
        {
            // Decryption failed (wrong user, corrupted data), return null
            return null;
        }
    }

    /// <summary>
    /// Gets the default configuration file path in the user's AppData folder
    /// </summary>
    private static string GetDefaultConfigPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "RewriteAssistant", "config.json");
    }

    #endregion
}
