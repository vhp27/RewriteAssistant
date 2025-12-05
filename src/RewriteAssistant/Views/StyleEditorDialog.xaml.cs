using System.Windows;
using System.Windows.Controls;
using RewriteAssistant.Controls;
using RewriteAssistant.Models;

namespace RewriteAssistant.Views;

/// <summary>
/// Dialog for creating and editing custom rewrite styles.
/// Requirements: 3.2, 3.3
/// </summary>
public partial class StyleEditorDialog : Window
{
    private CustomStyle? _existingStyle;
    private List<CustomPrompt> _prompts = new();
    private HotkeyConfig? _capturedHotkey;
    
    /// <summary>
    /// Gets the resulting style after the dialog is closed with Save.
    /// </summary>
    public CustomStyle? ResultStyle { get; private set; }
    
    /// <summary>
    /// Delegate for validating hotkey configurations.
    /// Set this before showing the dialog to enable conflict detection.
    /// </summary>
    public Func<HotkeyConfig, string?, HotkeyValidationResult>? ValidateHotkey { get; set; }
    
    /// <summary>
    /// Creates a new StyleEditorDialog for creating a new style.
    /// </summary>
    public StyleEditorDialog()
    {
        InitializeComponent();
        DialogTitle.Text = "New Style";
        Title = "New Style";
    }
    
    /// <summary>
    /// Creates a new StyleEditorDialog for editing an existing style.
    /// </summary>
    /// <param name="style">The style to edit</param>
    public StyleEditorDialog(CustomStyle style) : this()
    {
        _existingStyle = style;
        DialogTitle.Text = "Edit Style";
        Title = "Edit Style";
        
        // Populate fields with existing values
        NameTextBox.Text = style.Name;
        _capturedHotkey = style.Hotkey;
        
        // Disable name editing for built-in styles
        if (style.IsBuiltIn)
        {
            NameTextBox.IsEnabled = false;
            NameTextBox.ToolTip = "Built-in style names cannot be changed";
        }
    }

    /// <summary>
    /// Sets the available prompts for the dropdown.
    /// Must be called before showing the dialog.
    /// </summary>
    /// <param name="prompts">List of available prompts</param>
    public void SetPrompts(List<CustomPrompt> prompts)
    {
        _prompts = prompts ?? new List<CustomPrompt>();
        PromptComboBox.ItemsSource = _prompts;
        
        // Select the existing prompt if editing
        if (_existingStyle != null && !string.IsNullOrEmpty(_existingStyle.PromptId))
        {
            PromptComboBox.SelectedValue = _existingStyle.PromptId;
        }
        else if (_prompts.Count > 0)
        {
            // Select first prompt by default for new styles
            PromptComboBox.SelectedIndex = 0;
        }
        
        // Set up hotkey editor after prompts are loaded
        SetupHotkeyEditor();
        
        ValidateForm();
    }
    
    private void SetupHotkeyEditor()
    {
        // Set the current hotkey if editing
        HotkeyEditor.CurrentHotkey = _capturedHotkey;
        
        // Set up validation delegate
        HotkeyEditor.ValidateHotkey = hotkey =>
        {
            if (ValidateHotkey != null)
            {
                return ValidateHotkey(hotkey, _existingStyle?.Id);
            }
            return HotkeyValidationResult.Valid();
        };
    }

    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateForm();
    }
    
    private void PromptComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePromptPreview();
        ValidateForm();
    }
    
    private void HotkeyEditor_HotkeyCaptured(object? sender, HotkeyCapturedEventArgs e)
    {
        _capturedHotkey = e.Hotkey;
        ValidateForm();
    }
    
    private void UpdatePromptPreview()
    {
        var selectedPrompt = PromptComboBox.SelectedItem as CustomPrompt;
        if (selectedPrompt != null && !string.IsNullOrEmpty(selectedPrompt.PromptText))
        {
            PromptPreviewBorder.Visibility = Visibility.Visible;
            // Show first 200 characters of prompt text
            var previewText = selectedPrompt.PromptText;
            if (previewText.Length > 200)
            {
                previewText = previewText.Substring(0, 200) + "...";
            }
            PromptPreviewText.Text = previewText;
        }
        else
        {
            PromptPreviewBorder.Visibility = Visibility.Collapsed;
        }
    }
    
    private void ValidateForm()
    {
        var nameValid = !string.IsNullOrWhiteSpace(NameTextBox.Text);
        var promptValid = PromptComboBox.SelectedItem != null;
        var hotkeyValid = !HotkeyEditor.HasValidationError;
        
        // Show/hide error messages
        NameErrorText.Visibility = nameValid ? Visibility.Collapsed : Visibility.Visible;
        PromptErrorText.Visibility = promptValid ? Visibility.Collapsed : Visibility.Visible;
        
        // Enable save button when required fields are valid and no hotkey conflicts
        SaveButton.IsEnabled = nameValid && promptValid && hotkeyValid;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Final validation
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            ValidateForm();
            return;
        }
        
        var selectedPrompt = PromptComboBox.SelectedItem as CustomPrompt;
        if (selectedPrompt == null)
        {
            ValidateForm();
            return;
        }
        
        // Check for hotkey validation errors
        if (HotkeyEditor.HasValidationError)
        {
            return;
        }
        
        // Validate hotkey one more time if present
        if (_capturedHotkey != null && ValidateHotkey != null)
        {
            var validation = ValidateHotkey(_capturedHotkey, _existingStyle?.Id);
            if (!validation.IsValid)
            {
                HotkeyEditor.SetValidationError(validation.ErrorMessage, !string.IsNullOrEmpty(validation.ConflictingStyleId));
                return;
            }
        }
        
        ResultStyle = new CustomStyle
        {
            Id = _existingStyle?.Id ?? Guid.NewGuid().ToString(),
            Name = NameTextBox.Text.Trim(),
            PromptId = selectedPrompt.Id,
            Hotkey = _capturedHotkey,
            IsBuiltIn = _existingStyle?.IsBuiltIn ?? false
        };
        
        DialogResult = true;
        Close();
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
