using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using RewriteAssistant.Models;

namespace RewriteAssistant.Services;

/// <summary>
/// Connection state for the IPC client
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

/// <summary>
/// Event args for connection state changes
/// </summary>
public class ConnectionStateEventArgs : EventArgs
{
    public ConnectionState State { get; }
    public string? ErrorMessage { get; }

    public ConnectionStateEventArgs(ConnectionState state, string? errorMessage = null)
    {
        State = state;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Interface for IPC client
/// </summary>
public interface IIPCClient : IDisposable
{
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task<RewriteResponse> SendRewriteRequestAsync(RewriteRequest request, CancellationToken cancellationToken = default);
    Task<HealthStatus> SendHealthCheckAsync(CancellationToken cancellationToken = default);
    Task<ConfigResponse> SendConfigUpdateAsync(ConfigUpdate config, CancellationToken cancellationToken = default);
    Task<PromptSyncResponse> SendPromptSyncAsync(IEnumerable<CustomPrompt> prompts, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    ConnectionState State { get; }
    event EventHandler<ConnectionStateEventArgs>? ConnectionStateChanged;
}


/// <summary>
/// IPC client for communicating with the Node.js backend via named pipes
/// Requirements: 10.1
/// </summary>
public class IPCClient : IIPCClient
{
    private const string PipeName = "RewriteAssistantIPC";
    private const int ConnectionTimeoutMs = 5000;
    private const int ReadTimeoutMs = 30000;

    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private ConnectionState _state = ConnectionState.Disconnected;

    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                ConnectionStateChanged?.Invoke(this, new ConnectionStateEventArgs(value));
            }
        }
    }

    public event EventHandler<ConnectionStateEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Connects to the Node.js backend via named pipe
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (State == ConnectionState.Connected)
        {
            return true;
        }

        try
        {
            State = ConnectionState.Connecting;

            _pipeClient = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var timeoutCts = new CancellationTokenSource(ConnectionTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await _pipeClient.ConnectAsync(linkedCts.Token);

            // Use UTF8 without BOM to avoid JSON parsing issues
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            _reader = new StreamReader(_pipeClient, utf8NoBom);
            _writer = new StreamWriter(_pipeClient, utf8NoBom) { AutoFlush = true };

            State = ConnectionState.Connected;
            return true;
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            ConnectionStateChanged?.Invoke(this, new ConnectionStateEventArgs(ConnectionState.Error, ex.Message));
            await CleanupAsync();
            return false;
        }
    }


