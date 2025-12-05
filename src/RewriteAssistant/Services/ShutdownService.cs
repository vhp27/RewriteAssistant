using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RewriteAssistant.Services;

/// <summary>
/// Interface for graceful shutdown operations
/// </summary>
public interface IShutdownService
{
    /// <summary>
    /// Performs graceful shutdown of all application services
    /// </summary>
    Task ShutdownAsync();

    /// <summary>
    /// Registers a service for shutdown
    /// </summary>
    void RegisterService(IDisposable service, string name);

    /// <summary>
    /// Registers the backend process for termination
    /// </summary>
    void RegisterBackendProcess(Process process);

    /// <summary>
    /// Registers the configuration manager and current config for saving
    /// </summary>
    void RegisterConfiguration(IConfigurationManager configManager, Models.AppConfiguration config);
}

/// <summary>
/// Handles graceful shutdown of all application services.
/// Requirements: 7.1, 7.2
/// </summary>
public class ShutdownService : IShutdownService
{
    private readonly List<(IDisposable Service, string Name)> _services = new();
    private readonly object _lock = new();
    private Process? _backendProcess;
    private IConfigurationManager? _configManager;
    private Models.AppConfiguration? _config;
    private bool _isShuttingDown;

    private const int BackendTerminationTimeoutMs = 3000;
    private const int ServiceDisposeTimeoutMs = 1000;

    /// <inheritdoc />
    public void RegisterService(IDisposable service, string name)
    {
        if (service == null) throw new ArgumentNullException(nameof(service));
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name cannot be empty", nameof(name));

        lock (_lock)
        {
            _services.Add((service, name));
            Debug.WriteLine($"Registered service for shutdown: {name}");
        }
    }

    /// <inheritdoc />
    public void RegisterBackendProcess(Process process)
    {
        lock (_lock)
        {
            _backendProcess = process;
            Debug.WriteLine("Registered backend process for shutdown");
        }
    }

    /// <inheritdoc />
    public void RegisterConfiguration(IConfigurationManager configManager, Models.AppConfiguration config)
    {
        lock (_lock)
        {
            _configManager = configManager;
            _config = config;
            Debug.WriteLine("Registered configuration for shutdown save");
        }
    }

    /// <summary>
    /// Updates the configuration reference (for when config changes during runtime)
    /// </summary>
    public void UpdateConfiguration(Models.AppConfiguration config)
    {
        lock (_lock)
        {
            _config = config;
        }
    }

    /// <inheritdoc />
    public async Task ShutdownAsync()
    {
        lock (_lock)
        {
            if (_isShuttingDown)
            {
                Debug.WriteLine("Shutdown already in progress");
                return;
            }
            _isShuttingDown = true;
        }

        Debug.WriteLine("Starting graceful shutdown...");

        try
        {
            // Step 1: Save configuration first (most important)
            SaveConfiguration();

            // Step 2: Dispose services in reverse order (LIFO)
            await DisposeServicesAsync();

            // Step 3: Terminate backend process
            await TerminateBackendProcessAsync();

            Debug.WriteLine("Graceful shutdown completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during shutdown: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the current configuration
    /// </summary>
    private void SaveConfiguration()
    {
        try
        {
            if (_configManager != null && _config != null)
            {
                _configManager.Save(_config);
                Debug.WriteLine("Configuration saved during shutdown");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save configuration during shutdown: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes all registered services in reverse order
    /// </summary>
    private async Task DisposeServicesAsync()
    {
        List<(IDisposable Service, string Name)> servicesToDispose;
        
        lock (_lock)
        {
            // Create a reversed copy to dispose in LIFO order
            servicesToDispose = new List<(IDisposable, string)>(_services);
            servicesToDispose.Reverse();
        }

        foreach (var (service, name) in servicesToDispose)
        {
            try
            {
                Debug.WriteLine($"Disposing service: {name}");

                // Handle async disposable services
                if (service is IAsyncDisposable asyncDisposable)
                {
                    using var cts = new CancellationTokenSource(ServiceDisposeTimeoutMs);
                    try
                    {
                        await asyncDisposable.DisposeAsync().AsTask().WaitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine($"Service {name} dispose timed out");
                    }
                }
                else
                {
                    // Wrap synchronous dispose in a task with timeout
                    var disposeTask = Task.Run(() => service.Dispose());
                    using var cts = new CancellationTokenSource(ServiceDisposeTimeoutMs);
                    
                    try
                    {
                        await disposeTask.WaitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine($"Service {name} dispose timed out");
                    }
                }

                Debug.WriteLine($"Service {name} disposed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disposing service {name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Terminates the Node.js backend process
    /// </summary>
    private async Task TerminateBackendProcessAsync()
    {
        if (_backendProcess == null)
        {
            return;
        }

        try
        {
            if (!_backendProcess.HasExited)
            {
                Debug.WriteLine("Terminating backend process...");

                // Try graceful termination first
                _backendProcess.CloseMainWindow();

                // Wait for graceful exit
                var exitedGracefully = await Task.Run(() => 
                    _backendProcess.WaitForExit(BackendTerminationTimeoutMs / 2));

                if (!exitedGracefully && !_backendProcess.HasExited)
                {
                    // Force kill if graceful termination failed
                    Debug.WriteLine("Backend process did not exit gracefully, forcing termination...");
                    _backendProcess.Kill(entireProcessTree: true);
                    await Task.Run(() => _backendProcess.WaitForExit(BackendTerminationTimeoutMs / 2));
                }

                Debug.WriteLine("Backend process terminated");
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited
            Debug.WriteLine("Backend process already exited");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error terminating backend process: {ex.Message}");
        }
        finally
        {
            try
            {
                _backendProcess.Dispose();
            }
            catch
            {
                // Ignore dispose errors
            }
        }
    }
}
