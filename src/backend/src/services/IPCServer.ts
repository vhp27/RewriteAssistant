/**
 * IPCServer
 * Named pipe server for Windows IPC communication with C# frontend
 * Requirements: 10.1
 */

import * as net from 'net';
import { IPCMessage, IPCResponse, RewriteRequest, RewriteResponse, HealthStatus, ConfigUpdate, ConfigResponse, PromptSyncPayload } from '../models/types';
import { RewriteService, rewriteService, IRewriteService } from './RewriteService';
import { apiKeyManager, IAPIKeyManager, KeyStatus } from './APIKeyManager';
import { IPromptStore, promptStore } from './PromptStore';

// Named pipe path for Windows
const PIPE_NAME = '\\\\.\\pipe\\RewriteAssistantIPC';

/**
 * Interface for IPCServer
 */
export interface IIPCServer {
  start(): Promise<void>;
  stop(): Promise<void>;
  onRequest(handler: (request: IPCMessage) => Promise<IPCResponse>): void;
  isRunning(): boolean;
}

/**
 * IPCServer implementation using Windows named pipes
 */
export class IPCServer implements IIPCServer {
  private server: net.Server | null = null;
  private connections: Set<net.Socket> = new Set();
  private requestHandler: ((request: IPCMessage) => Promise<IPCResponse>) | null = null;
  private rewriteServiceInstance: IRewriteService;
  private keyManagerInstance: IAPIKeyManager;
  private promptStoreInstance: IPromptStore;
  private startTime: number = Date.now();
  private running: boolean = false;

  constructor(
    rewriteServiceInst?: IRewriteService,
    keyManagerInst?: IAPIKeyManager,
    promptStoreInst?: IPromptStore
  ) {
    this.rewriteServiceInstance = rewriteServiceInst || rewriteService;
    this.keyManagerInstance = keyManagerInst || apiKeyManager;
    this.promptStoreInstance = promptStoreInst || promptStore;
  }

  /**
   * Starts the named pipe server
   */
  async start(): Promise<void> {
    if (this.server) {
      throw new Error('Server is already running');
    }

    return new Promise((resolve, reject) => {
      this.server = net.createServer((socket) => {
        this.handleConnection(socket);
      });

      this.server.on('error', (err) => {
        console.error('IPC Server error:', err);
        reject(err);
      });

      this.server.listen(PIPE_NAME, () => {
        this.running = true;
        this.startTime = Date.now();
        console.log(`IPC Server listening on ${PIPE_NAME}`);
        resolve();
      });
    });
  }

  /**
   * Stops the named pipe server and closes all connections
   */
  async stop(): Promise<void> {
    if (!this.server) {
      return;
    }

    return new Promise((resolve) => {
      // Close all active connections
      for (const socket of this.connections) {
        socket.destroy();
      }
      this.connections.clear();

      this.server!.close(() => {
        this.server = null;
        this.running = false;
        console.log('IPC Server stopped');
        resolve();
      });
    });
  }

  /**
   * Registers a custom request handler
   */
  onRequest(handler: (request: IPCMessage) => Promise<IPCResponse>): void {
    this.requestHandler = handler;
  }

  /**
   * Returns whether the server is running
   */
  isRunning(): boolean {
    return this.running;
  }

  /**
   * Handles a new client connection
   */
  private handleConnection(socket: net.Socket): void {
    this.connections.add(socket);
    console.log('Client connected');

    let buffer = '';

    socket.on('data', async (data) => {
      buffer += data.toString();
      
      // Process complete messages (delimited by newline)
      const messages = buffer.split('\n');
      buffer = messages.pop() || ''; // Keep incomplete message in buffer

      for (const messageStr of messages) {
        if (messageStr.trim()) {
          await this.processMessage(socket, messageStr);
        }
      }
    });

    socket.on('close', () => {
      this.connections.delete(socket);
      console.log('Client disconnected');
    });

    socket.on('error', (err) => {
      console.error('Socket error:', err);
      this.connections.delete(socket);
    });
  }


  /**
   * Processes a single IPC message
   */
  private async processMessage(socket: net.Socket, messageStr: string): Promise<void> {
    let response: IPCResponse;

    try {
      const message: IPCMessage = JSON.parse(messageStr);
      
      // Use custom handler if registered
      if (this.requestHandler) {
        response = await this.requestHandler(message);
      } else {
        response = await this.handleMessage(message);
      }
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      response = {
        requestId: '',
        success: false,
        payload: { success: false, rewrittenText: undefined, error: errorMessage, usedFallbackKey: false },
        error: `Failed to process message: ${errorMessage}`
      };
    }

    // Send response back to client
    this.sendResponse(socket, response);
  }

