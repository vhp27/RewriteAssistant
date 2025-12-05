using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using RewriteAssistant.Models;

namespace RewriteAssistant.Services;

/// <summary>
/// Manages the system tray icon and context menu.
/// Implements Requirements 3.1, 4.3
/// </summary>
public class TrayManager : ITrayManager
{
    private TaskbarIcon? _trayIcon;
    private AppState _currentState = AppState.Enabled;
    private bool _isEnabled = true;
    private bool _disposed;
    private Icon? _trayIconResource;
    private DispatcherTimer? _flashTimer;
    private int _flashCount;
    private bool _isFlashing;

    /// <inheritdoc />
    public event EventHandler<SettingsRequestedEventArgs>? SettingsRequested;

    /// <inheritdoc />
    public event EventHandler? ExitRequested;

    /// <inheritdoc />
    public event EventHandler<bool>? EnabledToggled;

    /// <inheritdoc />
    public void Initialize()
    {
        if (_trayIcon != null)
        {
            return;
        }

        _trayIconResource = LoadTrayIcon();

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Rewrite Assistant - Enabled",
            Icon = _trayIconResource,
            ContextMenu = CreateContextMenu()
        };

        _trayIcon.TrayMouseDoubleClick += OnTrayDoubleClick;
    }

    /// <summary>
    /// Loads the tray icon from embedded resources
    /// </summary>
    private static Icon LoadTrayIcon()
    {
        var resourceUri = new Uri("pack://application:,,,/RewriteAssistant;component/Resources/system-tray.ico", UriKind.Absolute);
        var streamInfo = Application.GetResourceStream(resourceUri);
        
        if (streamInfo != null)
        {
            return new Icon(streamInfo.Stream);
        }
        
        // Fallback: try loading from file system (for development)
        var appDir = AppContext.BaseDirectory;
        var iconPath = Path.Combine(appDir, "Resources", "system-tray.ico");
        
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }
        
        throw new FileNotFoundException("Could not load system tray icon from resources or file system.");
    }

    /// <inheritdoc />
    public void ShowUI()
    {
        SettingsRequested?.Invoke(this, new SettingsRequestedEventArgs());
    }

    /// <inheritdoc />
    public void HideUI()
    {
        // Settings window handles its own visibility
    }

    /// <inheritdoc />
    public void UpdateIcon(AppState state)
    {
        if (_trayIcon == null || _trayIconResource == null)
        {
            return;
        }

        _currentState = state;
        _trayIcon.Icon = _trayIconResource;
        _trayIcon.ToolTipText = GetTooltipText(state);
    }


    /// <inheritdoc />
    public void ShowNotification(string title, string message)
    {
        _trayIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
    }

    /// <inheritdoc />
    public void FlashSuccess()
    {
        if (_trayIcon == null || _isFlashing)
        {
            return;
        }

        _isFlashing = true;
        _flashCount = 0;

        // Create timer for flash effect (blink 3 times)
        _flashTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _flashTimer.Tick += OnFlashTick;
        _flashTimer.Start();
    }

    /// <summary>
    /// Handles the flash timer tick to toggle icon visibility
    /// </summary>
    private void OnFlashTick(object? sender, EventArgs e)
    {
        if (_trayIcon == null || _flashTimer == null)
        {
            StopFlashing();
            return;
        }

        _flashCount++;

        // Toggle icon visibility (blink effect)
        if (_flashCount % 2 == 1)
        {
            _trayIcon.Icon = null;
        }
        else
        {
            _trayIcon.Icon = _trayIconResource;
        }

        // Stop after 6 ticks (3 blinks: off-on-off-on-off-on)
        if (_flashCount >= 6)
        {
            StopFlashing();
        }
    }

    /// <summary>
    /// Stops the flashing effect and restores the icon
    /// </summary>
    private void StopFlashing()
    {
        _flashTimer?.Stop();
        _flashTimer = null;
        _isFlashing = false;
        _flashCount = 0;

        // Ensure icon is restored
        if (_trayIcon != null && _trayIconResource != null)
        {
            _trayIcon.Icon = _trayIconResource;
        }
    }

    /// <summary>
    /// Creates the context menu for the tray icon
    /// </summary>
    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();

        // Enable/Disable toggle
        var enableItem = new MenuItem
        {
            Header = _isEnabled ? "Disable" : "Enable",
            Tag = "toggle"
        };
        enableItem.Click += OnToggleClick;
        menu.Items.Add(enableItem);

        menu.Items.Add(new Separator());

        // Settings
        var settingsItem = new MenuItem { Header = "Settings..." };
        settingsItem.Click += OnSettingsClick;
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        // Exit
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += OnExitClick;
        menu.Items.Add(exitItem);

        return menu;
    }

    /// <summary>
    /// Updates the context menu toggle item text
    /// </summary>
    private void UpdateToggleMenuItem()
    {
        if (_trayIcon?.ContextMenu == null)
        {
            return;
        }

        foreach (var item in _trayIcon.ContextMenu.Items)
        {
            if (item is MenuItem menuItem && menuItem.Tag?.ToString() == "toggle")
            {
                menuItem.Header = _isEnabled ? "Disable" : "Enable";
                break;
            }
        }
    }

    /// <summary>
    /// Gets tooltip text for the given state
    /// </summary>
    private static string GetTooltipText(AppState state)
    {
        return state switch
        {
            AppState.Enabled => "Rewrite Assistant - Enabled",
            AppState.Disabled => "Rewrite Assistant - Disabled",
            AppState.Processing => "Rewrite Assistant - Processing...",
            AppState.Error => "Rewrite Assistant - Error",
            _ => "Rewrite Assistant"
        };
    }

    private void OnTrayDoubleClick(object sender, RoutedEventArgs e)
    {
        ShowUI();
    }

    private void OnToggleClick(object sender, RoutedEventArgs e)
    {
        _isEnabled = !_isEnabled;
        UpdateToggleMenuItem();
        UpdateIcon(_isEnabled ? AppState.Enabled : AppState.Disabled);
        EnabledToggled?.Invoke(this, _isEnabled);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        ShowUI();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sets the enabled state (for external updates)
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        UpdateToggleMenuItem();
        UpdateIcon(enabled ? AppState.Enabled : AppState.Disabled);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop any ongoing flash effect
        StopFlashing();

        if (_trayIcon != null)
        {
            try
            {
                _trayIcon.TrayMouseDoubleClick -= OnTrayDoubleClick;
                
                // Explicitly hide and dispose the icon to remove it from system tray
                _trayIcon.Visibility = System.Windows.Visibility.Collapsed;
                _trayIcon.Icon?.Dispose();
                _trayIcon.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
            finally
            {
                _trayIcon = null;
            }
        }

        _trayIconResource?.Dispose();
        _trayIconResource = null;
    }
}
