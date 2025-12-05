using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using RewriteAssistant.Models;
using RewriteAssistant.Services;
using RewriteAssistant.Views;

namespace RewriteAssistant;

/// <summary>
/// Main application class with single-instance pattern and service orchestration.
/// Requirements: 3.1, 6.1
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;
    private const string MutexName = "RewriteAssistant_SingleInstance_Mutex";

    // Services
    private IConfigurationManager? _configManager;
    private TrayManager? _trayManager;
    private HotkeyManager? _hotkeyManager;
    private ITextCaptureService? _textCaptureService;
    private ITextReplaceService? _textReplaceService;
    private IIPCClient? _ipcClient;
    private IStartupManager? _startupManager;
    private ICleanupService? _cleanupService;
    private ShutdownService? _shutdownService;
    private Process? _backendProcess;
    private Window? _messageWindow;
    private SettingsWindow? _settingsWindow;
    private AppConfiguration? _config;
    private bool _isShuttingDown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        Logger.Info("=== Rewrite Assistant Starting ===");
        Logger.Info($"Log file: {Logger.GetLogFilePath()}");
        
        // Implement single-instance pattern
        _mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            Logger.Warn("Another instance is already running - exiting");
            MessageBox.Show("Rewrite Assistant is already running.", "Rewrite Assistant", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        try
        {
            await InitializeApplicationAsync();
            Logger.Info("=== Application initialization complete ===");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to initialize application", ex);
            MessageBox.Show($"Failed to initialize application: {ex.Message}", "Rewrite Assistant Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    /// <summary>
    /// Initializes all application services and starts the backend
    /// </summary>
    private async Task InitializeApplicationAsync()
    {
        Logger.Info("--- Initializing application services ---");
        
        // Initialize configuration manager and load config
        Logger.Debug("Loading configuration...");
        _configManager = new ConfigurationManager();
        _config = _configManager.Load();
        Logger.Info($"Configuration loaded: IsEnabled={_config.IsEnabled}, Hotkeys={_config.Hotkeys?.Count ?? 0}");

        // Initialize cleanup and shutdown services
        _cleanupService = new CleanupService();
        _shutdownService = new ShutdownService();

        // Register configuration for shutdown save
        _shutdownService.RegisterConfiguration(_configManager, _config);

        // Perform startup cleanup (Requirement 7.1)
        _cleanupService.PerformStartupCleanup();

        // Initialize services
        Logger.Debug("Initializing text services...");
        _textCaptureService = new TextCaptureService();
        _textReplaceService = new TextReplaceService();
        _ipcClient = new IPCClient();
        _startupManager = new StartupManager();

        // Sync startup registration with config (Requirement 3.5)
        _startupManager.SetStartupEnabled(_config.StartWithWindows);

        // Create a hidden window for hotkey message processing
        Logger.Info("--- Creating message window ---");
        _messageWindow = CreateMessageWindow();

        // Initialize hotkey manager with the message window
        Logger.Info("--- Initializing HotkeyManager ---");
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.Initialize(_messageWindow);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        Logger.Info($"HotkeyManager initialized: IsInitialized={_hotkeyManager.IsInitialized}");
        _shutdownService.RegisterService(_hotkeyManager, "HotkeyManager");

        // Subscribe to hotkey change events for hot reload (Requirement 1.3, 4.1)
        _configManager.HotkeyChanged += OnHotkeyChanged;

        // Initialize tray manager
        Logger.Debug("Initializing TrayManager...");
        _trayManager = new TrayManager();
        _trayManager.Initialize();
        _trayManager.SettingsRequested += OnSettingsRequested;
        _trayManager.ExitRequested += OnExitRequested;
        _trayManager.EnabledToggled += OnEnabledToggled;
        _shutdownService.RegisterService(_trayManager, "TrayManager");

        // Set initial tray state based on config
        _trayManager.SetEnabled(_config.IsEnabled);

        // Start Node.js backend process
        Logger.Info("--- Starting backend process ---");
        await StartBackendProcessAsync();

        // Register backend process for shutdown
        if (_backendProcess != null)
        {
            _shutdownService.RegisterBackendProcess(_backendProcess);
        }

        // Connect IPC client to backend
        Logger.Info("--- Connecting to backend ---");
        await ConnectToBackendAsync();

        // Register IPC client for shutdown
        if (_ipcClient is IDisposable disposableIpc)
        {
            _shutdownService.RegisterService(disposableIpc, "IPCClient");
        }

        // Send API keys to backend
        Logger.Info("--- Sending API keys to backend ---");
        await SendApiKeysToBackendAsync();

        // Sync prompts to backend (Requirement 4.2, 4.3)
        Logger.Info("--- Syncing prompts to backend ---");
        await SyncPromptsToBackendAsync();

        // Register hotkeys from configuration
        Logger.Info("--- Registering hotkeys ---");
        RegisterHotkeysFromConfig();

        // Log diagnostics
        _hotkeyManager.LogDiagnostics();

        Logger.Info("=== Rewrite Assistant initialized successfully ===");
        Logger.Info("Hotkeys registered - check Settings for configured shortcuts");
    }

    /// <summary>
    /// Creates a hidden window for processing Windows messages (hotkeys)
    /// IMPORTANT: The window must remain "shown" (not hidden) to receive WM_HOTKEY messages.
    /// We make it invisible by positioning it off-screen and making it tiny.
    /// </summary>
    private Window CreateMessageWindow()
    {
        Logger.Info("Creating message window for hotkey processing");
        
        var window = new Window
        {
            Title = "RewriteAssistant_MessageWindow",
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Opacity = 0,  // Completely transparent
            Left = -32000,  // Position way off-screen (standard Windows approach)
            Top = -32000,
            Topmost = false
        };
        
        // Subscribe to SourceInitialized to ensure handle is ready
        window.SourceInitialized += (s, e) =>
        {
            Logger.Info("Message window SourceInitialized event fired");
            var hwndHelper = new WindowInteropHelper(window);
            Logger.Info($"SourceInitialized - Handle: 0x{hwndHelper.Handle:X}");
        };
        
        // Must show the window to create the handle and keep it shown to receive messages
        // DO NOT call Hide() - hidden windows don't receive WM_HOTKEY messages!
        window.Show();
        
        // Ensure the window handle is created
        var helper = new WindowInteropHelper(window);
        var handle = helper.EnsureHandle();
        
        Logger.Info($"Message window created and shown - Handle: 0x{handle:X}, IsVisible: {window.IsVisible}");
        
        // Verify the window is receiving messages by checking the dispatcher
        Logger.Info($"Window Dispatcher: {window.Dispatcher != null}, CheckAccess: {window.Dispatcher?.CheckAccess()}");
        
        return window;
    }

    /// <summary>
    /// Starts the Node.js backend process
    /// Supports both installed mode (backend.exe) and development mode (Node.js)
    /// Requirements: 3.1, 3.2
    /// </summary>
    private async Task StartBackendProcessAsync()
    {
        try
        {
            var backendExePath = GetBackendPath();
            Logger.Info($"Backend path: {backendExePath}");
            
            // Check if this is an installed backend.exe
            if (backendExePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(backendExePath))
            {
                Logger.Info("Starting backend in installed mode (backend.exe)");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = backendExePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                Logger.Debug($"Starting process: {startInfo.FileName}");

                _backendProcess = new Process { StartInfo = startInfo };
                _backendProcess.OutputDataReceived += (s, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Logger.Debug($"Backend stdout: {e.Data}");
                };
                _backendProcess.ErrorDataReceived += (s, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Logger.Warn($"Backend stderr: {e.Data}");
                };
                
                _backendProcess.Start();
                _backendProcess.BeginOutputReadLine();
                _backendProcess.BeginErrorReadLine();

                // Give the backend time to start
                await Task.Delay(1000);

                Logger.Info($"Backend process started - PID: {_backendProcess.Id}");
                return;
            }

            // Development mode: backend is a Node.js directory
            var backendPath = backendExePath;
            if (!Directory.Exists(backendPath))
            {
                Logger.Warn($"Backend directory not found: {backendPath}");
                return;
            }

            Logger.Info("Starting backend in development mode (Node.js)");

            var nodeExe = "node";
            var scriptPath = Path.Combine(backendPath, "dist", "index.js");

            // Check if compiled JS exists, if not try ts-node
            if (!File.Exists(scriptPath))
            {
                Logger.Warn($"Compiled backend script not found: {scriptPath}");
                // Try to run with ts-node for development
                nodeExe = "npx";
                scriptPath = $"ts-node {Path.Combine(backendPath, "src", "index.ts")}";
                Logger.Info($"Using ts-node for development: {scriptPath}");
            }
            else
            {
                Logger.Info($"Using compiled backend: {scriptPath}");
            }

            var startInfo2 = new ProcessStartInfo
            {
                FileName = nodeExe,
                Arguments = File.Exists(Path.Combine(backendPath, "dist", "index.js")) 
                    ? Path.Combine(backendPath, "dist", "index.js")
                    : $"ts-node {Path.Combine(backendPath, "src", "index.ts")}",
                WorkingDirectory = backendPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Logger.Debug($"Starting process: {startInfo2.FileName} {startInfo2.Arguments}");

            _backendProcess = new Process { StartInfo = startInfo2 };
            _backendProcess.OutputDataReceived += (s, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Logger.Debug($"Backend stdout: {e.Data}");
            };
            _backendProcess.ErrorDataReceived += (s, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Logger.Warn($"Backend stderr: {e.Data}");
            };
            
            _backendProcess.Start();
            _backendProcess.BeginOutputReadLine();
            _backendProcess.BeginErrorReadLine();

            // Give the backend time to start
            await Task.Delay(1000);

            Logger.Info($"Backend process started - PID: {_backendProcess.Id}");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to start backend", ex);
        }
    }

    /// <summary>
    /// Gets the path to the backend executable or directory
    /// In installed mode, returns path to backend.exe in the same directory as the main executable
    /// In development mode, returns path to the backend directory
    /// Requirements: 3.1, 3.2
    /// </summary>
    private static string GetBackendPath()
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        
        // Production/Installed mode: Check for backend.exe in same directory as main executable
        var backendExe = Path.Combine(exeDir, "backend.exe");
        if (File.Exists(backendExe))
        {
            Logger.Info($"Found backend.exe in installed mode: {backendExe}");
            return backendExe;
        }

        // Development mode: Try relative path from executable
        var backendPath = Path.Combine(exeDir, "..", "..", "..", "..", "backend");
        
        if (Directory.Exists(backendPath))
        {
            Logger.Info($"Found backend directory in development mode: {Path.GetFullPath(backendPath)}");
            return Path.GetFullPath(backendPath);
        }

        // Development mode: Try alternative development path
        backendPath = Path.Combine(exeDir, "..", "..", "..", "..", "..", "src", "backend");
        if (Directory.Exists(backendPath))
        {
            Logger.Info($"Found backend directory in alternative development path: {Path.GetFullPath(backendPath)}");
            return Path.GetFullPath(backendPath);
        }

        // Fallback to current directory structure
        Logger.Warn("Backend not found in standard locations, using fallback path");
        return Path.GetFullPath(Path.Combine(exeDir, "backend"));
    }

    /// <summary>
    /// Connects the IPC client to the backend
    /// </summary>
    private async Task ConnectToBackendAsync()
    {
        const int maxRetries = 5;
        const int retryDelayMs = 500;

        Logger.Info($"Attempting to connect to backend (max {maxRetries} retries)...");

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                Logger.Debug($"Connection attempt {i + 1}/{maxRetries}...");
                var connected = await _ipcClient!.ConnectAsync();
                if (connected)
                {
                    Logger.Info("✓ Connected to backend successfully");
                    return;
                }
                Logger.Warn($"Connection attempt {i + 1} returned false");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Connection attempt {i + 1} failed: {ex.Message}");
            }

            await Task.Delay(retryDelayMs);
        }

        Logger.Error("Failed to connect to backend after all retries");
    }

    /// <summary>
    /// Sends API keys from configuration to the backend
    /// </summary>
    private async Task SendApiKeysToBackendAsync()
    {
        if (_ipcClient == null || _ipcClient.State != ConnectionState.Connected)
        {
            Logger.Warn("Cannot send API keys - not connected to backend");
            return;
        }

        if (_config == null || _configManager == null)
        {
            Logger.Warn("Cannot send API keys - configuration not loaded");
            return;
        }

        try
        {
            var primaryKey = _configManager.GetPrimaryApiKey(_config);
            var fallbackKey = _configManager.GetFallbackApiKey(_config);

            if (string.IsNullOrEmpty(primaryKey) && string.IsNullOrEmpty(fallbackKey))
            {
                Logger.Warn("No API keys configured - please set your Cerebras API key in Settings");
                _trayManager?.ShowNotification("Rewrite Assistant", "No API key configured. Please open Settings and enter your Cerebras API key.");
                return;
            }

            var configUpdate = new ConfigUpdate
            {
                PrimaryApiKey = primaryKey,
                FallbackApiKey = fallbackKey
            };

            var response = await _ipcClient.SendConfigUpdateAsync(configUpdate);
            
            if (response.Success)
            {
                Logger.Info($"✓ API keys sent to backend (Primary: {!string.IsNullOrEmpty(primaryKey)}, Fallback: {!string.IsNullOrEmpty(fallbackKey)})");
            }
            else
            {
                Logger.Error($"Failed to send API keys to backend: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error sending API keys to backend", ex);
        }
    }

    /// <summary>
    /// Registers hotkeys from the configuration (using new Styles model)
    /// </summary>
    private void RegisterHotkeysFromConfig()
    {
        Logger.Info("=== Registering hotkeys from configuration ===");
        
        if (_config?.Styles == null || _config.Styles.Count == 0)
        {
            Logger.Warn("No styles in configuration");
            return;
        }
        
        if (_hotkeyManager == null)
        {
            Logger.Error("HotkeyManager is null - cannot register hotkeys");
            return;
        }

        if (!_hotkeyManager.IsInitialized)
        {
            Logger.Error("HotkeyManager is not initialized - cannot register hotkeys");
            return;
        }

        var stylesWithHotkeys = _config.Styles.Where(s => s.Hotkey != null).ToList();
        Logger.Info($"Found {stylesWithHotkeys.Count} styles with hotkeys to register");

        var successCount = 0;
        var failCount = 0;

        foreach (var style in stylesWithHotkeys)
        {
            var hotkeyConfig = style.Hotkey!;
            Logger.Debug($"Registering: {hotkeyConfig.Id} - Modifiers=[{string.Join(",", hotkeyConfig.Modifiers)}] Key={hotkeyConfig.Key} StyleId={style.Id}");
            
            try
            {
                var success = _hotkeyManager.RegisterHotkey(hotkeyConfig, style.Id);
                if (success)
                {
                    successCount++;
                    Logger.Info($"✓ Registered: {hotkeyConfig.Id} for style {style.Id}");
                }
                else
                {
                    failCount++;
                    Logger.Error($"✗ Failed to register: {hotkeyConfig.Id}");
                }
            }
            catch (Exception ex)
            {
                failCount++;
                Logger.Error($"✗ Exception registering {hotkeyConfig.Id}", ex);
            }
        }
        
        Logger.Info($"=== Hotkey registration complete: {successCount} succeeded, {failCount} failed ===");
    }

    /// <summary>
    /// Handles hotkey press events - orchestrates the rewrite workflow
    /// Requirements: 1.1, 1.2, 1.3, 7.3, 7.4
    /// </summary>
    private async void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        Logger.Info($">>> OnHotkeyPressed: {e.HotkeyId} -> StyleId: {e.StyleId}");
        
        // Check if app is enabled (Requirement 3.4)
        if (_config == null || !_config.IsEnabled)
        {
            Logger.Warn("Hotkey ignored - app is disabled");
            return;
        }

        // Reentrant guard - prevent recursive triggers (Requirement 1.5, 7.5)
        if (_hotkeyManager == null || _hotkeyManager.IsProcessing)
        {
            Logger.Warn("Hotkey ignored - already processing");
            return;
        }

        try
        {
            _hotkeyManager.SetProcessing(true);
            _trayManager?.UpdateIcon(AppState.Processing);

            Logger.Info("Starting rewrite workflow...");
            await ExecuteRewriteWorkflowAsync(e.StyleId);
            Logger.Info("Rewrite workflow completed");
        }
        catch (Exception ex)
        {
            Logger.Error("Rewrite workflow error", ex);
            // Preserve original text on failure (Requirement 7.3, 7.4)
        }
        finally
        {
            _hotkeyManager?.SetProcessing(false);
            _trayManager?.UpdateIcon(_config?.IsEnabled == true ? AppState.Enabled : AppState.Disabled);
        }
    }

    /// <summary>
    /// Executes the complete rewrite workflow
    /// </summary>
    private async Task ExecuteRewriteWorkflowAsync(string styleId)
    {
        Logger.Debug($"ExecuteRewriteWorkflowAsync: StyleId={styleId}");
        
        // Get the style and prompt from configuration
        var style = _config?.Styles?.FirstOrDefault(s => s.Id == styleId);
        if (style == null)
        {
            Logger.Error($"Style not found: {styleId}");
            return;
        }

        var prompt = _config?.Prompts?.FirstOrDefault(p => p.Id == style.PromptId);
        if (prompt == null)
        {
            Logger.Error($"Prompt not found for style {styleId}: {style.PromptId}");
            return;
        }

        // Step 1: Capture text from focused field (Requirement 1.1, 1.2)
        Logger.Debug("Step 1: Capturing text from focused field...");
        var captureResult = await _textCaptureService!.CaptureTextAsync();

        Logger.Debug($"Capture result: Success={captureResult.Success}, IsEditable={captureResult.IsEditableField}, HasSelection={captureResult.HasSelection}, TextLength={captureResult.Text?.Length ?? 0}");

        // Check if we're in an editable field (Requirement 1.3, 5.1, 5.2)
        if (!captureResult.Success || !captureResult.IsEditableField)
        {
            Logger.Info("Not in an editable field - ignoring hotkey");
            return;
        }

        // Check if there's text to rewrite
        if (string.IsNullOrWhiteSpace(captureResult.Text))
        {
            Logger.Info("No text to rewrite - field is empty");
            return;
        }

        Logger.Info($"Captured text ({captureResult.Text.Length} chars) from {captureResult.Context.ApplicationName}");

        // Step 2: Check IPC connection
        Logger.Debug("Step 2: Checking IPC connection...");
        if (_ipcClient == null || _ipcClient.State != ConnectionState.Connected)
        {
            Logger.Warn($"Not connected to backend (state: {_ipcClient?.State}), attempting to connect...");
            await ConnectToBackendAsync();
            
            if (_ipcClient?.State != ConnectionState.Connected)
            {
                Logger.Error("Unable to connect to backend service");
                _trayManager?.ShowNotification("Rewrite Assistant", "Unable to connect to backend service");
                return;
            }
        }

        // Step 3: Send rewrite request via IPC (Requirement 5.4)
        Logger.Debug("Step 3: Sending rewrite request via IPC...");
        var request = new RewriteRequest
        {
            Text = captureResult.Text,
            PromptId = style.PromptId,
            PromptText = prompt.PromptText,
            RequestId = Guid.NewGuid().ToString()
        };

        Logger.Debug($"Request: Id={request.RequestId}, StyleId={styleId}, PromptId={style.PromptId}");

        RewriteResponse response;
        try
        {
            response = await _ipcClient.SendRewriteRequestAsync(request);
            Logger.Debug($"Response: Success={response.Success}, UsedFallback={response.UsedFallbackKey}");
        }
        catch (Exception ex)
        {
            Logger.Error("IPC request failed", ex);
            // Preserve original text on network error (Requirement 7.3, 8.3)
            return;
        }

        // Step 4: Handle response
        if (!response.Success)
        {
            Logger.Error($"Rewrite failed: {response.Error}");
            
            // Show notification if both API keys failed (Requirement 4.3, 8.2)
            if (response.Error?.Contains("API key") == true || response.Error?.Contains("quota") == true)
            {
                _trayManager?.ShowNotification("Rewrite Assistant", "API key error - please check your settings");
            }
            
            // Preserve original text on failure (Requirement 7.4)
            return;
        }

        // Step 5: Replace text with result (Requirement 1.1, 1.2, 1.4)
        Logger.Debug("Step 5: Replacing text with rewritten result...");
        if (!string.IsNullOrEmpty(response.RewrittenText))
        {
            Logger.Debug($"Rewritten text length: {response.RewrittenText.Length}");
            
            var replaceSuccess = await _textReplaceService!.ReplaceTextAsync(
                response.RewrittenText, 
                captureResult.Context);

            if (!replaceSuccess)
            {
                Logger.Error("Failed to replace text in the field");
            }
            else
            {
                Logger.Info($"✓ Text rewritten successfully (used fallback: {response.UsedFallbackKey})");
                
                // Show success feedback
                _trayManager?.FlashSuccess();
                if (_config?.ShowSuccessNotification == true)
                {
                    _trayManager?.ShowNotification("Rewrite Assistant", "Text rewritten successfully");
                }
            }
        }
    }

    /// <summary>
    /// Handles settings requested event from tray
    /// </summary>
    private void OnSettingsRequested(object? sender, SettingsRequestedEventArgs e)
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_configManager!, _config!);
            _settingsWindow.ConfigurationSaved += OnConfigurationSaved;
            _settingsWindow.Closed += (s, args) => _settingsWindow = null;
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>
    /// Handles configuration saved event from settings window
    /// Requirements: 4.2, 4.3
    /// </summary>
    private async void OnConfigurationSaved(object? sender, AppConfiguration newConfig)
    {
        _config = newConfig;
        
        // Update shutdown service with new config reference
        _shutdownService?.UpdateConfiguration(_config);
        
        // Update tray state
        _trayManager?.SetEnabled(_config.IsEnabled);

        // Re-register hotkeys
        _hotkeyManager?.UnregisterAll();
        RegisterHotkeysFromConfig();

        // Update startup registration
        UpdateStartupRegistration();

        // Send updated API keys to backend
        await SendApiKeysToBackendAsync();
        
        // Sync prompts to backend (Requirement 4.2)
        await SyncPromptsToBackendAsync();
    }

    /// <summary>
    /// Syncs prompts to the backend for use in rewrite operations
    /// Requirements: 4.2, 4.3
    /// </summary>
    private async Task SyncPromptsToBackendAsync()
    {
        if (_ipcClient == null || _ipcClient.State != ConnectionState.Connected)
        {
            Logger.Warn("Cannot sync prompts - not connected to backend");
            return;
        }

        if (_config?.Prompts == null || _config.Prompts.Count == 0)
        {
            Logger.Warn("No prompts to sync");
            return;
        }

        try
        {
            Logger.Info($"Syncing {_config.Prompts.Count} prompts to backend...");
            var response = await _ipcClient.SendPromptSyncAsync(_config.Prompts);
            
            if (response.Success)
            {
                Logger.Info($"✓ Prompts synced to backend ({response.PromptCount} prompts)");
            }
            else
            {
                Logger.Warn($"Prompt sync returned failure: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            // Handle sync failures gracefully - don't block the user
            Logger.Error("Failed to sync prompts to backend", ex);
        }
    }

    /// <summary>
    /// Handles hotkey change events from ConfigurationManager for hot reload
    /// Requirements: 1.3, 4.1
    /// </summary>
    private void OnHotkeyChanged(object? sender, HotkeyChangedEventArgs e)
    {
        Logger.Info($"OnHotkeyChanged: {e.ChangeType} for style {e.StyleId}");

        if (_hotkeyManager == null || !_hotkeyManager.IsInitialized)
        {
            Logger.Warn("HotkeyManager not initialized - cannot process hotkey change");
            return;
        }

        try
        {
            switch (e.ChangeType)
            {
                case ChangeType.Added:
                    // Register the new hotkey
                    if (e.NewHotkey != null)
                    {
                        var success = _hotkeyManager.RegisterHotkey(e.NewHotkey, e.StyleId);
                        Logger.Info($"Hotkey added for style {e.StyleId}: {(success ? "success" : "failed")}");
                    }
                    break;

                case ChangeType.Updated:
                    // Unregister old hotkey and register new one
                    if (e.OldHotkey != null)
                    {
                        _hotkeyManager.UnregisterHotkey(e.OldHotkey.Id);
                        Logger.Debug($"Unregistered old hotkey: {e.OldHotkey.Id}");
                    }
                    if (e.NewHotkey != null)
                    {
                        var success = _hotkeyManager.RegisterHotkey(e.NewHotkey, e.StyleId);
                        Logger.Info($"Hotkey updated for style {e.StyleId}: {(success ? "success" : "failed")}");
                    }
                    break;

                case ChangeType.Deleted:
                    // Unregister the hotkey
                    if (e.OldHotkey != null)
                    {
                        _hotkeyManager.UnregisterHotkey(e.OldHotkey.Id);
                        Logger.Info($"Hotkey deleted for style {e.StyleId}");
                    }
                    break;
            }

            // Reload config to keep in sync
            _config = _configManager?.Load();
        }
        catch (Exception ex)
        {
            Logger.Error($"Error handling hotkey change for style {e.StyleId}", ex);
        }
    }

    /// <summary>
    /// Handles enabled toggle from tray menu
    /// </summary>
    private void OnEnabledToggled(object? sender, bool enabled)
    {
        if (_config != null)
        {
            _config.IsEnabled = enabled;
            _configManager?.Save(_config);
        }
    }

    /// <summary>
    /// Handles exit requested event from tray
    /// </summary>
    private async void OnExitRequested(object? sender, EventArgs e)
    {
        await PerformShutdownAsync();
    }

    /// <summary>
    /// Performs graceful shutdown asynchronously to avoid deadlocks
    /// </summary>
    private async Task PerformShutdownAsync()
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        Logger.Info("=== Shutdown requested ===");

        try
        {
            // Dispose tray icon first to remove it from system tray immediately
            if (_trayManager != null)
            {
                Logger.Debug("Disposing tray manager...");
                _trayManager.SettingsRequested -= OnSettingsRequested;
                _trayManager.ExitRequested -= OnExitRequested;
                _trayManager.EnabledToggled -= OnEnabledToggled;
                _trayManager.Dispose();
                _trayManager = null;
            }

            // Close settings window if open
            if (_settingsWindow != null)
            {
                Logger.Debug("Closing settings window...");
                _settingsWindow.Close();
                _settingsWindow = null;
            }

            // Close message window
            if (_messageWindow != null)
            {
                Logger.Debug("Closing message window...");
                _messageWindow.Close();
                _messageWindow = null;
            }

            // Perform async shutdown of remaining services
            if (_shutdownService != null)
            {
                Logger.Debug("Running shutdown service...");
                await _shutdownService.ShutdownAsync();
            }

            Logger.Info("=== Shutdown complete ===");
        }
        catch (Exception ex)
        {
            Logger.Error("Error during shutdown", ex);
        }
        finally
        {
            // Release mutex before exiting
            try
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
                _mutex = null;
            }
            catch (Exception ex)
            {
                Logger.Error("Error releasing mutex", ex);
            }

            // Force application exit
            Environment.Exit(0);
        }
    }

    /// <summary>
    /// Updates Windows startup registration based on configuration (Requirement 3.5)
    /// </summary>
    private void UpdateStartupRegistration()
    {
        if (_config == null || _startupManager == null) return;

        _startupManager.SetStartupEnabled(_config.StartWithWindows);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // If shutdown was already handled by PerformShutdownAsync, just call base
        if (_isShuttingDown)
        {
            base.OnExit(e);
            return;
        }

        // Handle case where OnExit is called directly (e.g., Windows shutdown)
        _isShuttingDown = true;
        Logger.Info("=== OnExit called directly ===");

        try
        {
            // Dispose tray icon first
            if (_trayManager != null)
            {
                _trayManager.SettingsRequested -= OnSettingsRequested;
                _trayManager.ExitRequested -= OnExitRequested;
                _trayManager.EnabledToggled -= OnEnabledToggled;
                _trayManager.Dispose();
                _trayManager = null;
            }

            // Close windows
            _settingsWindow?.Close();
            _messageWindow?.Close();

            // Synchronous cleanup for critical resources only
            // Save configuration
            if (_configManager != null && _config != null)
            {
                _configManager.Save(_config);
            }

            // Kill backend process
            if (_backendProcess != null && !_backendProcess.HasExited)
            {
                try
                {
                    _backendProcess.Kill(entireProcessTree: true);
                    _backendProcess.WaitForExit(2000);
                }
                catch { }
                _backendProcess.Dispose();
            }

            // Dispose hotkey manager
            _hotkeyManager?.Dispose();

            // Dispose IPC client
            (_ipcClient as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during OnExit: {ex.Message}");
        }
        finally
        {
            try
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
            catch { }
        }

        base.OnExit(e);
    }
}
