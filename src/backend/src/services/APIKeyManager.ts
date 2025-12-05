/**
 * APIKeyManager Service
 * Manages API keys with encryption and automatic fallback
 * Requirements: 4.1, 4.2
 */

import * as crypto from 'crypto';

/**
 * Key status tracking
 */
export enum KeyStatus {
  Active = 'active',
  Failed = 'failed',
  NotSet = 'not_set'
}

/**
 * Interface for APIKeyManager
 */
export interface IAPIKeyManager {
  getPrimaryKey(): string | null;
  getFallbackKey(): string | null;
  setPrimaryKey(key: string): void;
  setFallbackKey(key: string): void;
  markKeyFailed(isPrimary: boolean): void;
  getActiveKey(): string | null;
  resetKeyStatus(): void;
  getPrimaryKeyStatus(): KeyStatus;
  getFallbackKeyStatus(): KeyStatus;
  encryptKey(key: string): string;
  decryptKey(encryptedKey: string): string;
}

/**
 * Encryption configuration
 */
const ENCRYPTION_CONFIG = {
  algorithm: 'aes-256-gcm' as const,
  keyLength: 32,
  ivLength: 16,
  tagLength: 16,
  saltLength: 16
};

/**
 * APIKeyManager implementation
 * Provides secure storage and automatic fallback for API keys
 */
export class APIKeyManager implements IAPIKeyManager {
  private primaryKey: string | null = null;
  private fallbackKey: string | null = null;
  private primaryKeyStatus: KeyStatus = KeyStatus.NotSet;
  private fallbackKeyStatus: KeyStatus = KeyStatus.NotSet;
  private encryptionKey: Buffer;

  constructor(encryptionSecret?: string) {
    // Derive encryption key from secret or use machine-specific default
    const secret = encryptionSecret || this.getMachineSecret();
    this.encryptionKey = crypto.scryptSync(secret, 'rewrite-assistant-salt', ENCRYPTION_CONFIG.keyLength);
  }

  /**
   * Gets a machine-specific secret for encryption
   */
  private getMachineSecret(): string {
    // Use environment variable or fallback to a default
    return process.env.REWRITE_ASSISTANT_SECRET || 'default-encryption-secret-change-in-production';
  }

  /**
   * Encrypts an API key for secure storage
   * @param key - The plaintext API key
   * @returns Base64-encoded encrypted key with IV and auth tag
   */
  encryptKey(key: string): string {
    const iv = crypto.randomBytes(ENCRYPTION_CONFIG.ivLength);
    const cipher = crypto.createCipheriv(
      ENCRYPTION_CONFIG.algorithm,
      this.encryptionKey,
      iv
    );
    
    let encrypted = cipher.update(key, 'utf8', 'hex');
    encrypted += cipher.final('hex');
    const authTag = cipher.getAuthTag();
    
    // Combine IV + authTag + encrypted data
    const combined = Buffer.concat([
      iv,
      authTag,
      Buffer.from(encrypted, 'hex')
    ]);
    
    return combined.toString('base64');
  }

  /**
   * Decrypts an encrypted API key
   * @param encryptedKey - Base64-encoded encrypted key
   * @returns The plaintext API key
   */
  decryptKey(encryptedKey: string): string {
    const combined = Buffer.from(encryptedKey, 'base64');
    
    const iv = combined.subarray(0, ENCRYPTION_CONFIG.ivLength);
    const authTag = combined.subarray(
      ENCRYPTION_CONFIG.ivLength,
      ENCRYPTION_CONFIG.ivLength + ENCRYPTION_CONFIG.tagLength
    );
    const encrypted = combined.subarray(
      ENCRYPTION_CONFIG.ivLength + ENCRYPTION_CONFIG.tagLength
    );
    
    const decipher = crypto.createDecipheriv(
      ENCRYPTION_CONFIG.algorithm,
      this.encryptionKey,
      iv
    );
    decipher.setAuthTag(authTag);
    
    let decrypted = decipher.update(encrypted.toString('hex'), 'hex', 'utf8');
    decrypted += decipher.final('utf8');
    
    return decrypted;
  }

  /**
   * Gets the primary API key
   */
  getPrimaryKey(): string | null {
    return this.primaryKey;
  }

  /**
   * Gets the fallback API key
   */
  getFallbackKey(): string | null {
    return this.fallbackKey;
  }

  /**
   * Sets the primary API key
   * @param key - The API key to set
   */
  setPrimaryKey(key: string): void {
    this.primaryKey = key;
    this.primaryKeyStatus = KeyStatus.Active;
  }

  /**
   * Sets the fallback API key
   * @param key - The API key to set
   */
  setFallbackKey(key: string): void {
    this.fallbackKey = key;
    this.fallbackKeyStatus = KeyStatus.Active;
  }

  /**
   * Marks a key as failed (quota exceeded, rate limited, etc.)
   * @param isPrimary - Whether to mark the primary or fallback key
   */
  markKeyFailed(isPrimary: boolean): void {
    if (isPrimary) {
      this.primaryKeyStatus = KeyStatus.Failed;
    } else {
      this.fallbackKeyStatus = KeyStatus.Failed;
    }
  }

  /**
   * Gets the currently active API key
   * Returns primary if active, otherwise fallback if active
   * @returns The active API key or null if none available
   */
  getActiveKey(): string | null {
    if (this.primaryKeyStatus === KeyStatus.Active && this.primaryKey) {
      return this.primaryKey;
    }
    if (this.fallbackKeyStatus === KeyStatus.Active && this.fallbackKey) {
      return this.fallbackKey;
    }
    return null;
  }

  /**
   * Resets all key statuses to active (if keys are set)
   */
  resetKeyStatus(): void {
    if (this.primaryKey) {
      this.primaryKeyStatus = KeyStatus.Active;
    }
    if (this.fallbackKey) {
      this.fallbackKeyStatus = KeyStatus.Active;
    }
  }

  /**
   * Gets the status of the primary key
   */
  getPrimaryKeyStatus(): KeyStatus {
    return this.primaryKeyStatus;
  }

  /**
   * Gets the status of the fallback key
   */
  getFallbackKeyStatus(): KeyStatus {
    return this.fallbackKeyStatus;
  }
}

// Export singleton instance
export const apiKeyManager = new APIKeyManager();
