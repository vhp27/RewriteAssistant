/**
 * Property-based tests for API key storage round-trip
 * 
 * **Feature: ai-rewrite-assistant, Property 7: API key storage round-trip**
 * **Validates: Requirements 4.1**
 * 
 * Property: For any API key stored by the system, encrypting then decrypting
 * should produce the original key value, and the stored format should not
 * contain the plaintext key.
 */

import * as fc from 'fast-check';
import { APIKeyManager } from './APIKeyManager';

describe('Property 7: API key storage round-trip', () => {
  // Use a consistent encryption secret for testing
  const testSecret = 'test-encryption-secret-for-property-tests';
  
  // Arbitrary for generating valid API key strings
  // Cerebras API keys are typically alphanumeric strings
  const apiKeyArb = fc.string({ minLength: 1, maxLength: 100 })
    .filter(s => s.length > 0);

  /**
   * **Feature: ai-rewrite-assistant, Property 7: API key storage round-trip**
   * **Validates: Requirements 4.1**
   * 
   * For any API key, encrypting then decrypting should produce the original key.
   */
  it('should round-trip any API key through encrypt/decrypt', () => {
    const keyManager = new APIKeyManager(testSecret);

    fc.assert(
      fc.property(apiKeyArb, (originalKey) => {
        // Encrypt the key
        const encrypted = keyManager.encryptKey(originalKey);
        
        // Decrypt the key
        const decrypted = keyManager.decryptKey(encrypted);
        
        // The decrypted key should match the original
        expect(decrypted).toBe(originalKey);
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: ai-rewrite-assistant, Property 7: API key storage round-trip**
   * **Validates: Requirements 4.1**
   * 
   * The encrypted format should not contain the plaintext key.
   */
  it('should not contain plaintext key in encrypted output', () => {
    const keyManager = new APIKeyManager(testSecret);

    fc.assert(
      fc.property(apiKeyArb, (originalKey) => {
        // Encrypt the key
        const encrypted = keyManager.encryptKey(originalKey);
        
        // The encrypted output should not contain the plaintext key
        // (unless the key is very short and could appear by coincidence in base64)
        if (originalKey.length >= 4) {
          expect(encrypted).not.toContain(originalKey);
        }
        
        // The encrypted output should be base64 encoded
        expect(() => Buffer.from(encrypted, 'base64')).not.toThrow();
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: ai-rewrite-assistant, Property 7: API key storage round-trip**
   * **Validates: Requirements 4.1**
   * 
   * Different encryption calls should produce different ciphertexts (due to random IV).
   */
  it('should produce different ciphertexts for same key (semantic security)', () => {
    const keyManager = new APIKeyManager(testSecret);

    fc.assert(
      fc.property(apiKeyArb, (originalKey) => {
        // Encrypt the same key twice
        const encrypted1 = keyManager.encryptKey(originalKey);
        const encrypted2 = keyManager.encryptKey(originalKey);
        
        // Due to random IV, the ciphertexts should be different
        expect(encrypted1).not.toBe(encrypted2);
        
        // But both should decrypt to the same original key
        expect(keyManager.decryptKey(encrypted1)).toBe(originalKey);
        expect(keyManager.decryptKey(encrypted2)).toBe(originalKey);
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: ai-rewrite-assistant, Property 7: API key storage round-trip**
   * **Validates: Requirements 4.1**
   * 
   * Keys encrypted with one secret should not decrypt with a different secret.
   */
  it('should fail decryption with wrong secret', () => {
    const keyManager1 = new APIKeyManager('secret-one');
    const keyManager2 = new APIKeyManager('secret-two');

    fc.assert(
      fc.property(apiKeyArb, (originalKey) => {
        // Encrypt with first manager
        const encrypted = keyManager1.encryptKey(originalKey);
        
        // Attempting to decrypt with different secret should throw
        expect(() => keyManager2.decryptKey(encrypted)).toThrow();
      }),
      { numRuns: 100 }
    );
  });
});
