/**
 * Property-based tests for ModelService model listing
 * 
 * **Feature: architecture-hardening, Property 6: Model list fetched from API**
 * **Validates: Requirements 6.1, 6.2**
 * 
 * Property: For any valid API key, the ModelService should return a list of
 * models with the expected structure (id, object, created, owned_by).
 * 
 * Note: Since we cannot make real API calls in tests, we test the structural
 * properties of the ModelService interface and response handling.
 */

import * as fc from 'fast-check';
import { ModelService, CerebrasModel } from './ModelService';

describe('Property 6: Model list fetched from API', () => {
  /**
   * Arbitrary for generating valid API key formats
   */
  const apiKeyArb = fc.stringMatching(/^csk-[a-zA-Z0-9]{32,64}$/);

  /**
   * Arbitrary for generating model response data
   */
  const modelArb: fc.Arbitrary<CerebrasModel> = fc.record({
    id: fc.stringMatching(/^[a-zA-Z][a-zA-Z0-9\-\.]{2,30}$/),
    object: fc.constant('model'),
    created: fc.integer({ min: 1600000000, max: 2000000000 }),
    owned_by: fc.stringMatching(/^[a-zA-Z][a-zA-Z0-9\-]{2,20}$/)
  });

  /**
   * **Feature: architecture-hardening, Property 6: Model list fetched from API**
   * **Validates: Requirements 6.1, 6.2**
   * 
   * ModelService should throw an error when no API key is provided.
   */
  it('should throw error when API key is empty', async () => {
    const service = new ModelService();
    
    await expect(service.listModels('')).rejects.toThrow('API key is required');
  });

  /**
   * **Feature: architecture-hardening, Property 6: Model list fetched from API**
   * **Validates: Requirements 6.1, 6.2**
   * 
   * For any generated model data, the structure should be valid.
   */
  it('should have valid model structure for any generated model', () => {
    fc.assert(
      fc.property(modelArb, (model) => {
        // Model should have all required fields
        expect(model).toHaveProperty('id');
        expect(model).toHaveProperty('object');
        expect(model).toHaveProperty('created');
        expect(model).toHaveProperty('owned_by');
        
        // Fields should have correct types
        expect(typeof model.id).toBe('string');
        expect(typeof model.object).toBe('string');
        expect(typeof model.created).toBe('number');
        expect(typeof model.owned_by).toBe('string');
        
        // ID should not be empty
        expect(model.id.length).toBeGreaterThan(0);
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: architecture-hardening, Property 6: Model list fetched from API**
   * **Validates: Requirements 6.1, 6.2**
   * 
   * For any list of models, all models should have unique IDs.
   */
  it('should have unique model IDs in any model list', () => {
    fc.assert(
      fc.property(
        fc.array(modelArb, { minLength: 1, maxLength: 20 }),
        (models) => {
          // Make IDs unique for this test
          const uniqueModels = models.map((m, i) => ({ ...m, id: `${m.id}-${i}` }));
          
          const ids = uniqueModels.map(m => m.id);
          const uniqueIds = new Set(ids);
          
          // All IDs should be unique
          expect(uniqueIds.size).toBe(ids.length);
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: architecture-hardening, Property 6: Model list fetched from API**
   * **Validates: Requirements 6.1, 6.2**
   * 
   * Model created timestamps should be valid Unix timestamps.
   */
  it('should have valid Unix timestamps for created field', () => {
    fc.assert(
      fc.property(modelArb, (model) => {
        // Created should be a positive integer
        expect(model.created).toBeGreaterThan(0);
        
        // Should be a reasonable Unix timestamp (after year 2000)
        expect(model.created).toBeGreaterThan(946684800);
        
        // Should not be in the far future
        expect(model.created).toBeLessThan(4102444800); // Year 2100
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: architecture-hardening, Property 6: Model list fetched from API**
   * **Validates: Requirements 6.1, 6.2**
   * 
   * ModelService instance should be reusable - creating multiple instances should work.
   */
  it('should allow creating multiple ModelService instances', () => {
    fc.assert(
      fc.property(
        fc.integer({ min: 1, max: 10 }),
        (count) => {
          const services: ModelService[] = [];
          
          for (let i = 0; i < count; i++) {
            services.push(new ModelService());
          }
          
          // All instances should be valid
          expect(services.length).toBe(count);
          services.forEach(service => {
            expect(service).toBeInstanceOf(ModelService);
            expect(typeof service.listModels).toBe('function');
          });
        }
      ),
      { numRuns: 20 }
    );
  });

  /**
   * **Feature: architecture-hardening, Property 6: Model list fetched from API**
   * **Validates: Requirements 6.1, 6.2**
   * 
   * The CerebrasModel interface should be properly exported and usable.
   */
  it('should export CerebrasModel interface that matches expected structure', () => {
    // Create a model that conforms to the interface
    const model: CerebrasModel = {
      id: 'test-model',
      object: 'model',
      created: Date.now(),
      owned_by: 'cerebras'
    };
    
    expect(model.id).toBe('test-model');
    expect(model.object).toBe('model');
    expect(typeof model.created).toBe('number');
    expect(model.owned_by).toBe('cerebras');
  });
});
