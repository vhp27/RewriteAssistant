using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using RewriteAssistant.Models;

namespace RewriteAssistant.Services;

/// <summary>
/// Event arguments for hotkey press events
/// </summary>
public class HotkeyEventArgs : EventArgs
{
    public string HotkeyId { get; }
    public string StyleId { get; }

    public HotkeyEventArgs(string hotkeyId, string styleId)
    {
        HotkeyId = hotkeyId;
        StyleId = styleId;
    }
}

/// <summary>
/// Interface for hotkey management
/// </summary>
public interface IHotkeyManager : IDisposable
{
    /// <summary>
    /// Registers a global hotkey for a style
    /// </summary>
    bool RegisterHotkey(HotkeyConfig config, string styleId);

    /// <summary>
    /// Unregisters a specific hotkey by ID
    /// </summary>
    bool UnregisterHotkey(string hotkeyId);

    /// <summary>
    /// Unregisters all registered hotkeys
    /// </summary>
    void UnregisterAll();

    /// <summary>
    /// Event raised when a registered hotkey is pressed
    /// </summary>
    event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    /// <summary>
    /// Gets whether a rewrite operation is currently in progress
    /// </summary>
    bool IsProcessing { get; }

    /// <summary>
    /// Sets the processing state (reentrant guard)
    /// </summary>
    void SetProcessing(bool processing);
}

/// <summary>
/// Internal class to track hotkey registration with associated style
/// </summary>
internal class HotkeyRegistration
{
    public HotkeyConfig Config { get; set; } = null!;
    public string StyleId { get; set; } = string.Empty;
}

/// <summary>
/// Manages global hotkey registration and handling using Windows API
/// Requirements: 1.1, 1.5, 2.2
/// </summary>
public class HotkeyManager : IHotkeyManager
{
    private readonly Dictionary<int, HotkeyRegistration> _registeredHotkeys = new();
    private readonly object _lock = new();
    private int _nextHotkeyId = 1;
    private IntPtr _windowHandle = IntPtr.Zero;
    private HwndSource? _hwndSource;
    private bool _isProcessing;
    private bool _disposed;
    private bool _isInitialized;

    /// <inheritdoc />
    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    /// <inheritdoc />
    public bool IsProcessing
    {
        get
        {
            lock (_lock)
            {
                return _isProcessing;
            }
        }
    }

    /// <summary>
    /// Gets whether the hotkey manager has been initialized
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Initializes the hotkey manager with a window handle for message processing
    /// </summary>
    public void Initialize(Window window)
    {
        Logger.Info($"Initializing HotkeyManager with Window: {window?.GetType().Name}");
        
        if (window == null)
        {
            Logger.Error("Window is null - cannot initialize HotkeyManager");
            return;
        }

        try
        {
            var helper = new WindowInteropHelper(window);
            _windowHandle = helper.EnsureHandle();
            
            Logger.Info($"Window handle obtained: 0x{_windowHandle:X} (IsZero: {_windowHandle == IntPtr.Zero})");
            
            if (_windowHandle == IntPtr.Zero)
            {
                Logger.Error("Window handle is zero after EnsureHandle()");
                return;
            }
            
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            
            if (_hwndSource == null)
            {
                Logger.Warn("HwndSource.FromHwnd returned null, creating new HwndSource");
                // Create HwndSource if FromHwnd returns null
                var parameters = new HwndSourceParameters("HotkeyMessageWindow")
                {
                    ParentWindow = _windowHandle,
                    WindowStyle = unchecked((int)0x80000000) // WS_POPUP
                };
                _hwndSource = new HwndSource(parameters);
                Logger.Info($"Created new HwndSource with handle: 0x{_hwndSource.Handle:X}");
            }
            else
            {
                Logger.Info($"Got HwndSource from existing window, handle: 0x{_hwndSource.Handle:X}");
            }
            
            _hwndSource.AddHook(WndProc);
            _isInitialized = true;
            Logger.Info("HotkeyManager initialized successfully - WndProc hook added");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to initialize HotkeyManager", ex);
            _isInitialized = false;
        }
    }

    /// <summary>
    /// Initializes the hotkey manager with a specific window handle
    /// </summary>
    public void Initialize(IntPtr windowHandle)
    {
        Logger.Info($"Initializing HotkeyManager with IntPtr handle: 0x{windowHandle:X}");
        
        _windowHandle = windowHandle;
        
        if (_windowHandle == IntPtr.Zero)
        {
            Logger.Error("Window handle is zero - cannot initialize");
            return;
        }
        
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        
        if (_hwndSource != null)
        {
            _hwndSource.AddHook(WndProc);
            _isInitialized = true;
            Logger.Info("HotkeyManager initialized with IntPtr handle");
        }
        else
        {
            Logger.Error("Failed to get HwndSource from handle");
        }
    }

