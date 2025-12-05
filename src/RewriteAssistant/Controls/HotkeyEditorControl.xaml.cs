using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RewriteAssistant.Models;

namespace RewriteAssistant.Controls;

/// <summary>
/// Event arguments for hotkey capture events
/// </summary>
public class HotkeyCapturedEventArgs : EventArgs
{
    public HotkeyConfig? Hotkey { get; }
    
    public HotkeyCapturedEventArgs(HotkeyConfig? hotkey)
    {
        Hotkey = hotkey;
    }
}

/// <summary>
/// A control for capturing and editing hotkey configurations.
/// Requirements: 1.2, 6.2
/// </summary>
public partial class HotkeyEditorControl : UserControl
{
    private bool _isCapturing;
    private HotkeyConfig? _currentHotkey;
    private string? _validationError;
    
    /// <summary>
    /// Event raised when a hotkey is captured or cleared
    /// </summary>
    public event EventHandler<HotkeyCapturedEventArgs>? HotkeyCaptured;
    
    /// <summary>
    /// Delegate for validating hotkey configurations
    /// </summary>
    public Func<HotkeyConfig, HotkeyValidationResult>? ValidateHotkey { get; set; }
    
    /// <summary>
    /// Gets whether the current hotkey has a validation error
    /// </summary>
    public bool HasValidationError => !string.IsNullOrEmpty(_validationError);
    
    /// <summary>
    /// Gets whether the current hotkey has a conflict with another hotkey
    /// </summary>
    public bool HasConflict { get; private set; }

    /// <summary>
    /// Gets or sets the current hotkey configuration
    /// </summary>
    public HotkeyConfig? CurrentHotkey
    {
        get => _currentHotkey;
        set
        {
            _currentHotkey = value;
            UpdateDisplay();
        }
    }
    
    /// <summary>
    /// Gets whether the control is currently capturing a hotkey
    /// </summary>
    public bool IsCapturing => _isCapturing;
    
    /// <summary>
    /// Gets or sets the validation error message
    /// </summary>
    public string? ValidationError
    {
        get => _validationError;
        set
        {
            _validationError = value;
            UpdateValidationDisplay();
        }
    }
    
    public HotkeyEditorControl()
    {
        InitializeComponent();
        UpdateDisplay();
    }
    
