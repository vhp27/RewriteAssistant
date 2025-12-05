/**
 * Integration tests for IPC communication
 * 
 * Tests message serialization/deserialization and connection lifecycle
 * Requirements: 10.1
 */

import * as net from 'net';
import { APIKeyManager, KeyStatus } from './APIKeyManager';
import { RewriteService, IRewriteService, RewriteOptions } from './RewriteService';
import { ICerebrasClient } from './CerebrasClient';
import { 
  IPCMessage, 
  IPCResponse, 
  RewriteResponse, 
  RewriteRequest,
  HealthStatus,
  ConfigUpdate,
  ConfigResponse
} from '../models/types';
import { DEFAULT_PROMPTS } from '../models/defaults';

/**
 * Mock CerebrasClient for testing
 */
class MockCerebrasClient implements ICerebrasClient {
  async rewriteWithOptions(text: string, options: RewriteOptions, apiKey: string): Promise<string> {
    const promptId = options.promptId || 'grammar_fix_prompt';
    return `Rewritten (${promptId}): ${text}`;
  }
}

/**
 * Test-specific IPCServer that uses a unique pipe name
 */
class TestIPCServer {
  private server: net.Server | null = null;
  private connections: Set<net.Socket> = new Set();
  private rewriteServiceInstance: IRewriteService;
  private keyManagerInstance: APIKeyManager;
  private startTime: number = Date.now();
  private running: boolean = false;
  private pipeName: string;

  constructor(
    pipeName: string,
    rewriteServiceInst: IRewriteService,
    keyManagerInst: APIKeyManager
  ) {
    this.pipeName = pipeName;
    this.rewriteServiceInstance = rewriteServiceInst;
    this.keyManagerInstance = keyManagerInst;
  }

  async start(): Promise<void> {
    if (this.server) {
      throw new Error('Server is already running');
    }

    return new Promise((resolve, reject) => {
      this.server = net.createServer((socket) => {
        this.handleConnection(socket);
      });

      this.server.on('error', reject);

      this.server.listen(this.pipeName, () => {
        this.running = true;
        this.startTime = Date.now();
        resolve();
      });
    });
  }


  async stop(): Promise<void> {
    if (!this.server) {
      return;
    }

    return new Promise((resolve) => {
      for (const socket of this.connections) {
        socket.destroy();
      }
      this.connections.clear();

      this.server!.close(() => {
        this.server = null;
        this.running = false;
        resolve();
      });
    });
  }

  isRunning(): boolean {
    return this.running;
  }

  getPipeName(): string {
    return this.pipeName;
  }

  private handleConnection(socket: net.Socket): void {
    this.connections.add(socket);

    let buffer = '';

    socket.on('data', async (data) => {
      buffer += data.toString();
      
      const messages = buffer.split('\n');
      buffer = messages.pop() || '';

      for (const messageStr of messages) {
        if (messageStr.trim()) {
          await this.processMessage(socket, messageStr);
        }
      }
    });

    socket.on('close', () => {
      this.connections.delete(socket);
    });

    socket.on('error', () => {
      this.connections.delete(socket);
    });
  }

  private async processMessage(socket: net.Socket, messageStr: string): Promise<void> {
    let response: IPCResponse;

    try {
      const message: IPCMessage = JSON.parse(messageStr);
      response = await this.handleMessage(message);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      response = {
        requestId: '',
        success: false,
        payload: { success: false, rewrittenText: undefined, error: errorMessage, usedFallbackKey: false },
        error: `Failed to process message: ${errorMessage}`
      };
    }

    this.sendResponse(socket, response);
  }

  private async handleMessage(message: IPCMessage): Promise<IPCResponse> {
    switch (message.type) {
      case 'rewrite_request':
        return this.handleRewriteRequest(message);
      case 'health_check':
        return this.handleHealthCheck(message);
      case 'config_update':
        return this.handleConfigUpdate(message);
      default:
        return {
          requestId: message.requestId,
          success: false,
          payload: { success: false, rewrittenText: undefined, error: 'Unknown message type', usedFallbackKey: false },
          error: `Unknown message type: ${message.type}`
        };
    }
  }

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

  private sendResponse(socket: net.Socket, response: IPCResponse): void {
    try {
      const responseStr = JSON.stringify(response) + '\n';
      socket.write(responseStr);
    } catch (error) {
      // Ignore send errors
    }
  }
}

/**
 * Helper to create a client connection to the IPC server
 */
function createClient(pipeName: string): Promise<net.Socket> {
  return new Promise((resolve, reject) => {
    const client = net.createConnection(pipeName, () => {
      resolve(client);
    });
    client.on('error', reject);
  });
}

/**
 * Helper to send a message and receive a response
 */
