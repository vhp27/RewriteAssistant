/**
 * ModelService
 * Service for fetching available AI models from Cerebras API
 * Requirements: 6.1, 6.2
 */

import Cerebras from '@cerebras/cerebras_cloud_sdk';

/**
 * Cerebras model information
 */
export interface CerebrasModel {
  id: string;
  object: string;
  created: number;
  owned_by: string;
}

/**
 * Interface for ModelService
 */
export interface IModelService {
  /**
   * Lists available models from the Cerebras API
   * @param apiKey - The API key to use for authentication
   * @returns Array of available models
   */
  listModels(apiKey: string): Promise<CerebrasModel[]>;
}

/**
 * ModelService implementation
 * Fetches available models from Cerebras API using the SDK
 */
export class ModelService implements IModelService {
  /**
   * Lists available models from the Cerebras API
   * Requirements: 6.1, 6.2
   */
  async listModels(apiKey: string): Promise<CerebrasModel[]> {
    if (!apiKey) {
      throw new Error('API key is required to list models');
    }

    const client = new Cerebras({ apiKey });
    const response = await client.models.list();
    
    // Map the response to our CerebrasModel interface
    return (response.data || []).map((model: { id?: string; object?: string; created?: number; owned_by?: string }) => ({
      id: model.id || '',
      object: model.object || 'model',
      created: model.created || 0,
      owned_by: model.owned_by || 'cerebras'
    }));
  }
}

// Export singleton instance
export const modelService = new ModelService();