    /// <summary>
    /// Sends a rewrite request to the backend
    /// </summary>
    public async Task<RewriteResponse> SendRewriteRequestAsync(RewriteRequest request, CancellationToken cancellationToken = default)
    {
        var message = new IPCMessage
        {
            Type = "rewrite_request",
            RequestId = request.RequestId,
            Payload = request,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var response = await SendMessageAsync<RewriteResponse>(message, cancellationToken);
        return response ?? new RewriteResponse { Success = false, Error = "Failed to get response from backend" };
    }

    /// <summary>
    /// Sends a health check request to the backend
    /// </summary>
    public async Task<HealthStatus> SendHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var message = new IPCMessage
        {
            Type = "health_check",
            RequestId = Guid.NewGuid().ToString(),
            Payload = null,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var response = await SendMessageAsync<HealthStatus>(message, cancellationToken);
        return response ?? new HealthStatus { Healthy = false };
    }

    /// <summary>
    /// Sends a configuration update to the backend (API keys)
    /// </summary>
    public async Task<ConfigResponse> SendConfigUpdateAsync(ConfigUpdate config, CancellationToken cancellationToken = default)
    {
        var message = new IPCMessage
        {
            Type = "config_update",
            RequestId = Guid.NewGuid().ToString(),
            Payload = config,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var response = await SendMessageAsync<ConfigResponse>(message, cancellationToken);
        return response ?? new ConfigResponse { Success = false, Message = "Failed to get response from backend" };
    }

    /// <summary>
    /// Sends a prompt sync to the backend to update available prompts
    /// Requirements: 4.2, 4.3
    /// </summary>
    public async Task<PromptSyncResponse> SendPromptSyncAsync(IEnumerable<CustomPrompt> prompts, CancellationToken cancellationToken = default)
    {
        var payload = new PromptSyncPayload
        {
            Prompts = prompts.Select(CustomPromptDto.FromModel).ToList()
        };

        var message = new IPCMessage
        {
            Type = "prompt_sync",
            RequestId = Guid.NewGuid().ToString(),
            Payload = payload,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var response = await SendMessageAsync<PromptSyncResponse>(message, cancellationToken);
        return response ?? new PromptSyncResponse { Success = false, Message = "Failed to get response from backend" };
    }

    /// <summary>
    /// Disconnects from the backend
    /// </summary>
    public async Task DisconnectAsync()
    {
        await CleanupAsync();
        State = ConnectionState.Disconnected;
    }

    /// <summary>
    /// Sends a message and waits for response
    /// </summary>
    private async Task<T?> SendMessageAsync<T>(IPCMessage message, CancellationToken cancellationToken) where T : class
    {
        if (State != ConnectionState.Connected || _writer == null || _reader == null)
        {
            Logger.Error("SendMessageAsync: Not connected to backend");
            throw new InvalidOperationException("Not connected to backend");
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            // Serialize and send message
            var messageJson = JsonSerializer.Serialize(message);
            Logger.Debug($"Sending IPC message: {messageJson.Substring(0, Math.Min(200, messageJson.Length))}...");
            await _writer.WriteLineAsync(messageJson);

            // Read response with timeout
            using var timeoutCts = new CancellationTokenSource(ReadTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var responseJson = await ReadLineWithCancellationAsync(_reader, linkedCts.Token);
            if (string.IsNullOrEmpty(responseJson))
            {
                Logger.Warn("Received empty response from backend");
                return null;
            }

            Logger.Debug($"Received IPC response: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}...");

            // Parse response
            var response = JsonSerializer.Deserialize<IPCResponse>(responseJson);
            if (response?.Payload == null)
            {
                Logger.Warn("Response payload is null");
                return null;
            }

            // Deserialize payload to expected type
            var payloadJson = JsonSerializer.Serialize(response.Payload);
            return JsonSerializer.Deserialize<T>(payloadJson);
        }
        catch (OperationCanceledException)
        {
            Logger.Warn("IPC request was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"IPC error: {ex.Message}");
            State = ConnectionState.Error;
            ConnectionStateChanged?.Invoke(this, new ConnectionStateEventArgs(ConnectionState.Error, ex.Message));
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }


    /// <summary>
    /// Reads a line from the stream with cancellation support
    /// </summary>
    private static async Task<string?> ReadLineWithCancellationAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var readTask = reader.ReadLineAsync();
        var completedTask = await Task.WhenAny(readTask, Task.Delay(Timeout.Infinite, cancellationToken));
        
        if (completedTask == readTask)
        {
            var result = await readTask;
            // Strip BOM if present (can happen with some UTF-8 streams)
            if (result != null && result.Length > 0 && result[0] == '\uFEFF')
            {
                result = result.Substring(1);
            }
            return result;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }

    /// <summary>
    /// Cleans up resources
    /// </summary>
    private async Task CleanupAsync()
    {
        try
        {
            if (_writer != null)
            {
                await _writer.DisposeAsync();
                _writer = null;
            }

            _reader?.Dispose();
            _reader = null;

            if (_pipeClient != null)
            {
                await _pipeClient.DisposeAsync();
                _pipeClient = null;
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Disposes the client
    /// </summary>
    public void Dispose()
    {
        CleanupAsync().GetAwaiter().GetResult();
        _sendLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
