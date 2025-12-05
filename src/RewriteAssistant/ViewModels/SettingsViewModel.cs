using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using RewriteAssistant.Models;
using RewriteAssistant.Services;

namespace RewriteAssistant.ViewModels;

/// <summary>
/// Simple ICommand implementation for ViewModel commands
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
}

/// <summary>
/// View model for displaying prompts in the list
/// </summary>
public class PromptListItem : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _promptText = string.Empty;
    private bool _isBuiltIn;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string PromptText
    {
        get => _promptText;
        set { _promptText = value; OnPropertyChanged(); }
    }

    public bool IsBuiltIn
    {
        get => _isBuiltIn;
        set { _isBuiltIn = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// View model for displaying styles in the list
/// </summary>
public class StyleListItem : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _promptId = string.Empty;
    private string _promptName = string.Empty;
    private string _promptPreview = string.Empty;
    private string _hotkeyDisplay = string.Empty;
    private bool _isBuiltIn;
    private HotkeyConfig? _hotkey;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string PromptId
    {
        get => _promptId;
        set { _promptId = value; OnPropertyChanged(); }
    }

    public string PromptName
    {
        get => _promptName;
        set { _promptName = value; OnPropertyChanged(); }
    }

    public string PromptPreview
    {
        get => _promptPreview;
        set { _promptPreview = value; OnPropertyChanged(); }
    }

    public string HotkeyDisplay
    {
        get => _hotkeyDisplay;
        set { _hotkeyDisplay = value; OnPropertyChanged(); }
    }

    public bool IsBuiltIn
    {
        get => _isBuiltIn;
        set { _isBuiltIn = value; OnPropertyChanged(); }
    }

    public HotkeyConfig? Hotkey
    {
        get => _hotkey;
        set { _hotkey = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// View model for the settings window.
/// Implements Requirements 2.2, 2.3, 2.4, 2.5, 3.2, 3.3, 3.4
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IConfigurationManager _configManager;
    private AppConfiguration _config;
    
    /// <summary>
    /// Gets the configuration manager for CRUD operations
    /// </summary>
    public IConfigurationManager ConfigurationManager => _configManager;
    
    private bool _isEnabled;
    private bool _startWithWindows;
    private bool _showSuccessNotification = true;
    private string _defaultStyle = "grammar_fix";
    private string _primaryApiKey = string.Empty;
    private string _fallbackApiKey = string.Empty;
    
    // Prompt management
    private PromptListItem? _selectedPrompt;
    
    // Style management
    private StyleListItem? _selectedStyle;

    /// <summary>
    /// Event raised when a property value changes
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Event raised when the enabled state changes
    /// </summary>
    public event EventHandler<bool>? EnabledChanged;
    
    /// <summary>
    /// Event raised when a prompt add is requested (UI should show dialog)
    /// </summary>
    public event EventHandler? AddPromptRequested;
    
    /// <summary>
    /// Event raised when a prompt edit is requested (UI should show dialog)
    /// </summary>
    public event EventHandler<CustomPrompt>? EditPromptRequested;
    
    /// <summary>
    /// Event raised when a prompt delete confirmation is needed
    /// </summary>
    public event EventHandler<PromptDeleteEventArgs>? DeletePromptRequested;
    
    /// <summary>
    /// Event raised when a style add is requested (UI should show dialog)
    /// </summary>
    public event EventHandler? AddStyleRequested;
    
    /// <summary>
    /// Event raised when a style edit is requested (UI should show dialog)
    /// </summary>
    public event EventHandler<CustomStyle>? EditStyleRequested;
    
    /// <summary>
    /// Event raised when a style delete confirmation is needed
    /// </summary>
    public event EventHandler<StyleDeleteEventArgs>? DeleteStyleRequested;
    
    /// <summary>
    /// Event raised when an error occurs during CRUD operations
    /// </summary>
    public event EventHandler<string>? ErrorOccurred;
    
    /// <summary>
    /// Collection of prompts for UI binding
    /// </summary>
    public ObservableCollection<PromptListItem> Prompts { get; } = new();
    
    /// <summary>
    /// Collection of styles for UI binding
    /// </summary>
    public ObservableCollection<StyleListItem> Styles { get; } = new();
    
    /// <summary>
    /// Collection of raw CustomStyle objects for dropdown binding
    /// </summary>
    public ObservableCollection<CustomStyle> StylesRaw { get; } = new();
    
    /// <summary>
    /// Collection of raw CustomPrompt objects
    /// </summary>
    public ObservableCollection<CustomPrompt> PromptsRaw { get; } = new();
    
    /// <summary>
    /// Currently selected prompt in the list
    /// </summary>
    public PromptListItem? SelectedPrompt
    {
        get => _selectedPrompt;
        set
        {
            if (_selectedPrompt != value)
            {
                _selectedPrompt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanEditPrompt));
                OnPropertyChanged(nameof(CanDeletePrompt));
            }
        }
    }
    
    /// <summary>
    /// Currently selected style in the list
    /// </summary>
    public StyleListItem? SelectedStyle
    {
        get => _selectedStyle;
        set
        {
            if (_selectedStyle != value)
            {
                _selectedStyle = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanEditStyle));
                OnPropertyChanged(nameof(CanDeleteStyle));
            }
        }
    }
    
    /// <summary>
    /// Whether the selected prompt can be edited
    /// </summary>
    public bool CanEditPrompt => SelectedPrompt != null;
    
    /// <summary>
    /// Whether the selected prompt can be deleted (not built-in)
    /// </summary>
    public bool CanDeletePrompt => SelectedPrompt != null && !SelectedPrompt.IsBuiltIn;
    
    /// <summary>
    /// Whether the selected style can be edited
    /// </summary>
    public bool CanEditStyle => SelectedStyle != null;
    
    /// <summary>
    /// Whether the selected style can be deleted (not built-in)
    /// </summary>
    public bool CanDeleteStyle => SelectedStyle != null && !SelectedStyle.IsBuiltIn;
    
    // Commands
    public ICommand AddPromptCommand { get; }
    public ICommand EditPromptCommand { get; }
    public ICommand DeletePromptCommand { get; }
    public ICommand AddStyleCommand { get; }
    public ICommand EditStyleCommand { get; }
    public ICommand DeleteStyleCommand { get; }

    /// <summary>
    /// Creates a new SettingsViewModel
    /// </summary>
    public SettingsViewModel(IConfigurationManager configManager)
    {
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _config = _configManager.Load();
        
        // Initialize commands
        AddPromptCommand = new RelayCommand(_ => OnAddPrompt());
        EditPromptCommand = new RelayCommand(_ => OnEditPrompt(), _ => CanEditPrompt);
        DeletePromptCommand = new RelayCommand(_ => OnDeletePrompt(), _ => CanDeletePrompt);
        AddStyleCommand = new RelayCommand(_ => OnAddStyle());
        EditStyleCommand = new RelayCommand(_ => OnEditStyle(), _ => CanEditStyle);
        DeleteStyleCommand = new RelayCommand(_ => OnDeleteStyle(), _ => CanDeleteStyle);
        
        LoadFromConfig();
        LoadPromptsAndStyles();
    }

    /// <summary>
    /// Whether the application is enabled
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                OnPropertyChanged();
                EnabledChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Whether to start with Windows
    /// </summary>
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows != value)
            {
                _startWithWindows = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether to show notification on successful rewrite
    /// </summary>
    public bool ShowSuccessNotification
    {
        get => _showSuccessNotification;
        set
        {
            if (_showSuccessNotification != value)
            {
                _showSuccessNotification = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Default rewrite style
    /// </summary>
    public string DefaultStyle
    {
        get => _defaultStyle;
        set
        {
            if (_defaultStyle != value)
            {
                _defaultStyle = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Primary API key (plaintext for UI binding)
    /// </summary>
    public string PrimaryApiKey
    {
        get => _primaryApiKey;
        set
        {
            if (_primaryApiKey != value)
            {
                _primaryApiKey = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Fallback API key (plaintext for UI binding)
    /// </summary>
    public string FallbackApiKey
    {
        get => _fallbackApiKey;
        set
        {
            if (_fallbackApiKey != value)
            {
                _fallbackApiKey = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Loads settings from the configuration
    /// </summary>
    private void LoadFromConfig()
    {
        _isEnabled = _config.IsEnabled;
        _startWithWindows = _config.StartWithWindows;
        _showSuccessNotification = _config.ShowSuccessNotification;
        _defaultStyle = _config.DefaultStyleId;
        
        // Decrypt API keys for display
        _primaryApiKey = _configManager.GetPrimaryApiKey(_config) ?? string.Empty;
        _fallbackApiKey = _configManager.GetFallbackApiKey(_config) ?? string.Empty;
    }

    /// <summary>
    /// Reloads settings from disk
    /// </summary>
    public void Reload()
    {
        _config = _configManager.Load();
        LoadFromConfig();
        
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(StartWithWindows));
        OnPropertyChanged(nameof(ShowSuccessNotification));
        OnPropertyChanged(nameof(DefaultStyle));
        OnPropertyChanged(nameof(PrimaryApiKey));
        OnPropertyChanged(nameof(FallbackApiKey));
    }


    /// <summary>
    /// Validates the current settings
    /// </summary>
    /// <returns>Error message if validation fails, null if valid</returns>
    public string? Validate()
    {
        // Validate primary API key format if provided
        if (!string.IsNullOrWhiteSpace(_primaryApiKey))
        {
            if (!IsValidApiKeyFormat(_primaryApiKey))
            {
                return "Primary API key format is invalid. Please check your key.";
            }
        }

        // Validate fallback API key format if provided
        if (!string.IsNullOrWhiteSpace(_fallbackApiKey))
        {
            if (!IsValidApiKeyFormat(_fallbackApiKey))
            {
                return "Fallback API key format is invalid. Please check your key.";
            }
        }

        // Validate default style
        if (!IsValidStyle(_defaultStyle))
        {
            return "Invalid default style selected.";
        }

        return null;
    }

    /// <summary>
    /// Validates API key format.
    /// Cerebras API keys typically follow a specific pattern.
    /// </summary>
    private static bool IsValidApiKeyFormat(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        // Basic validation: API keys should be alphanumeric with possible dashes/underscores
        // and have a reasonable length (typically 32-128 characters)
        if (apiKey.Length < 10 || apiKey.Length > 256)
        {
            return false;
        }

        // Allow alphanumeric, dashes, underscores, and dots
        var validPattern = new Regex(@"^[a-zA-Z0-9\-_\.]+$");
        return validPattern.IsMatch(apiKey);
    }

    /// <summary>
    /// Validates that the style exists in the configuration
    /// </summary>
    private bool IsValidStyle(string styleId)
    {
        if (string.IsNullOrWhiteSpace(styleId))
            return false;
        
        // Check if the style exists in the current configuration
        return _config.Styles?.Any(s => s.Id == styleId) ?? false;
    }

    /// <summary>
    /// Saves the current settings to configuration
    /// </summary>
    /// <returns>True if save was successful</returns>
    public bool Save()
    {
        try
        {
            // Update config object
            _config.IsEnabled = _isEnabled;
            _config.StartWithWindows = _startWithWindows;
            _config.ShowSuccessNotification = _showSuccessNotification;
            _config.DefaultStyleId = _defaultStyle;

            // Encrypt and store API keys
            _configManager.SetPrimaryApiKey(_config, _primaryApiKey);
            _configManager.SetFallbackApiKey(_config, _fallbackApiKey);

            // Save to disk
            _configManager.Save(_config);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current configuration (for external use)
    /// </summary>
    public AppConfiguration GetConfiguration()
    {
        return _config;
    }

    #region Prompt CRUD Operations

    /// <summary>
    /// Loads prompts and styles from configuration into observable collections
    /// </summary>
    public void LoadPromptsAndStyles()
    {
        _config = _configManager.Load();
        var prompts = _config.Prompts ?? new List<CustomPrompt>();
        var styles = _config.Styles ?? new List<CustomStyle>();
        
        // Update Prompts collection
        Prompts.Clear();
        PromptsRaw.Clear();
        foreach (var p in prompts)
        {
            PromptsRaw.Add(p);
            Prompts.Add(new PromptListItem
            {
                Id = p.Id,
                Name = p.Name,
                PromptText = p.PromptText.Length > 50 ? p.PromptText.Substring(0, 50) + "..." : p.PromptText,
                IsBuiltIn = p.IsBuiltIn
            });
        }
        
        // Update Styles collection
        Styles.Clear();
        StylesRaw.Clear();
        foreach (var s in styles)
        {
            StylesRaw.Add(s);
            Styles.Add(new StyleListItem
            {
                Id = s.Id,
                Name = s.Name,
                PromptId = s.PromptId,
                PromptName = prompts.FirstOrDefault(p => p.Id == s.PromptId)?.Name ?? "Unknown",
                PromptPreview = GetPromptPreview(s.PromptId, prompts),
                HotkeyDisplay = FormatHotkey(s.Hotkey),
                IsBuiltIn = s.IsBuiltIn,
                Hotkey = s.Hotkey
            });
        }
    }

    private void OnAddPrompt()
    {
        AddPromptRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnEditPrompt()
    {
        if (SelectedPrompt == null) return;
        
        var prompt = PromptsRaw.FirstOrDefault(p => p.Id == SelectedPrompt.Id);
        if (prompt != null)
        {
            EditPromptRequested?.Invoke(this, prompt);
        }
    }

    private void OnDeletePrompt()
    {
        if (SelectedPrompt == null || SelectedPrompt.IsBuiltIn) return;
        
        var prompt = PromptsRaw.FirstOrDefault(p => p.Id == SelectedPrompt.Id);
        if (prompt == null) return;
        
        // Find styles using this prompt
        var stylesUsingPrompt = StylesRaw.Where(s => s.PromptId == prompt.Id).ToList();
        
        var args = new PromptDeleteEventArgs(prompt, stylesUsingPrompt.Select(s => s.Name).ToList());
        DeletePromptRequested?.Invoke(this, args);
    }

    /// <summary>
    /// Adds a new prompt to the configuration
    /// </summary>
    public void AddPrompt(CustomPrompt prompt)
    {
        try
        {
            _configManager.AddPrompt(prompt);
            LoadPromptsAndStyles();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to add prompt: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates an existing prompt in the configuration
    /// </summary>
    public void UpdatePrompt(CustomPrompt prompt)
    {
        try
        {
            _configManager.UpdatePrompt(prompt);
            LoadPromptsAndStyles();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to update prompt: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a prompt from the configuration
    /// </summary>
    public void DeletePrompt(string promptId)
    {
        try
        {
            _configManager.DeletePrompt(promptId);
            LoadPromptsAndStyles();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to delete prompt: {ex.Message}");
        }
    }

    #endregion

    #region Style CRUD Operations

    private void OnAddStyle()
    {
        AddStyleRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnEditStyle()
    {
        if (SelectedStyle == null) return;
        
        var style = StylesRaw.FirstOrDefault(s => s.Id == SelectedStyle.Id);
        if (style != null)
        {
            EditStyleRequested?.Invoke(this, style);
        }
    }

    private void OnDeleteStyle()
    {
        if (SelectedStyle == null || SelectedStyle.IsBuiltIn) return;
        
        var style = StylesRaw.FirstOrDefault(s => s.Id == SelectedStyle.Id);
        if (style == null) return;
        
        var args = new StyleDeleteEventArgs(style);
        DeleteStyleRequested?.Invoke(this, args);
    }

    /// <summary>
    /// Adds a new style to the configuration
    /// </summary>
    public void AddStyle(CustomStyle style)
    {
        try
        {
            _configManager.AddStyle(style);
            LoadPromptsAndStyles();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to add style: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates an existing style in the configuration
    /// </summary>
    public void UpdateStyle(CustomStyle style)
    {
        try
        {
            _configManager.UpdateStyle(style);
            LoadPromptsAndStyles();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to update style: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a style from the configuration
    /// </summary>
    public void DeleteStyle(string styleId)
    {
        try
        {
            _configManager.DeleteStyle(styleId);
            LoadPromptsAndStyles();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to delete style: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    private static string GetPromptPreview(string promptId, List<CustomPrompt> prompts)
    {
        var prompt = prompts.FirstOrDefault(p => p.Id == promptId);
        if (prompt == null) return string.Empty;
        
        var text = prompt.PromptText;
        return text.Length > 200 ? text.Substring(0, 200) + "..." : text;
    }

    private static string FormatHotkey(HotkeyConfig? hotkey)
    {
        if (hotkey == null || string.IsNullOrEmpty(hotkey.Key))
            return "No hotkey";
        
        var parts = new List<string>();
        foreach (var modifier in hotkey.Modifiers ?? new List<string>())
        {
            var formatted = modifier.ToLowerInvariant() switch
            {
                "ctrl" or "control" => "Ctrl",
                "alt" => "Alt",
                "shift" => "Shift",
                "win" or "windows" => "Win",
                _ => modifier
            };
            parts.Add(formatted);
        }
        parts.Add(hotkey.Key.ToUpperInvariant());
        return string.Join("+", parts);
    }

    #endregion

    /// <summary>
    /// Raises the PropertyChanged event
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Event args for prompt deletion confirmation
/// </summary>
public class PromptDeleteEventArgs : EventArgs
{
    public CustomPrompt Prompt { get; }
    public List<string> AffectedStyleNames { get; }
    public bool Confirmed { get; set; }

    public PromptDeleteEventArgs(CustomPrompt prompt, List<string> affectedStyleNames)
    {
        Prompt = prompt;
        AffectedStyleNames = affectedStyleNames;
    }
}

/// <summary>
/// Event args for style deletion confirmation
/// </summary>
public class StyleDeleteEventArgs : EventArgs
{
    public CustomStyle Style { get; }
    public bool Confirmed { get; set; }

    public StyleDeleteEventArgs(CustomStyle style)
    {
        Style = style;
    }
}
