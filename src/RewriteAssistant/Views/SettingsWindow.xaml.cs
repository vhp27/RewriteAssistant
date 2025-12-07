using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RewriteAssistant.Models;
using RewriteAssistant.Services;
using RewriteAssistant.ViewModels;

namespace RewriteAssistant.Views;

/// <summary>
/// Settings window for the Rewrite Assistant application.
/// Implements Requirements 2.1, 3.2, 3.3, 3.5, 6.1
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly IConfigurationManager _configManager;

    /// <summary>
    /// Event raised when configuration is saved
    /// </summary>
    public event EventHandler<AppConfiguration>? ConfigurationSaved;

    /// <summary>
    /// Creates a new SettingsWindow with a SettingsViewModel
    /// </summary>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _configManager = viewModel.ConfigurationManager;
        DataContext = _viewModel;
        
        SubscribeToViewModelEvents();
        LoadSettings();
    }

    /// <summary>
    /// Creates a new SettingsWindow with configuration manager and config
    /// </summary>
    public SettingsWindow(IConfigurationManager configManager, AppConfiguration config)
    {
        InitializeComponent();
        _configManager = configManager;
        _viewModel = new SettingsViewModel(configManager);
        DataContext = _viewModel;
        
        SubscribeToViewModelEvents();
        LoadSettings();
    }

    /// <summary>
    /// Subscribes to ViewModel events for CRUD operations
    /// </summary>
    private void SubscribeToViewModelEvents()
    {
        _viewModel.AddPromptRequested += OnAddPromptRequested;
        _viewModel.EditPromptRequested += OnEditPromptRequested;
        _viewModel.DeletePromptRequested += OnDeletePromptRequested;
        _viewModel.AddStyleRequested += OnAddStyleRequested;
        _viewModel.EditStyleRequested += OnEditStyleRequested;
        _viewModel.DeleteStyleRequested += OnDeleteStyleRequested;
        _viewModel.ErrorOccurred += OnErrorOccurred;
    }

    /// <summary>
    /// Loads current settings into the UI controls
    /// </summary>
    private void LoadSettings()
    {
        EnabledToggle.IsChecked = _viewModel.IsEnabled;
        StartWithWindowsCheckbox.IsChecked = _viewModel.StartWithWindows;
        ShowSuccessNotificationCheckbox.IsChecked = _viewModel.ShowSuccessNotification;
        
        // Set primary API key if available
        if (!string.IsNullOrEmpty(_viewModel.PrimaryApiKey))
        {
            PrimaryApiKeyBox.Password = _viewModel.PrimaryApiKey;
        }
        
        // Set fallback API key if available
        if (!string.IsNullOrEmpty(_viewModel.FallbackApiKey))
        {
            FallbackApiKeyBox.Password = _viewModel.FallbackApiKey;
        }
        
        // Bind lists to ViewModel collections
        PromptsListBox.ItemsSource = _viewModel.Prompts;
        StylesListBox.ItemsSource = _viewModel.Styles;
        DefaultStyleCombo.ItemsSource = _viewModel.StylesRaw;
        
        // Load available models and bind to dropdown
        // This will show fallback models immediately and fetch from API in background
        _viewModel.LoadModelsWithFallback();
        ModelCombo.ItemsSource = _viewModel.AvailableModels;
        SelectModel(_viewModel.SelectedModel);
        
        // Re-select model when collection changes (after API fetch completes)
        _viewModel.AvailableModels.CollectionChanged += (s, e) => 
        {
            Dispatcher.Invoke(() => SelectModel(_viewModel.SelectedModel));
        };
        
        // Select the default style in the combo box
        SelectDefaultStyle(_viewModel.DefaultStyle);
    }

    /// <summary>
    /// Selects the appropriate item in the default style combo box
    /// </summary>
    private void SelectDefaultStyle(string styleId)
    {
        DefaultStyleCombo.SelectedValue = styleId;
        
        // Default to first item if not found
        if (DefaultStyleCombo.SelectedItem == null && DefaultStyleCombo.Items.Count > 0)
        {
            DefaultStyleCombo.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Selects the appropriate item in the model combo box
    /// Requirements: 6.3
    /// </summary>
    private void SelectModel(string modelId)
    {
        ModelCombo.SelectedValue = modelId;
        
        // Default to first item if not found
        if (ModelCombo.SelectedItem == null && ModelCombo.Items.Count > 0)
        {
            ModelCombo.SelectedIndex = 0;
        }
    }

    #region Prompt CRUD Event Handlers

    private void PromptsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.SelectedPrompt = PromptsListBox.SelectedItem as PromptListItem;
        EditPromptButton.IsEnabled = _viewModel.CanEditPrompt;
        DeletePromptButton.IsEnabled = _viewModel.CanDeletePrompt;
    }

    private void AddPromptButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddPromptCommand.Execute(null);
    }

    private void EditPromptButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.EditPromptCommand.Execute(null);
    }

    private void DeletePromptButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeletePromptCommand.Execute(null);
    }

    private void OnAddPromptRequested(object? sender, EventArgs e)
    {
        var dialog = new PromptEditorDialog();
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true && dialog.ResultPrompt != null)
        {
            _viewModel.AddPrompt(dialog.ResultPrompt);
        }
    }

    private void OnEditPromptRequested(object? sender, CustomPrompt prompt)
    {
        var dialog = new PromptEditorDialog(prompt);
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true && dialog.ResultPrompt != null)
        {
            _viewModel.UpdatePrompt(dialog.ResultPrompt);
        }
    }

    private void OnDeletePromptRequested(object? sender, PromptDeleteEventArgs e)
    {
        var message = e.AffectedStyleNames.Count > 0
            ? $"Are you sure you want to delete the prompt '{e.Prompt.Name}'?\n\nThe following styles use this prompt and will be updated to use the default prompt:\n• {string.Join("\n• ", e.AffectedStyleNames)}"
            : $"Are you sure you want to delete the prompt '{e.Prompt.Name}'?";
        
        var result = MessageBox.Show(message, "Confirm Delete", 
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DeletePrompt(e.Prompt.Id);
        }
    }

    #endregion

    #region Style CRUD Event Handlers

    private void StylesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.SelectedStyle = StylesListBox.SelectedItem as StyleListItem;
        EditStyleButton.IsEnabled = _viewModel.CanEditStyle;
        DeleteStyleButton.IsEnabled = _viewModel.CanDeleteStyle;
    }

    private void AddStyleButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddStyleCommand.Execute(null);
    }

    private void EditStyleButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.EditStyleCommand.Execute(null);
    }

    private void DeleteStyleButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteStyleCommand.Execute(null);
    }

    private void OnAddStyleRequested(object? sender, EventArgs e)
    {
        var dialog = new StyleEditorDialog();
        dialog.Owner = this;
        dialog.SetPrompts(_viewModel.PromptsRaw.ToList());
        dialog.ValidateHotkey = (hotkey, excludeStyleId) => _configManager.ValidateHotkey(hotkey, excludeStyleId);
        
        if (dialog.ShowDialog() == true && dialog.ResultStyle != null)
        {
            _viewModel.AddStyle(dialog.ResultStyle);
        }
    }

    private void OnEditStyleRequested(object? sender, CustomStyle style)
    {
        var dialog = new StyleEditorDialog(style);
        dialog.Owner = this;
        dialog.SetPrompts(_viewModel.PromptsRaw.ToList());
        dialog.ValidateHotkey = (hotkey, excludeStyleId) => _configManager.ValidateHotkey(hotkey, excludeStyleId);
        
        if (dialog.ShowDialog() == true && dialog.ResultStyle != null)
        {
            _viewModel.UpdateStyle(dialog.ResultStyle);
        }
    }

    private void OnDeleteStyleRequested(object? sender, StyleDeleteEventArgs e)
    {
        var result = MessageBox.Show(
            $"Are you sure you want to delete the style '{e.Style.Name}'?", 
            "Confirm Delete", 
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DeleteStyle(e.Style.Id);
        }
    }

    private void OnErrorOccurred(object? sender, string errorMessage)
    {
        MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    #endregion

    private void EnabledToggle_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.IsEnabled = EnabledToggle.IsChecked ?? true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Update view model with current UI values
        _viewModel.IsEnabled = EnabledToggle.IsChecked ?? true;
        _viewModel.StartWithWindows = StartWithWindowsCheckbox.IsChecked ?? false;
        _viewModel.ShowSuccessNotification = ShowSuccessNotificationCheckbox.IsChecked ?? true;
        _viewModel.PrimaryApiKey = PrimaryApiKeyBox.Password;
        _viewModel.FallbackApiKey = FallbackApiKeyBox.Password;
        
        // Get selected model from dropdown (Requirements: 6.3)
        if (ModelCombo.SelectedItem is ViewModels.ModelItem selectedModel)
        {
            _viewModel.SelectedModel = selectedModel.Id;
        }
        else if (ModelCombo.SelectedValue is string modelId)
        {
            _viewModel.SelectedModel = modelId;
        }
        
        // Get selected style from dynamic dropdown
        if (DefaultStyleCombo.SelectedItem is CustomStyle selectedStyle)
        {
            _viewModel.DefaultStyle = selectedStyle.Id;
        }
        else if (DefaultStyleCombo.SelectedValue is string styleId)
        {
            _viewModel.DefaultStyle = styleId;
        }
        
        // Validate and save
        var validationError = _viewModel.Validate();
        if (!string.IsNullOrEmpty(validationError))
        {
            MessageBox.Show(validationError, "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (_viewModel.Save())
        {
            // Raise ConfigurationSaved event with updated config
            ConfigurationSaved?.Invoke(this, _viewModel.GetConfiguration());
            
            // Hide window instead of closing (allows reuse)
            Hide();
        }
        else
        {
            MessageBox.Show("Failed to save settings. Please try again.", 
                "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        // Reload settings to discard changes
        _viewModel.Reload();
        _viewModel.LoadPromptsAndStyles();
        LoadSettings();
        
        // Hide window instead of closing
        Hide();
    }

    /// <summary>
    /// Prevents ComboBox from capturing scroll events when dropdown is closed.
    /// Bubbles the scroll event to the parent ScrollViewer for page scrolling.
    /// </summary>
    private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ComboBox comboBox && !comboBox.IsDropDownOpen)
        {
            e.Handled = true;
            var parent = VisualTreeHelper.GetParent(comboBox);
            while (parent != null && parent is not ScrollViewer)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            if (parent is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            }
        }
    }

    /// <summary>
    /// Override to hide instead of close when user clicks X button
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Reload settings to discard any unsaved changes
        _viewModel.Reload();
        _viewModel.LoadPromptsAndStyles();
        LoadSettings();
        
        // Hide instead of close
        e.Cancel = true;
        Hide();
    }
}
