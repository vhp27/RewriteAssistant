/**
 * CerebrasClient Service
 * Wrapper for Cerebras SDK interactions following official documentation
 * Requirements: 10.1
 * 
 * Uses gpt-oss-120b as primary model with llama-3.3-70b as fallback
 * Follows Cerebras best practices - uses SDK defaults, no unnecessary parameters
 */

import Cerebras from '@cerebras/cerebras_cloud_sdk';
import { PromptBuilder, promptBuilder, ChatMessage } from './PromptBuilder';

/**
 * Available Cerebras models
 */
const MODELS = {
  PRIMARY: 'gpt-oss-120b',
  FALLBACK: 'llama-3.3-70b'
} as const;

/**
 * Error types for quota/rate-limit detection
 */
export enum CerebrasErrorType {
  QuotaExceeded = 'quota_exceeded',
  RateLimited = 'rate_limited',
  TokenExhausted = 'token_exhausted',
  InvalidKey = 'invalid_key',
  NetworkError = 'network_error',
  ModelUnavailable = 'model_unavailable',
  Unknown = 'unknown'
}

/**
 * Custom error class for Cerebras API errors
 */
export class CerebrasError extends Error {
  constructor(
    message: string,
    public readonly errorType: CerebrasErrorType,
    public readonly statusCode?: number
  ) {
    super(message);
    this.name = 'CerebrasError';
  }

  /**
   * Checks if this error should trigger a fallback to another API key
   */
  shouldFallback(): boolean {
    return [
      CerebrasErrorType.QuotaExceeded,
      CerebrasErrorType.RateLimited,
      CerebrasErrorType.TokenExhausted
    ].includes(this.errorType);
  }

  /**
   * Checks if this error should trigger a fallback to another model
   */
  shouldTryFallbackModel(): boolean {
    return this.errorType === CerebrasErrorType.ModelUnavailable;
  }
}

/**
 * Options for rewrite operation
 */
export interface RewriteOptions {
  /** The prompt ID to use (from PromptStore) */
  promptId?: string;
  /** Optional direct prompt text override */
  promptText?: string;
}

/**
 * Interface for CerebrasClient
 */
export interface ICerebrasClient {
  /**
   * Rewrites text using promptId or promptText
   */
  rewriteWithOptions(text: string, options: RewriteOptions, apiKey: string): Promise<string>;
}

/**
 * CerebrasClient implementation
 * Uses official Cerebras SDK following documentation best practices
 */
export class CerebrasClient implements ICerebrasClient {
  private promptBuilder: PromptBuilder;

  constructor(promptBuilderInstance?: PromptBuilder) {
    this.promptBuilder = promptBuilderInstance || promptBuilder;
  }

  /**
   * Rewrites text using the Cerebras SDK with flexible options
   * Supports promptId or promptText override
   * Tries primary model (gpt-oss-120b) first, falls back to llama-3.3-70b if unavailable
   * Requirements: 2.6
   */
  async rewriteWithOptions(text: string, options: RewriteOptions, apiKey: string): Promise<string> {
    // Build messages based on options priority: promptText > promptId
    let messages: ChatMessage[];
    
    if (options.promptText) {
      // Direct prompt text override
      messages = this.promptBuilder.buildMessagesWithCustomPrompt(text, options.promptText);
    } else if (options.promptId) {
      // Use prompt from PromptStore
      messages = this.promptBuilder.buildMessages(text, options.promptId);
    } else {
      // Default to grammar fix if nothing specified
      messages = this.promptBuilder.buildMessages(text, 'grammar_fix_prompt');
    }

    // Try primary model first
    try {
      return await this.callAPI(messages, apiKey, MODELS.PRIMARY);
    } catch (error) {
      const cerebrasError = this.handleError(error);
      
      // If model is unavailable, try fallback model
      if (cerebrasError.shouldTryFallbackModel()) {
        console.log(`Primary model ${MODELS.PRIMARY} unavailable, trying fallback ${MODELS.FALLBACK}`);
        try {
          return await this.callAPI(messages, apiKey, MODELS.FALLBACK);
        } catch (fallbackError) {
          throw this.handleError(fallbackError);
        }
      }
      
      throw cerebrasError;
    }
  }

  /**
   * Makes the API call using Cerebras SDK
   * Follows official documentation - minimal parameters, SDK defaults
   */
  private async callAPI(
    messages: ChatMessage[],
    apiKey: string,
    model: string
  ): Promise<string> {
    // Create client with API key per official docs
    const client = new Cerebras({ apiKey });

    // Call chat completions following official SDK pattern
    // Only required parameters: model and messages
    // Let SDK use defaults for temperature, max_tokens, etc.
    const response = await client.chat.completions.create({
      model,
      messages: messages.map(msg => ({
        role: msg.role as 'system' | 'user' | 'assistant',
        content: msg.content
      }))
    });

    // Extract response content with proper typing
    const choices = response.choices as Array<{ message?: { content?: string | null } }>;
    const choice = choices?.[0];
    const content = choice?.message?.content;
    
    if (!content) {
      throw new Error('Invalid response format from Cerebras API');
    }

    return content.trim();
  }

  /**
   * Handles and categorizes SDK errors
   */
  private handleError(error: unknown): CerebrasError {
    if (error instanceof CerebrasError) {
      return error;
    }

    const errorObj = error as { status?: number; message?: string; name?: string };
    const statusCode = errorObj.status;
    const message = errorObj.message || 'Unknown error';
    const errorName = errorObj.name || '';

    // Authentication errors (401, 403)
    if (errorName.includes('Authentication') || statusCode === 401 || statusCode === 403) {
      return new CerebrasError(
        'Invalid or unauthorized API key',
        CerebrasErrorType.InvalidKey,
        statusCode
      );
    }

    // Rate limit errors (429)
    if (errorName.includes('RateLimit') || statusCode === 429) {
      if (message.toLowerCase().includes('quota')) {
        return new CerebrasError(
          'API quota exceeded',
          CerebrasErrorType.QuotaExceeded,
          statusCode
        );
      }
      return new CerebrasError(
        'Rate limit exceeded',
        CerebrasErrorType.RateLimited,
        statusCode
      );
    }

    // Model unavailable (503, 404 for model, or specific error messages)
    if (
      statusCode === 503 ||
      (statusCode === 404 && message.toLowerCase().includes('model')) ||
      message.toLowerCase().includes('model') && message.toLowerCase().includes('unavailable')
    ) {
      return new CerebrasError(
        'Model temporarily unavailable',
        CerebrasErrorType.ModelUnavailable,
        statusCode
      );
    }

    // Token exhaustion (402)
    if (statusCode === 402) {
      return new CerebrasError(
        'Token quota exhausted',
        CerebrasErrorType.TokenExhausted,
        statusCode
      );
    }

    // Network/connection errors
    if (
      errorName.includes('Connection') ||
      errorName.includes('Network') ||
      (error instanceof TypeError && message.includes('fetch'))
    ) {
      return new CerebrasError(
        'Network error connecting to Cerebras API',
        CerebrasErrorType.NetworkError
      );
    }

    // Generic error
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';
    return new CerebrasError(errorMessage, CerebrasErrorType.Unknown, statusCode);
  }
}

// Export singleton instance
export const cerebrasClient = new CerebrasClient();