    /// <inheritdoc />
    public bool RegisterHotkey(HotkeyConfig config, string styleId)
    {
        Logger.Debug($"RegisterHotkey called for: {config?.Id ?? "null"}, styleId: {styleId}");
        
        if (config == null)
        {
            Logger.Error("Config is null");
            throw new ArgumentNullException(nameof(config));
        }

        if (string.IsNullOrEmpty(config.Id))
        {
            Logger.Error("Hotkey ID is empty");
            throw new ArgumentException("Hotkey ID cannot be empty", nameof(config));
        }

        if (string.IsNullOrEmpty(styleId))
        {
            Logger.Error("Style ID is empty");
            throw new ArgumentException("Style ID cannot be empty", nameof(styleId));
        }

        if (config.VirtualKeyCode == 0)
        {
            Logger.Error($"Invalid key: {config.Key} - VirtualKeyCode is 0");
            throw new ArgumentException($"Invalid key: {config.Key}", nameof(config));
        }

        Logger.Info($"Registering hotkey: Id={config.Id}, Key={config.Key}, VK=0x{config.VirtualKeyCode:X}, Modifiers=[{string.Join(",", config.Modifiers)}], ModFlags=0x{config.ModifierFlags:X}, StyleId={styleId}");

        if (!_isInitialized)
        {
            Logger.Error("HotkeyManager not initialized - Initialize() must be called first");
            return false;
        }

        if (_windowHandle == IntPtr.Zero)
        {
            Logger.Error("Window handle is zero - Initialize() must be called first");
            return false;
        }

        lock (_lock)
        {
            // Check if this ID is already registered
            var existingEntry = _registeredHotkeys.FirstOrDefault(kvp => kvp.Value.Config.Id == config.Id);
            if (existingEntry.Value != null)
            {
                Logger.Info($"Hotkey {config.Id} already registered with id {existingEntry.Key}, unregistering first");
                UnregisterHotkeyInternal(existingEntry.Key);
            }

            var hotkeyId = _nextHotkeyId++;
            var modifiers = config.ModifierFlags | NativeMethods.MOD_NOREPEAT;

            Logger.Debug($"Calling RegisterHotKey: hwnd=0x{_windowHandle:X}, id={hotkeyId}, modifiers=0x{modifiers:X}, vk=0x{config.VirtualKeyCode:X}");

            var success = NativeMethods.RegisterHotKey(
                _windowHandle,
                hotkeyId,
                modifiers,
                config.VirtualKeyCode);

            if (success)
            {
                _registeredHotkeys[hotkeyId] = new HotkeyRegistration { Config = config, StyleId = styleId };
                Logger.Info($"SUCCESS: Hotkey '{config.Id}' registered with system id {hotkeyId}. Total registered: {_registeredHotkeys.Count}");
                return true;
            }

            var error = Marshal.GetLastWin32Error();
            var errorMessage = GetWin32ErrorMessage(error);
            Logger.Error($"FAILED: RegisterHotKey for '{config.Id}' - Win32 error {error}: {errorMessage}");
            return false;
        }
    }

    /// <summary>
    /// Gets a human-readable message for common Win32 errors
    /// </summary>
    private static string GetWin32ErrorMessage(int error)
    {
        return error switch
        {
            0 => "Success",
            1409 => "Hotkey already registered by another application",
            87 => "Invalid parameter",
            6 => "Invalid handle",
            _ => $"Unknown error code {error}"
        };
    }

    /// <inheritdoc />
    public bool UnregisterHotkey(string hotkeyId)
    {
        if (string.IsNullOrEmpty(hotkeyId))
            return false;

        lock (_lock)
        {
            var entry = _registeredHotkeys.FirstOrDefault(kvp => kvp.Value.Config.Id == hotkeyId);
            if (entry.Value == null)
                return false;

            return UnregisterHotkeyInternal(entry.Key);
        }
    }

    /// <inheritdoc />
    public void UnregisterAll()
    {
        lock (_lock)
        {
            var hotkeyIds = _registeredHotkeys.Keys.ToList();
            foreach (var id in hotkeyIds)
            {
                UnregisterHotkeyInternal(id);
            }
        }
    }

    /// <inheritdoc />
    public void SetProcessing(bool processing)
    {
        lock (_lock)
        {
            _isProcessing = processing;
        }
    }