function sendMessage(client: net.Socket, message: IPCMessage): Promise<IPCResponse> {
  return new Promise((resolve, reject) => {
    let buffer = '';
    
    const onData = (data: Buffer) => {
      buffer += data.toString();
      const lines = buffer.split('\n');
      
      for (const line of lines) {
        if (line.trim()) {
          try {
            const response = JSON.parse(line) as IPCResponse;
            client.removeListener('data', onData);
            resolve(response);
            return;
          } catch (e) {
            // Continue buffering
          }
        }
      }
    };
    
    client.on('data', onData);
    client.on('error', reject);
    
    client.write(JSON.stringify(message) + '\n');
  });
}

// Generate unique pipe name for each test
let testCounter = 0;
function getUniquePipeName(): string {
  return `\\\\.\\pipe\\RewriteAssistantIPCTest_${process.pid}_${++testCounter}_${Date.now()}`;
}


describe('IPC Communication Integration Tests', () => {
  let server: TestIPCServer;
  let keyManager: APIKeyManager;
  let mockClient: MockCerebrasClient;
  let rewriteService: RewriteService;
  let pipeName: string;

  // All valid prompt IDs from defaults
  const allPromptIds: string[] = DEFAULT_PROMPTS.map(p => p.id);

  beforeEach(async () => {
    pipeName = getUniquePipeName();
    keyManager = new APIKeyManager('test-secret');
    mockClient = new MockCerebrasClient();
    rewriteService = new RewriteService(keyManager, mockClient);
    server = new TestIPCServer(pipeName, rewriteService, keyManager);
    
    keyManager.setPrimaryKey('test-primary-key');
    keyManager.setFallbackKey('test-fallback-key');
    
    await server.start();
  });

  afterEach(async () => {
    await server.stop();
  });

  describe('Message Serialization/Deserialization', () => {
    it('should correctly serialize and deserialize rewrite request messages', async () => {
      const client = await createClient(pipeName);
      
      try {
        const message: IPCMessage = {
          type: 'rewrite_request',
          requestId: 'test-123',
          payload: {
            text: 'Hello world',
            promptId: 'grammar_fix_prompt',
            requestId: 'test-123'
          },
          timestamp: Date.now()
        };

        const response = await sendMessage(client, message);

        expect(response.requestId).toBe('test-123');
        expect(response.success).toBe(true);
        
        const payload = response.payload as RewriteResponse;
        expect(payload.success).toBe(true);
        expect(payload.rewrittenText).toContain('Rewritten');
        expect(payload.rewrittenText).toContain('Hello world');
      } finally {
        client.destroy();
      }
    });

    it('should correctly serialize and deserialize health check messages', async () => {
      const client = await createClient(pipeName);
      
      try {
        const message: IPCMessage = {
          type: 'health_check',
          requestId: 'health-456',
          payload: null,
          timestamp: Date.now()
        };

        const response = await sendMessage(client, message);

        expect(response.requestId).toBe('health-456');
        expect(response.success).toBe(true);
        
        const payload = response.payload as HealthStatus;
        expect(payload.healthy).toBe(true);
        expect(payload.primaryKeyValid).toBe(true);
        expect(payload.fallbackKeyValid).toBe(true);
        expect(typeof payload.uptime).toBe('number');
      } finally {
        client.destroy();
      }
    });

    it('should correctly serialize and deserialize config update messages', async () => {
      const client = await createClient(pipeName);
      
      try {
        const message: IPCMessage = {
          type: 'config_update',
          requestId: 'config-789',
          payload: {
            primaryApiKey: 'new-primary-key',
            fallbackApiKey: 'new-fallback-key'
          } as ConfigUpdate,
          timestamp: Date.now()
        };

        const response = await sendMessage(client, message);

        expect(response.requestId).toBe('config-789');
        expect(response.success).toBe(true);
        
        const payload = response.payload as ConfigResponse;
        expect(payload.success).toBe(true);
        expect(payload.message).toContain('successfully');
        
        expect(keyManager.getPrimaryKey()).toBe('new-primary-key');
        expect(keyManager.getFallbackKey()).toBe('new-fallback-key');
      } finally {
        client.destroy();
      }
    });

    it('should handle malformed JSON gracefully', async () => {
      const client = await createClient(pipeName);
      
      try {
        const responsePromise = new Promise<IPCResponse>((resolve) => {
          let buffer = '';
          client.on('data', (data) => {
            buffer += data.toString();
            const lines = buffer.split('\n');
            for (const line of lines) {
              if (line.trim()) {
                try {
                  resolve(JSON.parse(line));
                  return;
                } catch (e) {
                  // Continue
                }
              }
            }
          });
        });

        client.write('{ invalid json }\n');
        
        const response = await responsePromise;
        expect(response.success).toBe(false);
        expect(response.error).toBeDefined();
      } finally {
        client.destroy();
      }
    });

    it('should handle all prompt IDs correctly', async () => {
      const client = await createClient(pipeName);
      
      try {
        for (const promptId of allPromptIds) {
          const message: IPCMessage = {
            type: 'rewrite_request',
            requestId: `prompt-${promptId}`,
            payload: {
              text: 'Test text',
              promptId: promptId,
              requestId: `prompt-${promptId}`
            },
            timestamp: Date.now()
          };

          const response = await sendMessage(client, message);
          
          expect(response.success).toBe(true);
          const payload = response.payload as RewriteResponse;
          expect(payload.rewrittenText).toContain(promptId);
        }
      } finally {
        client.destroy();
      }
    });

    it('should handle empty text in rewrite request', async () => {
      const client = await createClient(pipeName);
      
      try {
        const message: IPCMessage = {
          type: 'rewrite_request',
          requestId: 'empty-text',
          payload: {
            text: '',
            promptId: 'grammar_fix_prompt',
            requestId: 'empty-text'
          },
          timestamp: Date.now()
        };

        const response = await sendMessage(client, message);
        
        expect(response.success).toBe(false);
        expect(response.error).toContain('missing text');
      } finally {
        client.destroy();
      }
    });
  });


  describe('Connection Lifecycle', () => {
    it('should accept multiple client connections', async () => {
      const client1 = await createClient(pipeName);
      const client2 = await createClient(pipeName);
      
      try {
        const message1: IPCMessage = {
          type: 'health_check',
          requestId: 'client1-check',
          payload: null,
          timestamp: Date.now()
        };

        const message2: IPCMessage = {
          type: 'health_check',
          requestId: 'client2-check',
          payload: null,
          timestamp: Date.now()
        };

        const [response1, response2] = await Promise.all([
          sendMessage(client1, message1),
          sendMessage(client2, message2)
        ]);

        expect(response1.requestId).toBe('client1-check');
        expect(response1.success).toBe(true);
        expect(response2.requestId).toBe('client2-check');
        expect(response2.success).toBe(true);
      } finally {
        client1.destroy();
        client2.destroy();
      }
    });

    it('should handle client disconnection gracefully', async () => {
      const client = await createClient(pipeName);
      
      const message: IPCMessage = {
        type: 'health_check',
        requestId: 'before-disconnect',
        payload: null,
        timestamp: Date.now()
      };

      const response = await sendMessage(client, message);
      expect(response.success).toBe(true);
      
      client.destroy();
      
      expect(server.isRunning()).toBe(true);
      
      const newClient = await createClient(pipeName);
      try {
        const newMessage: IPCMessage = {
          type: 'health_check',
          requestId: 'after-disconnect',
          payload: null,
          timestamp: Date.now()
        };

        const newResponse = await sendMessage(newClient, newMessage);
        expect(newResponse.success).toBe(true);
      } finally {
        newClient.destroy();
      }
    });

    it('should handle multiple sequential messages on same connection', async () => {
      const client = await createClient(pipeName);
      
      try {
        for (let i = 0; i < 5; i++) {
          const message: IPCMessage = {
            type: 'health_check',
            requestId: `sequential-${i}`,
            payload: null,
            timestamp: Date.now()
          };

          const response = await sendMessage(client, message);
          expect(response.requestId).toBe(`sequential-${i}`);
          expect(response.success).toBe(true);
        }
      } finally {
        client.destroy();
      }
    });

    it('should report correct running state', async () => {
      expect(server.isRunning()).toBe(true);
      
      await server.stop();
      expect(server.isRunning()).toBe(false);
    });
  });

  describe('Error Handling', () => {
    it('should return error for unknown message type', async () => {
      const client = await createClient(pipeName);
      
      try {
        const message = {
          type: 'unknown_type',
          requestId: 'unknown-type-test',
          payload: null,
          timestamp: Date.now()
        } as unknown as IPCMessage;

        const response = await sendMessage(client, message);
        
        expect(response.success).toBe(false);
        expect(response.error).toContain('Unknown message type');
      } finally {
        client.destroy();
      }
    });

    it('should handle missing payload in rewrite request', async () => {
      const client = await createClient(pipeName);
      
      try {
        const message: IPCMessage = {
          type: 'rewrite_request',
          requestId: 'missing-payload',
          payload: null,
          timestamp: Date.now()
        };

        const response = await sendMessage(client, message);
        
        expect(response.success).toBe(false);
        expect(response.error).toContain('Invalid request');
      } finally {
        client.destroy();
      }
    });
  });
});
