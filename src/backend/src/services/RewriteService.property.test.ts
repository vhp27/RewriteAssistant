/**
 * Property-based tests for RewriteService
 * 
 * **Feature: ai-rewrite-assistant, Property 4: API key fallback on quota errors**
 * **Validates: Requirements 4.2, 4.4, 8.1**
 * 
 * **Feature: ai-rewrite-assistant, Property 2: Text preservation on failure**
 * **Validates: Requirements 7.3, 7.4, 8.3**
 */

import * as fc from 'fast-check';
import { RewriteService, RewriteOptions } from './RewriteService';
import { APIKeyManager, IAPIKeyManager, KeyStatus } from './APIKeyManager';
import { ICerebrasClient, CerebrasError, CerebrasErrorType } from './CerebrasClient';
import { DEFAULT_PROMPTS } from '../models/defaults';

/**
 * Mock CerebrasClient that can be configured to fail with specific errors
 */
class MockCerebrasClient implements ICerebrasClient {
  private primaryKeyBehavior: 'success' | 'quota' | 'rate_limit' | 'token_exhausted' | 'network' | 'invalid_key' = 'success';
  private fallbackKeyBehavior: 'success' | 'quota' | 'rate_limit' | 'token_exhausted' | 'network' | 'invalid_key' = 'success';
  private primaryKey: string | null = null;
  private fallbackKey: string | null = null;
  public callLog: Array<{ text: string; options: RewriteOptions; apiKey: string }> = [];

  setPrimaryKey(key: string): void {
    this.primaryKey = key;
  }

  setFallbackKey(key: string): void {
    this.fallbackKey = key;
  }

  setPrimaryKeyBehavior(behavior: 'success' | 'quota' | 'rate_limit' | 'token_exhausted' | 'network' | 'invalid_key'): void {
    this.primaryKeyBehavior = behavior;
  }

  setFallbackKeyBehavior(behavior: 'success' | 'quota' | 'rate_limit' | 'token_exhausted' | 'network' | 'invalid_key'): void {
    this.fallbackKeyBehavior = behavior;
  }

  async rewriteWithOptions(text: string, options: RewriteOptions, apiKey: string): Promise<string> {
    this.callLog.push({ text, options, apiKey });

    const behavior = apiKey === this.primaryKey ? this.primaryKeyBehavior : this.fallbackKeyBehavior;

    switch (behavior) {
      case 'success':
        return `Rewritten: ${text}`;
      case 'quota':
        throw new CerebrasError('API quota exceeded', CerebrasErrorType.QuotaExceeded, 429);
      case 'rate_limit':
        throw new CerebrasError('Rate limit exceeded', CerebrasErrorType.RateLimited, 429);
      case 'token_exhausted':
        throw new CerebrasError('Token quota exhausted', CerebrasErrorType.TokenExhausted, 402);
      case 'network':
        throw new CerebrasError('Network error', CerebrasErrorType.NetworkError);
      case 'invalid_key':
        throw new CerebrasError('Invalid API key', CerebrasErrorType.InvalidKey, 401);
      default:
        throw new Error('Unknown behavior');
    }
  }

  reset(): void {
    this.callLog = [];
    this.primaryKeyBehavior = 'success';
    this.fallbackKeyBehavior = 'success';
  }
}

