/**
 * Shared types and interfaces for IPC communication
 * These types must match the C# models in the frontend
 */

/**
 * Custom prompt configuration
 * Matches CustomPrompt class in C#
 */
export interface CustomPrompt {
  id: string;
  name: string;
  promptText: string;
  isBuiltIn: boolean;
  createdAt?: string;
  modifiedAt?: string;
}

/**
 * Hotkey configuration
 * Matches HotkeyConfig class in C#
 */
export interface HotkeyConfig {
  id: string;
  modifiers: string[];
  key: string;
}

/**
 * Custom style configuration
 * Matches CustomStyle class in C#
 */
export interface CustomStyle {
  id: string;
  name: string;
  promptId: string;
  hotkey?: HotkeyConfig;
  isBuiltIn: boolean;
}

/**
 * IPC message types
 */
export type IPCMessageType = 'rewrite_request' | 'config_update' | 'health_check' | 'prompt_sync';

/**
 * Rewrite request payload
 */
export interface RewriteRequest {
  text: string;
  promptId: string;
  promptText?: string;  // Optional override for custom prompts
  requestId: string;
}

/**
 * Rewrite response payload
 */
export interface RewriteResponse {
  success: boolean;
  rewrittenText?: string;
  error?: string;
  usedFallbackKey: boolean;
}

/**
 * Prompt sync payload for updating backend prompts
 */
export interface PromptSyncPayload {
  prompts: CustomPrompt[];
}

/**
 * Prompt sync response
 */
export interface PromptSyncResponse {
  success: boolean;
  message?: string;
  promptCount: number;
}

/**
 * Generic IPC message wrapper
 */
export interface IPCMessage {
  type: IPCMessageType;
  requestId: string;
  payload: RewriteRequest | ConfigUpdate | PromptSyncPayload | null;
  timestamp: number;
}

/**
 * Generic IPC response wrapper
 */
export interface IPCResponse {
  requestId: string;
  success: boolean;
  payload: RewriteResponse | ConfigResponse | HealthStatus | PromptSyncResponse;
  error?: string;
}

/**
 * Configuration update payload
 */
export interface ConfigUpdate {
  primaryApiKey?: string;
  fallbackApiKey?: string;
  isEnabled?: boolean;
}

/**
 * Configuration response payload
 */
export interface ConfigResponse {
  success: boolean;
  message?: string;
}

/**
 * Health check status
 */
export interface HealthStatus {
  healthy: boolean;
  primaryKeyValid: boolean;
  fallbackKeyValid: boolean;
  uptime: number;
}

/**
 * Rewrite result from the service layer
 */
export interface RewriteResult {
  success: boolean;
  text?: string;
  error?: string;
  usedFallback: boolean;
}