    /// <summary>
    /// Internal method to unregister a hotkey by its numeric ID
    /// </summary>
    private bool UnregisterHotkeyInternal(int hotkeyId)
    {
        var success = NativeMethods.UnregisterHotKey(_windowHandle, hotkeyId);
        if (success)
        {
            _registeredHotkeys.Remove(hotkeyId);
        }
        return success;
    }

    /// <summary>
    /// Window procedure hook to handle WM_HOTKEY messages
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Log all messages for debugging (only WM_HOTKEY in production)
        if (msg == NativeMethods.WM_HOTKEY)
        {
            Logger.Info($">>> WM_HOTKEY received! hwnd=0x{hwnd:X}, wParam={wParam.ToInt32()}, lParam=0x{lParam:X}");
            var hotkeyId = wParam.ToInt32();
            HandleHotkeyPress(hotkeyId);
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Handles a hotkey press event
    /// </summary>
    private void HandleHotkeyPress(int hotkeyId)
    {
        Logger.Debug($"HandleHotkeyPress called with id: {hotkeyId}");
        
        HotkeyRegistration? registration;
        bool shouldProcess;

        lock (_lock)
        {
            // Reentrant guard: ignore if already processing
            if (_isProcessing)
            {
                Logger.Warn($"Hotkey {hotkeyId} ignored - already processing a rewrite operation");
                return;
            }

            if (!_registeredHotkeys.TryGetValue(hotkeyId, out registration))
            {
                Logger.Warn($"Hotkey id {hotkeyId} not found in registered hotkeys. Registered ids: [{string.Join(", ", _registeredHotkeys.Keys)}]");
                return;
            }

            shouldProcess = true;
            Logger.Debug($"Hotkey {hotkeyId} found: {registration.Config.Id} -> StyleId: {registration.StyleId}");
        }

        if (shouldProcess && registration != null)
        {
            Logger.Info($">>> HOTKEY TRIGGERED: {registration.Config.Id} -> StyleId: {registration.StyleId}");
            
            var handler = HotkeyPressed;
            if (handler != null)
            {
                Logger.Debug($"Invoking HotkeyPressed event with {handler.GetInvocationList().Length} subscriber(s)");
                handler.Invoke(this, new HotkeyEventArgs(registration.Config.Id, registration.StyleId));
            }
            else
            {
                Logger.Warn("No subscribers to HotkeyPressed event!");
            }
        }
    }

    /// <summary>
    /// Gets all registered hotkey configurations
    /// </summary>
    public IReadOnlyList<HotkeyConfig> GetRegisteredHotkeys()
    {
        lock (_lock)
        {
            return _registeredHotkeys.Values.Select(r => r.Config).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Logs diagnostic information about the hotkey manager state
    /// </summary>
    public void LogDiagnostics()
    {
        Logger.Info("=== HotkeyManager Diagnostics ===");
        Logger.Info($"IsInitialized: {_isInitialized}");
        Logger.Info($"WindowHandle: 0x{_windowHandle:X} (IsZero: {_windowHandle == IntPtr.Zero})");
        Logger.Info($"HwndSource: {(_hwndSource != null ? $"Handle=0x{_hwndSource.Handle:X}" : "null")}");
        Logger.Info($"IsProcessing: {_isProcessing}");
        Logger.Info($"Registered Hotkeys: {_registeredHotkeys.Count}");
        
        lock (_lock)
        {
            foreach (var kvp in _registeredHotkeys)
            {
                var reg = kvp.Value;
                Logger.Info($"  - ID={kvp.Key}: {reg.Config.Id} -> [{string.Join("+", reg.Config.Modifiers)}]+{reg.Config.Key} = StyleId:{reg.StyleId}");
            }
        }
        
        Logger.Info($"HotkeyPressed event subscribers: {HotkeyPressed?.GetInvocationList().Length ?? 0}");
        Logger.Info("=================================");
    }

    /// <summary>
    /// Gets a hotkey configuration by ID
    /// </summary>
    public HotkeyConfig? GetHotkeyById(string hotkeyId)
    {
        lock (_lock)
        {
            return _registeredHotkeys.Values.FirstOrDefault(r => r.Config.Id == hotkeyId)?.Config;
        }
    }

    /// <summary>
    /// Checks if a hotkey with the given ID is registered
    /// </summary>
    public bool IsHotkeyRegistered(string hotkeyId)
    {
        lock (_lock)
        {
            return _registeredHotkeys.Values.Any(r => r.Config.Id == hotkeyId);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            UnregisterAll();
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource?.Dispose();
        }

        _disposed = true;
    }

    ~HotkeyManager()
    {
        Dispose(false);
    }
}
