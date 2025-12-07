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
export type IPCMessageType = 'rewrite_request' | 'config_update' | 'health_check' | 'list_models';

/**
 * Rewrite request payload
 */
export interface RewriteRequest {
  text: string;
  promptText: string;  // Required - the prompt text to use for rewriting
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
 * Generic IPC message wrapper
 */
export interface IPCMessage {
  type: IPCMessageType;
  requestId: string;
  payload: RewriteRequest | ConfigUpdate | null;
  timestamp: number;
}

/**
 * Generic IPC response wrapper
 */
export interface IPCResponse {
  requestId: string;
  success: boolean;
  payload: RewriteResponse | ConfigResponse | HealthStatus;
  error?: string;
}

/**
 * Configuration update payload
 */
export interface ConfigUpdate {
  primaryApiKey?: string;
  fallbackApiKey?: string;
  isEnabled?: boolean;
  /** Selected AI model for rewrite operations - Requirements: 6.3 */
  selectedModel?: string;
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

/**
 * List models request payload
 * Requirements: 6.1
 */
export interface ListModelsRequest {
  apiKey: string;
}

/**
 * Cerebras model information
 * Requirements: 6.1, 6.2
 */
export interface CerebrasModelInfo {
  id: string;
  object: string;
  created: number;
  owned_by: string;
}

/**
 * List models response payload
 * Requirements: 6.1
 */
export interface ListModelsResponse {
  success: boolean;
  models?: CerebrasModelInfo[];
  error?: string;
}
