/**
 * Property-based tests for CerebrasClient model selection
 * 
 * **Feature: architecture-hardening, Property 5: Selected model is used for API calls**
 * **Validates: Requirements 6.3**
 * 
 * Property: For any model selected by the user, the CerebrasClient should
 * store and return that exact model name for subsequent operations.
 */

import * as fc from 'fast-check';
import { CerebrasClient } from './CerebrasClient';

describe('Property 5: Selected model is used for API calls', () => {
  /**
   * Arbitrary for generating valid model names
   * Model names are typically alphanumeric with dashes and dots
   */
  const modelNameArb = fc.stringMatching(/^[a-zA-Z][a-zA-Z0-9\-\.]{2,50}$/);

  /**
   * **Feature: architecture-hardening, Property 5: Selected model is used for API calls**
   * **Validates: Requirements 6.3**
   * 
   * For any valid model name, setSelectedModel should store it and
   * getSelectedModel should return the exact same value.
   */
  it('should store and return the exact model name that was set', () => {
    fc.assert(
      fc.property(modelNameArb, (modelName) => {
        const client = new CerebrasClient();
        
        client.setSelectedModel(modelName);
        const retrievedModel = client.getSelectedModel();
        
        expect(retrievedModel).toBe(modelName);
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: architecture-hardening, Property 5: Selected model is used for API calls**
   * **Validates: Requirements 6.3**
   * 
   * Setting a model multiple times should always use the last value.
   */
  it('should use the most recently set model', () => {
    fc.assert(
      fc.property(
        fc.array(modelNameArb, { minLength: 2, maxLength: 10 }),
        (modelNames) => {
          const client = new CerebrasClient();
          
          // Set each model in sequence
          for (const modelName of modelNames) {
            client.setSelectedModel(modelName);
          }
          
          // Should return the last model set
          const lastModel = modelNames[modelNames.length - 1];
          expect(client.getSelectedModel()).toBe(lastModel);
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: architecture-hardening, Property 5: Selected model is used for API calls**
   * **Validates: Requirements 6.3**
   * 
   * Empty or whitespace-only model names should not change the current model.
   */
  it('should not change model when given empty or whitespace-only input', () => {
    fc.assert(
      fc.property(modelNameArb, (validModel) => {
        const client = new CerebrasClient();
        
        // Set a valid model first
        client.setSelectedModel(validModel);
        const modelAfterValid = client.getSelectedModel();
        
        // Try to set empty string - should not change
        client.setSelectedModel('');
        expect(client.getSelectedModel()).toBe(modelAfterValid);
        
        // Try to set whitespace - should not change
        client.setSelectedModel('   ');
        expect(client.getSelectedModel()).toBe(modelAfterValid);
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: architecture-hardening, Property 5: Selected model is used for API calls**
   * **Validates: Requirements 6.3**
   * 
   * Model names with leading/trailing whitespace should be trimmed.
   */
  it('should trim whitespace from model names', () => {
    fc.assert(
      fc.property(modelNameArb, (modelName) => {
        const client = new CerebrasClient();
        
        // Set model with extra whitespace
        client.setSelectedModel(`  ${modelName}  `);
        
        // Should return trimmed version
        expect(client.getSelectedModel()).toBe(modelName);
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: architecture-hardening, Property 5: Selected model is used for API calls**
   * **Validates: Requirements 6.3**
   * 
   * Default model should be used when no model is explicitly set.
   */
  it('should use default model when none is set', () => {
    const client = new CerebrasClient();
    
    // Should have a default model
    const defaultModel = client.getSelectedModel();
    expect(defaultModel).toBe('gpt-oss-120b');
  });

  /**
   * **Feature: architecture-hardening, Property 5: Selected model is used for API calls**
   * **Validates: Requirements 6.3**
   * 
   * Different model names should result in different stored values.
   */
  it('should distinguish between different model names', () => {
    fc.assert(
      fc.property(modelNameArb, modelNameArb, (model1, model2) => {
        // Skip if models are the same
        fc.pre(model1 !== model2);
        
        const client1 = new CerebrasClient();
        const client2 = new CerebrasClient();
        
        client1.setSelectedModel(model1);
        client2.setSelectedModel(model2);
        
        expect(client1.getSelectedModel()).toBe(model1);
        expect(client2.getSelectedModel()).toBe(model2);
        expect(client1.getSelectedModel()).not.toBe(client2.getSelectedModel());
      }),
      { numRuns: 100 }
    );
  });
});