    /// <summary>
    /// Starts capturing a hotkey combination
    /// </summary>
    public void StartCapture()
    {
        _isCapturing = true;
        HotkeyDisplay.Visibility = Visibility.Collapsed;
        CapturingText.Visibility = Visibility.Visible;
        HotkeyBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 150, 136)); // #009688
        HotkeyBorder.BorderThickness = new Thickness(1.5);
        ClearValidationError();
        Focus();
    }
    
    /// <summary>
    /// Stops capturing and reverts to display mode
    /// </summary>
    public void StopCapture()
    {
        _isCapturing = false;
        HotkeyDisplay.Visibility = Visibility.Visible;
        CapturingText.Visibility = Visibility.Collapsed;
        HotkeyBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)); // #E2E8F0
        HotkeyBorder.BorderThickness = new Thickness(1);
    }
    
    /// <summary>
    /// Clears the current hotkey
    /// </summary>
    public void ClearHotkey()
    {
        _currentHotkey = null;
        UpdateDisplay();
        ClearValidationError();
        HotkeyCaptured?.Invoke(this, new HotkeyCapturedEventArgs(null));
    }
    
    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (!_isCapturing)
        {
            StartCapture();
        }
    }
    
    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        StopCapture();
    }
    
    private void OnBorderClick(object sender, MouseButtonEventArgs e)
    {
        StartCapture();
    }
    
    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        ClearHotkey();
        e.Handled = true;
    }
    
    /// <summary>
    /// Handles mouse wheel events to allow scrolling in parent ScrollViewer
    /// </summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Don't handle the event - let it bubble up to parent ScrollViewer
        // This fixes the scrolling issue when the control has focus
        e.Handled = false;
        
        // Find parent ScrollViewer and scroll it
        var parent = VisualTreeHelper.GetParent(this) as DependencyObject;
        while (parent != null)
        {
            if (parent is System.Windows.Controls.ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3.0);
                e.Handled = true;
                break;
            }
            parent = VisualTreeHelper.GetParent(parent);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isCapturing)
            return;
        
        e.Handled = true;
        
        // Ignore standalone modifier keys
        if (IsModifierKey(e.Key))
            return;
        
        // Escape cancels capture
        if (e.Key == Key.Escape)
        {
            StopCapture();
            return;
        }
        
        // Build the hotkey configuration
        var modifiers = new List<string>();
        
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            modifiers.Add("ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            modifiers.Add("alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            modifiers.Add("shift");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
            modifiers.Add("win");
        
        // Require at least one modifier
        if (modifiers.Count == 0)
        {
            ShowValidationError("Hotkey must include at least one modifier (Ctrl, Alt, Shift, or Win)");
            return;
        }
        
        // Get the actual key (handle system keys)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var keyString = ConvertKeyToString(key);
        
        if (string.IsNullOrEmpty(keyString))
        {
            ShowValidationError("Invalid key combination");
            return;
        }
        
        var hotkey = new HotkeyConfig
        {
            Id = _currentHotkey?.Id ?? Guid.NewGuid().ToString(),
            Modifiers = modifiers,
            Key = keyString
        };
        
        // Validate the hotkey if a validator is provided
        if (ValidateHotkey != null)
        {
            var result = ValidateHotkey(hotkey);
            if (!result.IsValid)
            {
                // Check if this is a conflict (has conflicting style info)
                var isConflict = !string.IsNullOrEmpty(result.ConflictingStyleId);
                ShowValidationError(result.ErrorMessage ?? "Invalid hotkey", isConflict);
                return;
            }
        }
        
        // Accept the hotkey
        _currentHotkey = hotkey;
        UpdateDisplay();
        StopCapture();
        ClearValidationError();
        
        HotkeyCaptured?.Invoke(this, new HotkeyCapturedEventArgs(hotkey));
    }
    
    private static bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl ||
               key == Key.LeftAlt || key == Key.RightAlt ||
               key == Key.LeftShift || key == Key.RightShift ||
               key == Key.LWin || key == Key.RWin;
    }
    
    private static string? ConvertKeyToString(Key key)
    {
        // Letters A-Z
        if (key >= Key.A && key <= Key.Z)
            return key.ToString();
        
        // Numbers 0-9
        if (key >= Key.D0 && key <= Key.D9)
            return ((int)key - (int)Key.D0).ToString();
        
        // Numpad numbers
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return ((int)key - (int)Key.NumPad0).ToString();
        
        // Function keys F1-F12
        if (key >= Key.F1 && key <= Key.F12)
            return key.ToString();
        
        // Special keys
        return key switch
        {
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.OemTilde => "`",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            _ => null
        };
    }

    private void UpdateDisplay()
    {
        if (_currentHotkey == null || string.IsNullOrEmpty(_currentHotkey.Key))
        {
            HotkeyDisplay.Text = "Click to set hotkey";
            HotkeyDisplay.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // #94A3B8
            ClearButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            HotkeyDisplay.Text = FormatHotkey(_currentHotkey);
            HotkeyDisplay.Foreground = new SolidColorBrush(Color.FromRgb(0, 150, 136)); // #009688
            HotkeyDisplay.FontWeight = FontWeights.Medium;
            ClearButton.Visibility = Visibility.Visible;
        }
    }
    
    private static string FormatHotkey(HotkeyConfig hotkey)
    {
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
        
        if (!string.IsNullOrEmpty(hotkey.Key))
        {
            parts.Add(hotkey.Key.ToUpperInvariant());
        }
        
        return string.Join("+", parts);
    }
    
    private void ShowValidationError(string message, bool isConflict = false)
    {
        _validationError = message;
        HasConflict = isConflict;
        UpdateValidationDisplay();
    }
    
    private void ClearValidationError()
    {
        _validationError = null;
        HasConflict = false;
        UpdateValidationDisplay();
    }
    
    private void UpdateValidationDisplay()
    {
        if (string.IsNullOrEmpty(_validationError))
        {
            ErrorBorder.Visibility = Visibility.Collapsed;
            HotkeyBorder.BorderBrush = _isCapturing 
                ? new SolidColorBrush(Color.FromRgb(0, 150, 136))  // #009688
                : new SolidColorBrush(Color.FromRgb(226, 232, 240)); // #E2E8F0
        }
        else
        {
            ErrorBorder.Visibility = Visibility.Visible;
            ErrorText.Text = _validationError;
            // Show different icon for conflicts vs other errors
            ErrorIcon.Text = HasConflict ? "🔄" : "⚠";
            HotkeyBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // #DC2626
        }
    }
    
    /// <summary>
    /// Sets a validation error externally (for use by parent controls)
    /// </summary>
    public void SetValidationError(string? errorMessage, bool isConflict = false)
    {
        if (string.IsNullOrEmpty(errorMessage))
        {
            ClearValidationError();
        }
        else
        {
            ShowValidationError(errorMessage, isConflict);
        }
    }
}