describe('Property 4: API key fallback on quota errors', () => {
  // All valid prompt IDs from defaults
  const allPromptIds: string[] = DEFAULT_PROMPTS.map(p => p.id);

  // Arbitraries for generating test data
  const textArb = fc.string({ minLength: 1, maxLength: 500 });
  const apiKeyArb = fc.string({ minLength: 10, maxLength: 50 }).filter(s => s.trim().length > 0);
  const promptIdArb = fc.constantFrom(...allPromptIds);
  
  // Arbitrary for quota-related error types that should trigger fallback
  const quotaErrorArb = fc.constantFrom<'quota' | 'rate_limit' | 'token_exhausted'>(
    'quota',
    'rate_limit',
    'token_exhausted'
  );


  /**
   * **Feature: ai-rewrite-assistant, Property 4: API key fallback on quota errors**
   * **Validates: Requirements 4.2, 4.4, 8.1**
   * 
   * When primary key fails with quota/rate-limit/token error, system should
   * automatically try the fallback key.
   */
  it('should automatically switch to fallback key on quota errors', async () => {
    await fc.assert(
      fc.asyncProperty(
        textArb,
        promptIdArb,
        apiKeyArb,
        apiKeyArb,
        quotaErrorArb,
        async (text, promptId, primaryKey, fallbackKey, errorType) => {
          // Ensure keys are different
          fc.pre(primaryKey !== fallbackKey);

          const mockClient = new MockCerebrasClient();
          const keyManager = new APIKeyManager('test-secret');
          
          // Set up keys
          keyManager.setPrimaryKey(primaryKey);
          keyManager.setFallbackKey(fallbackKey);
          mockClient.setPrimaryKey(primaryKey);
          mockClient.setFallbackKey(fallbackKey);
          
          // Configure primary to fail with quota error, fallback to succeed
          mockClient.setPrimaryKeyBehavior(errorType);
          mockClient.setFallbackKeyBehavior('success');
          
          const service = new RewriteService(keyManager, mockClient);
          
          // Execute rewrite
          const result = await service.rewrite(text, { promptId });
          
          // Should succeed using fallback
          expect(result.success).toBe(true);
          expect(result.usedFallback).toBe(true);
          
          // Should have called both keys - primary first, then fallback
          expect(mockClient.callLog.length).toBe(2);
          expect(mockClient.callLog[0].apiKey).toBe(primaryKey);
          expect(mockClient.callLog[1].apiKey).toBe(fallbackKey);
          
          // Primary key should be marked as failed
          expect(keyManager.getPrimaryKeyStatus()).toBe(KeyStatus.Failed);
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: ai-rewrite-assistant, Property 4: API key fallback on quota errors**
   * **Validates: Requirements 4.2, 4.4, 8.1**
   * 
   * Fallback should happen seamlessly without user intervention - the result
   * should contain the rewritten text when fallback succeeds.
   */
  it('should continue rewriting seamlessly when fallback key succeeds', async () => {
    await fc.assert(
      fc.asyncProperty(
        textArb,
        promptIdArb,
        apiKeyArb,
        apiKeyArb,
        quotaErrorArb,
        async (text, promptId, primaryKey, fallbackKey, errorType) => {
          fc.pre(primaryKey !== fallbackKey);

          const mockClient = new MockCerebrasClient();
          const keyManager = new APIKeyManager('test-secret');
          
          keyManager.setPrimaryKey(primaryKey);
          keyManager.setFallbackKey(fallbackKey);
          mockClient.setPrimaryKey(primaryKey);
          mockClient.setFallbackKey(fallbackKey);
          
          mockClient.setPrimaryKeyBehavior(errorType);
          mockClient.setFallbackKeyBehavior('success');
          
          const service = new RewriteService(keyManager, mockClient);
          
          const result = await service.rewrite(text, { promptId });
          
          // Result should contain rewritten text
          expect(result.success).toBe(true);
          expect(result.text).toBeDefined();
          expect(result.text).toContain('Rewritten');
          expect(result.error).toBeUndefined();
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: ai-rewrite-assistant, Property 4: API key fallback on quota errors**
   * **Validates: Requirements 4.2, 4.4, 8.1**
   * 
   * Non-quota errors (network, invalid key) should NOT trigger fallback to
   * another API key.
   */
  it('should not fallback on non-quota errors', async () => {
    const nonQuotaErrorArb = fc.constantFrom<'network' | 'invalid_key'>('network', 'invalid_key');

    await fc.assert(
      fc.asyncProperty(
        textArb,
        promptIdArb,
        apiKeyArb,
        apiKeyArb,
        nonQuotaErrorArb,
        async (text, promptId, primaryKey, fallbackKey, errorType) => {
          fc.pre(primaryKey !== fallbackKey);

          const mockClient = new MockCerebrasClient();
          const keyManager = new APIKeyManager('test-secret');
          
          keyManager.setPrimaryKey(primaryKey);
          keyManager.setFallbackKey(fallbackKey);
          mockClient.setPrimaryKey(primaryKey);
          mockClient.setFallbackKey(fallbackKey);
          
          mockClient.setPrimaryKeyBehavior(errorType);
          mockClient.setFallbackKeyBehavior('success');
          
          const service = new RewriteService(keyManager, mockClient);
          
          const result = await service.rewrite(text, { promptId });
          
          // Should fail without trying fallback
          expect(result.success).toBe(false);
          expect(result.usedFallback).toBe(false);
          
          // Should have only called primary key
          expect(mockClient.callLog.length).toBe(1);
          expect(mockClient.callLog[0].apiKey).toBe(primaryKey);
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: ai-rewrite-assistant, Property 4: API key fallback on quota errors**
   * **Validates: Requirements 4.2, 4.4, 8.1**
   * 
   * When both keys fail with quota errors, the operation should fail gracefully.
   */
  it('should fail gracefully when both keys have quota errors', async () => {
    await fc.assert(
      fc.asyncProperty(
        textArb,
        promptIdArb,
        apiKeyArb,
        apiKeyArb,
        quotaErrorArb,
        quotaErrorArb,
        async (text, promptId, primaryKey, fallbackKey, primaryError, fallbackError) => {
          fc.pre(primaryKey !== fallbackKey);

          const mockClient = new MockCerebrasClient();
          const keyManager = new APIKeyManager('test-secret');
          
          keyManager.setPrimaryKey(primaryKey);
          keyManager.setFallbackKey(fallbackKey);
          mockClient.setPrimaryKey(primaryKey);
          mockClient.setFallbackKey(fallbackKey);
          
          mockClient.setPrimaryKeyBehavior(primaryError);
          mockClient.setFallbackKeyBehavior(fallbackError);
          
          const service = new RewriteService(keyManager, mockClient);
          
          const result = await service.rewrite(text, { promptId });
          
          // Should fail
          expect(result.success).toBe(false);
          expect(result.usedFallback).toBe(true);
          expect(result.error).toBeDefined();
          
          // Both keys should have been tried
          expect(mockClient.callLog.length).toBe(2);
          
          // Both keys should be marked as failed
          expect(keyManager.getPrimaryKeyStatus()).toBe(KeyStatus.Failed);
          expect(keyManager.getFallbackKeyStatus()).toBe(KeyStatus.Failed);
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: ai-rewrite-assistant, Property 4: API key fallback on quota errors**
   * **Validates: Requirements 4.2, 4.4, 8.1**
   * 
   * When primary key is already marked as failed, should go directly to fallback.
   */
  it('should skip failed primary key and use fallback directly', async () => {
    await fc.assert(
      fc.asyncProperty(
        textArb,
        promptIdArb,
        apiKeyArb,
        apiKeyArb,
        async (text, promptId, primaryKey, fallbackKey) => {
          fc.pre(primaryKey !== fallbackKey);

          const mockClient = new MockCerebrasClient();
          const keyManager = new APIKeyManager('test-secret');
          
          keyManager.setPrimaryKey(primaryKey);
          keyManager.setFallbackKey(fallbackKey);
          mockClient.setPrimaryKey(primaryKey);
          mockClient.setFallbackKey(fallbackKey);
          
          // Mark primary as already failed
          keyManager.markKeyFailed(true);
          
          mockClient.setPrimaryKeyBehavior('success');
          mockClient.setFallbackKeyBehavior('success');
          
          const service = new RewriteService(keyManager, mockClient);
          
          const result = await service.rewrite(text, { promptId });
          
          // Should succeed using fallback
          expect(result.success).toBe(true);
          expect(result.usedFallback).toBe(true);
          
          // Should have only called fallback key (skipped primary)
          expect(mockClient.callLog.length).toBe(1);
          expect(mockClient.callLog[0].apiKey).toBe(fallbackKey);
        }
      ),
      { numRuns: 100 }
    );
  });
});



/**
 * Property 2: Text preservation on failure
 * 
 * **Feature: ai-rewrite-assistant, Property 2: Text preservation on failure**
 * **Validates: Requirements 7.3, 7.4, 8.3**
 * 
 * Property: For any rewrite operation that fails (network error, API error, or
 * any exception), the original text in the editable field must remain unchanged
 * and uncorrupted.
 */
describe('Property 2: Text preservation on failure', () => {
  // All valid prompt IDs from defaults
  const allPromptIds: string[] = DEFAULT_PROMPTS.map(p => p.id);

  // Arbitraries for generating test data
  const textArb = fc.string({ minLength: 1, maxLength: 500 });
  const apiKeyArb = fc.string({ minLength: 10, maxLength: 50 }).filter(s => s.trim().length > 0);
  const promptIdArb = fc.constantFrom(...allPromptIds);

  // All possible error types that can cause failure
  const allErrorArb = fc.constantFrom<'quota' | 'rate_limit' | 'token_exhausted' | 'network' | 'invalid_key'>(
    'quota',
    'rate_limit',
    'token_exhausted',
    'network',
    'invalid_key'
  );

  /**
   * **Feature: ai-rewrite-assistant, Property 2: Text preservation on failure**
   * **Validates: Requirements 7.3, 7.4, 8.3**
   * 
   * When a rewrite fails, the result should not contain corrupted text.
   * The service should return success=false with an error message, allowing
   * the caller to preserve the original text.
   */
  it('should return failure result without corrupted text on any error', async () => {
    await fc.assert(
      fc.asyncProperty(
        textArb,
        promptIdArb,
        apiKeyArb,
        apiKeyArb,
        allErrorArb,
        allErrorArb,
        async (originalText, promptId, primaryKey, fallbackKey, primaryError, fallbackError) => {
          fc.pre(primaryKey !== fallbackKey);

          const mockClient = new MockCerebrasClient();
          const keyManager = new APIKeyManager('test-secret');
          
          keyManager.setPrimaryKey(primaryKey);
          keyManager.setFallbackKey(fallbackKey);
          mockClient.setPrimaryKey(primaryKey);
          mockClient.setFallbackKey(fallbackKey);
          
          // Configure both keys to fail
          mockClient.setPrimaryKeyBehavior(primaryError);
          mockClient.setFallbackKeyBehavior(fallbackError);
          
          const service = new RewriteService(keyManager, mockClient);
          
          const result = await service.rewrite(originalText, { promptId });
          
          // When both keys fail, result should indicate failure
          // The result.text should either be undefined or not be a corrupted version
          if (!result.success) {
            // On failure, text should be undefined (not corrupted)
            expect(result.text).toBeUndefined();
            // Error message should be present
            expect(result.error).toBeDefined();
            expect(typeof result.error).toBe('string');
          }
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: ai-rewrite-assistant, Property 2: Text preservation on failure**
   * **Validates: Requirements 7.3, 7.4, 8.3**
   * 
   * Network errors should preserve original text by returning failure.
   */
  it('should preserve original text on network errors', async () => {
    await fc.assert(
      fc.asyncProperty(
        textArb,
        promptIdArb,
        apiKeyArb,
        async (originalText, promptId, apiKey) => {
          const mockClient = new MockCerebrasClient();
          const keyManager = new APIKeyManager('test-secret');
          
          keyManager.setPrimaryKey(apiKey);
          mockClient.setPrimaryKey(apiKey);
          
          // Configure to fail with network error
          mockClient.setPrimaryKeyBehavior('network');
          
          const service = new RewriteService(keyManager, mockClient);
          
          const result = await service.rewrite(originalText, { promptId });
          
          // Should fail
          expect(result.success).toBe(false);
          // Text should be undefined (caller preserves original)
          expect(result.text).toBeUndefined();
          // Error should mention network
          expect(result.error).toBeDefined();
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: ai-rewrite-assistant, Property 2: Text preservation on failure**
   * **Validates: Requirements 7.3, 7.4, 8.3**
   * 
   * Invalid API key errors should preserve original text by returning failure.
   */
  it('should preserve original text on invalid API key errors', async () => {
    await fc.assert(
      fc.asyncProperty(
        textArb,
        promptIdArb,
        apiKeyArb,
        async (originalText, promptId, apiKey) => {
          const mockClient = new MockCerebrasClient();
          const keyManager = new APIKeyManager('test-secret');
          
          keyManager.setPrimaryKey(apiKey);
          mockClient.setPrimaryKey(apiKey);
          
          // Configure to fail with invalid key error
          mockClient.setPrimaryKeyBehavior('invalid_key');
          
          const service = new RewriteService(keyManager, mockClient);
          
          const result = await service.rewrite(originalText, { promptId });
          
          // Should fail
          expect(result.success).toBe(false);
          // Text should be undefined (caller preserves original)
          expect(result.text).toBeUndefined();
          // Error should be present
          expect(result.error).toBeDefined();
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: ai-rewrite-assistant, Property 2: Text preservation on failure**
   * **Validates: Requirements 7.3, 7.4, 8.3**
   * 
   * When no API keys are configured, should fail gracefully without corrupting text.
   */
  it('should fail gracefully when no API keys are configured', async () => {
    await fc.assert(
      fc.asyncProperty(
        textArb,
        promptIdArb,
        async (originalText, promptId) => {
          const mockClient = new MockCerebrasClient();
          const keyManager = new APIKeyManager('test-secret');
          
          // Don't set any keys
          const service = new RewriteService(keyManager, mockClient);
          
          const result = await service.rewrite(originalText, { promptId });
          
          // Should fail
          expect(result.success).toBe(false);
          // Text should be undefined
          expect(result.text).toBeUndefined();
          // Error should indicate no keys available
          expect(result.error).toBeDefined();
          expect(result.error).toContain('No valid API keys');
          // No API calls should have been made
          expect(mockClient.callLog.length).toBe(0);
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: ai-rewrite-assistant, Property 2: Text preservation on failure**
   * **Validates: Requirements 7.3, 7.4, 8.3**
   * 
   * The service should never throw an exception - it should always return a result.
   */
  it('should never throw exceptions, always return a result', async () => {
    await fc.assert(
      fc.asyncProperty(
        textArb,
        promptIdArb,
        apiKeyArb,
        allErrorArb,
        async (originalText, promptId, apiKey, errorType) => {
          const mockClient = new MockCerebrasClient();
          const keyManager = new APIKeyManager('test-secret');
          
          keyManager.setPrimaryKey(apiKey);
          mockClient.setPrimaryKey(apiKey);
          mockClient.setPrimaryKeyBehavior(errorType);
          
          const service = new RewriteService(keyManager, mockClient);
          
          // Should not throw - should return a result
          let result;
          let didThrow = false;
          try {
            result = await service.rewrite(originalText, { promptId });
          } catch (e) {
            didThrow = true;
          }
          
          expect(didThrow).toBe(false);
          expect(result).toBeDefined();
          expect(typeof result!.success).toBe('boolean');
        }
      ),
      { numRuns: 100 }
    );
  });
});
