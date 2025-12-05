using System;

namespace RewriteAssistant.Services;

/// <summary>
/// Application state for tray icon display
/// </summary>
public enum AppState
{
    Enabled,
    Disabled,
    Processing,
    Error
}

/// <summary>
/// Event args for settings requested event
/// </summary>
public class SettingsRequestedEventArgs : EventArgs
{
}

/// <summary>
/// Interface for system tray management
/// </summary>
public interface ITrayManager : IDisposable
{
    /// <summary>
    /// Initializes the tray icon and context menu
    /// </summary>
    void Initialize();

    /// <summary>
    /// Shows the settings UI
    /// </summary>
    void ShowUI();

    /// <summary>
    /// Hides the settings UI
    /// </summary>
    void HideUI();

    /// <summary>
    /// Updates the tray icon based on application state
    /// </summary>
    void UpdateIcon(AppState state);

    /// <summary>
    /// Shows a notification balloon
    /// </summary>
    void ShowNotification(string title, string message);

    /// <summary>
    /// Flashes the tray icon briefly to indicate success
    /// </summary>
    void FlashSuccess();

    /// <summary>
    /// Event raised when user requests to open settings
    /// </summary>
    event EventHandler<SettingsRequestedEventArgs>? SettingsRequested;

    /// <summary>
    /// Event raised when user requests to exit the application
    /// </summary>
    event EventHandler? ExitRequested;

    /// <summary>
    /// Event raised when user toggles the enabled state
    /// </summary>
    event EventHandler<bool>? EnabledToggled;
}