  /**
   * Handles different message types
   */
  private async handleMessage(message: IPCMessage): Promise<IPCResponse> {
    switch (message.type) {
      case 'rewrite_request':
        return this.handleRewriteRequest(message);
      case 'health_check':
        return this.handleHealthCheck(message);
      case 'config_update':
        return this.handleConfigUpdate(message);
      case 'prompt_sync':
        return this.handlePromptSync(message);
      default:
        return {
          requestId: message.requestId,
          success: false,
          payload: { success: false, rewrittenText: undefined, error: 'Unknown message type', usedFallbackKey: false },
          error: `Unknown message type: ${message.type}`
        };
    }
  }

  /**
   * Handles rewrite requests
   * Supports promptId or promptText override
   * Requirements: 2.6
   */
  private async handleRewriteRequest(message: IPCMessage): Promise<IPCResponse> {
    const request = message.payload as RewriteRequest;
    
    if (!request || !request.text) {
      return {
        requestId: message.requestId,
        success: false,
        payload: { success: false, rewrittenText: undefined, error: 'Invalid request: missing text', usedFallbackKey: false },
        error: 'Invalid request: missing text'
      };
    }

    try {
      // Build rewrite options from request - prioritize promptText > promptId
      const result = await this.rewriteServiceInstance.rewrite(request.text, {
        promptText: request.promptText,
        promptId: request.promptId
      });
      
      const responsePayload: RewriteResponse = {
        success: result.success,
        rewrittenText: result.text,
        error: result.error,
        usedFallbackKey: result.usedFallback
      };

      return {
        requestId: message.requestId,
        success: result.success,
        payload: responsePayload,
        error: result.error
      };
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      return {
        requestId: message.requestId,
        success: false,
        payload: { success: false, rewrittenText: undefined, error: errorMessage, usedFallbackKey: false },
        error: errorMessage
      };
    }
  }


  /**
   * Handles health check requests
   */
  private handleHealthCheck(message: IPCMessage): IPCResponse {
    const primaryKey = this.keyManagerInstance.getPrimaryKey();
    const fallbackKey = this.keyManagerInstance.getFallbackKey();
    const primaryStatus = this.keyManagerInstance.getPrimaryKeyStatus();
    const fallbackStatus = this.keyManagerInstance.getFallbackKeyStatus();

    const healthStatus: HealthStatus = {
      healthy: this.running,
      primaryKeyValid: !!primaryKey && primaryStatus === KeyStatus.Active,
      fallbackKeyValid: !!fallbackKey && fallbackStatus === KeyStatus.Active,
      uptime: Date.now() - this.startTime
    };

    return {
      requestId: message.requestId,
      success: true,
      payload: healthStatus
    };
  }

  /**
   * Handles configuration update requests
   */
  private handleConfigUpdate(message: IPCMessage): IPCResponse {
    const config = message.payload as ConfigUpdate;
    
    try {
      if (config.primaryApiKey !== undefined) {
        this.keyManagerInstance.setPrimaryKey(config.primaryApiKey);
      }
      if (config.fallbackApiKey !== undefined) {
        this.keyManagerInstance.setFallbackKey(config.fallbackApiKey);
      }

      const configResponse: ConfigResponse = {
        success: true,
        message: 'Configuration updated successfully'
      };

      return {
        requestId: message.requestId,
        success: true,
        payload: configResponse
      };
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      const configResponse: ConfigResponse = {
        success: false,
        message: errorMessage
      };

      return {
        requestId: message.requestId,
        success: false,
        payload: configResponse,
        error: errorMessage
      };
    }
  }

  /**
   * Handles prompt sync requests
   * Updates the PromptStore with new prompts from the frontend
   * Requirements: 4.2, 4.3
   */
  private handlePromptSync(message: IPCMessage): IPCResponse {
    const payload = message.payload as PromptSyncPayload;
    
    try {
      if (!payload || !Array.isArray(payload.prompts)) {
        return {
          requestId: message.requestId,
          success: false,
          payload: { success: false, message: 'Invalid prompt sync payload: missing prompts array', promptCount: 0 },
          error: 'Invalid prompt sync payload: missing prompts array'
        };
      }

      // Update the prompt store with the new prompts
      this.promptStoreInstance.setPrompts(payload.prompts);

      const promptCount = payload.prompts.length;
      console.log(`Prompt sync completed: ${promptCount} prompts loaded`);

      return {
        requestId: message.requestId,
        success: true,
        payload: { 
          success: true, 
          message: `Successfully synced ${promptCount} prompts`,
          promptCount 
        }
      };
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      console.error('Prompt sync error:', errorMessage);
      
      return {
        requestId: message.requestId,
        success: false,
        payload: { success: false, message: errorMessage, promptCount: 0 },
        error: errorMessage
      };
    }
  }

  /**
   * Sends a response to the client
   */
  private sendResponse(socket: net.Socket, response: IPCResponse): void {
    try {
      const responseStr = JSON.stringify(response) + '\n';
      socket.write(responseStr);
    } catch (error) {
      console.error('Failed to send response:', error);
    }
  }
}

// Export singleton instance
export const ipcServer = new IPCServer();

// Export pipe name for client configuration
export { PIPE_NAME };
