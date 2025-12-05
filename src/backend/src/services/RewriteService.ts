/**
 * RewriteService
 * Orchestrates the rewrite flow with automatic API key fallback
 * Requirements: 2.6, 4.2, 4.4
 */

import { RewriteResult } from '../models/types';
import { IAPIKeyManager, KeyStatus, apiKeyManager } from './APIKeyManager';
import { ICerebrasClient, CerebrasError, cerebrasClient, RewriteOptions } from './CerebrasClient';

// Re-export RewriteOptions for consumers that import from RewriteService
export { RewriteOptions } from './CerebrasClient';

/**
 * Interface for RewriteService
 */
export interface IRewriteService {
  /**
   * Rewrites text using promptId or promptText
   * @param text - The text to rewrite
   * @param options - Rewrite options including promptId or promptText
   */
  rewrite(text: string, options: RewriteOptions): Promise<RewriteResult>;
}

/**
 * RewriteService implementation
 * Handles rewrite operations with automatic fallback on quota errors
 */
export class RewriteService implements IRewriteService {
  private keyManager: IAPIKeyManager;
  private client: ICerebrasClient;

  constructor(
    keyManagerInstance?: IAPIKeyManager,
    clientInstance?: ICerebrasClient
  ) {
    this.keyManager = keyManagerInstance || apiKeyManager;
    this.client = clientInstance || cerebrasClient;
  }

  /**
   * Rewrites text using the configured API keys with automatic fallback
   * Supports promptId or promptText override
   * @param text - The text to rewrite
   * @param options - Rewrite options including promptId or promptText
   * @returns RewriteResult with success status and rewritten text or error
   * Requirements: 2.6
   */
  async rewrite(text: string, options: RewriteOptions): Promise<RewriteResult> {
    // Try with primary key first
    const primaryKey = this.keyManager.getPrimaryKey();
    const primaryStatus = this.keyManager.getPrimaryKeyStatus();

    if (primaryKey && primaryStatus === KeyStatus.Active) {
      const result = await this.tryRewrite(text, options, primaryKey);
      if (result.success || !result.shouldFallback) {
        return {
          success: result.success,
          text: result.text,
          error: result.error,
          usedFallback: false
        };
      }
      // Primary failed with quota/rate error, mark as failed
      this.keyManager.markKeyFailed(true);
    }

    // Try with fallback key
    const fallbackKey = this.keyManager.getFallbackKey();
    const fallbackStatus = this.keyManager.getFallbackKeyStatus();

    if (fallbackKey && fallbackStatus === KeyStatus.Active) {
      const result = await this.tryRewrite(text, options, fallbackKey);
      if (!result.success && result.shouldFallback) {
        // Fallback also failed with quota error
        this.keyManager.markKeyFailed(false);
      }
      return {
        success: result.success,
        text: result.text,
        error: result.error,
        usedFallback: true
      };
    }

    // No valid keys available
    return {
      success: false,
      error: 'No valid API keys available. Both primary and fallback keys have failed or are not configured.',
      usedFallback: false
    };
  }

  /**
   * Attempts a rewrite with a specific API key
   */
  private async tryRewrite(
    text: string,
    options: RewriteOptions,
    apiKey: string
  ): Promise<{ success: boolean; text?: string; error?: string; shouldFallback: boolean }> {
    try {
      const rewrittenText = await this.client.rewriteWithOptions(text, options, apiKey);
      return {
        success: true,
        text: rewrittenText,
        shouldFallback: false
      };
    } catch (error) {
      if (error instanceof CerebrasError) {
        return {
          success: false,
          error: error.message,
          shouldFallback: error.shouldFallback()
        };
      }

      // Unknown error - don't fallback for unknown errors
      const errorMessage = error instanceof Error ? error.message : 'Unknown error occurred';
      return {
        success: false,
        error: errorMessage,
        shouldFallback: false
      };
    }
  }
}

// Export singleton instance
export const rewriteService = new RewriteService();
