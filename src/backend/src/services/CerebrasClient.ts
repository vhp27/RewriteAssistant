/**
 * CerebrasClient Service
 * Wrapper for Cerebras SDK interactions following official documentation
 * Requirements: 6.3, 6.6
 * 
 * Uses user-selected model from configuration
 * Follows Cerebras best practices - uses SDK defaults, no unnecessary parameters
 */

import Cerebras from '@cerebras/cerebras_cloud_sdk';
import { PromptBuilder, promptBuilder, ChatMessage } from './PromptBuilder';

/**
 * Default model to use when none is configured
 * Requirements: 6.5
 */
const DEFAULT_MODEL = 'gpt-oss-120b';



/**
 * JSON Schema for structured output enforcement
 * Ensures the model returns only the rewritten text in a predictable format
 * Requirements: 1.2, 1.3, 1.4, 5.1, 5.2
 */
export const REWRITE_RESPONSE_SCHEMA = {
  type: 'json_schema' as const,
  json_schema: {
    name: 'rewrite_response',
    description: 'Response containing only the rewritten text',
    schema: {
      type: 'object',
      properties: {
        rewritten_text: { type: 'string' }
      },
      required: ['rewritten_text'],
      additionalProperties: false
    },
    strict: true
  }
};

/**
 * Interface for the expected JSON response structure
 */
interface RewriteResponse {
  rewritten_text: string;
}

/**
 * Parses a structured JSON response and extracts the rewritten_text field.
 * Falls back to returning the raw content if parsing fails.
 * Requirements: 1.5, 1.6
 * 
 * @param content - The raw response content from the API
 * @returns The extracted rewritten_text or the raw content as fallback
 */
export function parseRewriteResponse(content: string): string {
  try {
    const parsed: RewriteResponse = JSON.parse(content);
    if (typeof parsed.rewritten_text === 'string') {
      return parsed.rewritten_text;
    }
    // If rewritten_text is missing or not a string, fall back to raw content
    console.warn('parseRewriteResponse: rewritten_text field missing or invalid, using raw content');
    return content;
  } catch (error) {
    // Log parsing error for debugging and return raw content as fallback
    console.warn('parseRewriteResponse: Failed to parse JSON response, using raw content:', error);
    return content;
  }
}

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
 * Simplified: promptText is required, no more promptId lookup
 */
export interface RewriteOptions {
  /** The prompt text to use for the system message */
  promptText: string;
}

/**
 * Interface for CerebrasClient
 */
export interface ICerebrasClient {
  /**
   * Rewrites text using promptText
   */
  rewriteWithOptions(text: string, options: RewriteOptions, apiKey: string): Promise<string>;
  
  /**
   * Sets the model to use for API calls
   * Requirements: 6.3
   */
  setSelectedModel(model: string): void;
  
  /**
   * Gets the currently selected model
   */
  getSelectedModel(): string;
}


/**
 * CerebrasClient implementation
 * Uses official Cerebras SDK following documentation best practices
 */
export class CerebrasClient implements ICerebrasClient {
  private promptBuilder: PromptBuilder;
  private selectedModel: string = DEFAULT_MODEL;

  constructor(promptBuilderInstance?: PromptBuilder) {
    this.promptBuilder = promptBuilderInstance || promptBuilder;
  }

  /**
   * Sets the model to use for API calls
   * Requirements: 6.3
   */
  setSelectedModel(model: string): void {
    if (model && model.trim()) {
      this.selectedModel = model.trim();
      console.log(`CerebrasClient: Model set to ${this.selectedModel}`);
    }
  }

  /**
   * Gets the currently selected model
   */
  getSelectedModel(): string {
    return this.selectedModel;
  }

  /**
   * Rewrites text using the Cerebras SDK with promptText
   * Uses the user-selected model from configuration
   * Requirements: 6.3, 6.6, 3.1, 3.2
   */
  async rewriteWithOptions(text: string, options: RewriteOptions, apiKey: string): Promise<string> {
    // Build messages using promptText directly
    const messages = this.promptBuilder.buildMessages(text, options.promptText);

    try {
      return await this.callAPI(messages, apiKey, this.selectedModel);
    } catch (error) {
      const cerebrasError = this.handleError(error);
      
      // If model is unavailable and we're not already using the default, try default model
      if (cerebrasError.shouldTryFallbackModel() && this.selectedModel !== DEFAULT_MODEL) {
        console.log(`Selected model ${this.selectedModel} unavailable, trying default ${DEFAULT_MODEL}`);
        try {
          return await this.callAPI(messages, apiKey, DEFAULT_MODEL);
        } catch (fallbackError) {
          throw this.handleError(fallbackError);
        }
      }
      
      throw cerebrasError;
    }
  }

  /**
   * Makes the API call using Cerebras SDK with structured output enforcement
   * Requirements: 1.1, 2.1, 2.2, 3.1, 3.2
   * 
   * @param messages - Chat messages to send
   * @param apiKey - API key for authentication
   * @param model - Model to use
   */
  private async callAPI(
    messages: ChatMessage[],
    apiKey: string,
    model: string
  ): Promise<string> {
    // Create client with API key per official docs
    const client = new Cerebras({ apiKey });

    // Build request with structured output parameters
    // Requirements: 1.1 (response_format), 2.1 (temperature), 2.2 (seed)
    // Note: prediction parameter removed - not supported by Cerebras API
    const requestParams = {
      model,
      messages: messages.map(msg => ({
        role: msg.role as 'system' | 'user' | 'assistant',
        content: msg.content
      })),
      response_format: REWRITE_RESPONSE_SCHEMA as { type: 'json_schema'; json_schema: { name: string; description: string; schema: Record<string, unknown>; strict: boolean } },
      temperature: 0.2,
      seed: 42
    };

    // Call chat completions with structured output enforcement
    const response = await client.chat.completions.create(requestParams);

    // Extract response content with proper typing
    const choices = response.choices as Array<{ message?: { content?: string | null } }>;
    const choice = choices?.[0];
    const content = choice?.message?.content;
    
    if (!content) {
      throw new Error('Invalid response format from Cerebras API');
    }

    // Parse structured JSON response and extract rewritten_text
    // Requirements: 1.5
    return parseRewriteResponse(content.trim());
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
