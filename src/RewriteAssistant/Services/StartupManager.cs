using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace RewriteAssistant.Services;

/// <summary>
/// Interface for managing Windows startup registration
/// </summary>
public interface IStartupManager
{
    /// <summary>
    /// Registers the application to start with Windows
    /// </summary>
    bool RegisterStartup();

    /// <summary>
    /// Unregisters the application from Windows startup
    /// </summary>
    bool UnregisterStartup();

    /// <summary>
    /// Checks if the application is registered to start with Windows
    /// </summary>
    bool IsRegistered { get; }

    /// <summary>
    /// Sets the startup registration based on the enabled flag
    /// </summary>
    void SetStartupEnabled(bool enabled);
}

/// <summary>
/// Manages Windows startup registration for the application.
/// Implements Requirement 3.5
/// </summary>
public class StartupManager : IStartupManager
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "RewriteAssistant";

    /// <inheritdoc />
    public bool IsRegistered
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public bool RegisterStartup()
    {
        try
        {
            var exePath = GetExecutablePath();
            if (string.IsNullOrEmpty(exePath))
            {
                Debug.WriteLine("Failed to get executable path for startup registration");
                return false;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null)
            {
                Debug.WriteLine("Failed to open registry key for startup registration");
                return false;
            }

            key.SetValue(AppName, $"\"{exePath}\"");
            Debug.WriteLine($"Registered startup: {exePath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register startup: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public bool UnregisterStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null)
            {
                return true; // Key doesn't exist, nothing to unregister
            }

            key.DeleteValue(AppName, false);
            Debug.WriteLine("Unregistered startup");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unregister startup: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public void SetStartupEnabled(bool enabled)
    {
        if (enabled)
        {
            RegisterStartup();
        }
        else
        {
            UnregisterStartup();
        }
    }

    /// <summary>
    /// Gets the path to the current executable
    /// </summary>
    private static string? GetExecutablePath()
    {
        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }
}
