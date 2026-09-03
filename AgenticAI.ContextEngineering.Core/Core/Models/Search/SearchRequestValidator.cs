// AgenticAI.ContextEngineering.Core/Core/Models/Search/SearchRequestValidator.cs
using AgenticAI.ContextEngineering.Core.Exceptions;
using System;
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models.Search
{
    /// <summary>
    /// Validates search requests and parameters for correctness.
    /// Implements validation rules specific to semantic and hybrid search operations.
    /// </summary>
    public static class SearchRequestValidator
    {
        private const int MIN_TOP_RESULTS = 1;
        private const int MAX_TOP_RESULTS = 1000;
        private const double MIN_RELEVANCE_SCORE = 0.0;
        private const double MAX_RELEVANCE_SCORE = 1.0;
        private const int MIN_QUERY_LENGTH = 1;
        private const int MAX_QUERY_LENGTH = 10000;
        private const int MIN_BATCH_SIZE = 1;
        private const int MAX_BATCH_SIZE = 1000;

        /// <summary>
        /// Validates a single search request object.
        /// </summary>
        /// <exception cref="InvalidRequestException">When validation fails.</exception>
        public static void ValidateSearchRequest(SearchRequest request)
        {
            if (request == null)
                throw new InvalidRequestException("Null Search Request", "request", null, "Search request cannot be null");

            // Validate TopResults
            if (request.TopResults < MIN_TOP_RESULTS || request.TopResults > MAX_TOP_RESULTS)
                throw new InvalidRequestException(
                    "Constraint",
                    "TopResults",
                    request.TopResults,
                    $"TopResults must be between {MIN_TOP_RESULTS} and {MAX_TOP_RESULTS}");

            // Validate MinimumRelevanceScore
            if (request.MinimumRelevanceScore < MIN_RELEVANCE_SCORE || request.MinimumRelevanceScore > MAX_RELEVANCE_SCORE)
                throw new InvalidRequestException(
                    "Range",
                    "MinimumRelevanceScore",
                    request.MinimumRelevanceScore,
                    $"MinimumRelevanceScore must be between {MIN_RELEVANCE_SCORE} and {MAX_RELEVANCE_SCORE}");

            // Validate Query if present
            if (!string.IsNullOrEmpty(request.Query))
            {
                if (request.Query.Length < MIN_QUERY_LENGTH || request.Query.Length > MAX_QUERY_LENGTH)
                    throw new InvalidRequestException(
                        "Length",
                        "Query",
                        request.Query.Length,
                        $"Query length must be between {MIN_QUERY_LENGTH} and {MAX_QUERY_LENGTH} characters");

                // Validate query contains non-whitespace
                if (string.IsNullOrWhiteSpace(request.Query))
                    throw new InvalidRequestException(
                        "Format",
                        "Query",
                        request.Query,
                        "Query cannot be empty or whitespace only");
            }

            // Validate Filters if present
            if (request.Filters != null)
            {
                ValidateFilters(request.Filters);
            }
        }

        /// <summary>
        /// Validates a query vector for semantic search.
        /// </summary>
        /// <exception cref="InvalidRequestException">When vector is null or invalid.</exception>
        /// <exception cref="DimensionMismatchException">When dimensions don't match expected.</exception>
        public static void ValidateQueryVector(float[] queryVector, int? expectedDimension = null)
        {
            if (queryVector == null)
                throw new InvalidRequestException("Validation", "queryVector", null, "Query vector cannot be null");

            if (queryVector.Length == 0)
                throw new InvalidRequestException("Validation", "queryVector", queryVector.Length, "Query vector cannot be empty");

            // Check for expected dimension if specified
            if (expectedDimension.HasValue && queryVector.Length != expectedDimension.Value)
                throw new DimensionMismatchException(
                    expectedDimension.Value,
                    queryVector.Length,
                    "queryVector");

            // Check for NaN or Infinity values
            for (int i = 0; i < queryVector.Length; i++)
            {
                if (float.IsNaN(queryVector[i]) || float.IsInfinity(queryVector[i]))
                    throw new InvalidRequestException(
                        "Format",
                        $"queryVector[{i}]",
                        queryVector[i],
                        $"Vector contains invalid value at index {i}");
            }

            // Warn if vector is all zeros (weak signal)
            bool isZeroVector = true;
            for (int i = 0; i < queryVector.Length; i++)
            {
                if (Math.Abs(queryVector[i]) > 0.00001)  // Small epsilon for floating point
                {
                    isZeroVector = false;
                    break;
                }
            }

            if (isZeroVector)
                throw new InvalidRequestException(
                    "Validation",
                    "queryVector",
                    queryVector,
                    "Query vector is all zeros (no useful embedding signal)");
        }

        /// <summary>
        /// Validates document vectors for consistency.
        /// </summary>
        /// <exception cref="InvalidRequestException">When vectors are null or inconsistent.</exception>
        /// <exception cref="DimensionMismatchException">When vectors have inconsistent dimensions.</exception>
        public static void ValidateDocumentVectors(List<float[]> vectors, int? expectedDimension = null)
        {
            if (vectors == null)
                throw new InvalidRequestException("Validation", "documentVectors", null, "Document vectors collection cannot be null");

            if (vectors.Count == 0)
                throw new InvalidRequestException("Validation", "documentVectors", vectors.Count, "Document vectors collection cannot be empty");

            int? firstDimension = null;

            for (int i = 0; i < vectors.Count; i++)
            {
                var vector = vectors[i];

                // Skip null vectors with warning
                if (vector == null)
                    continue;

                if (vector.Length == 0)
                    throw new InvalidRequestException(
                        "Format",
                        $"documentVectors[{i}]",
                        vector.Length,
                        $"Document vector at index {i} is empty");

                // Check for NaN or Infinity
                for (int j = 0; j < vector.Length; j++)
                {
                    if (float.IsNaN(vector[j]) || float.IsInfinity(vector[j]))
                        throw new InvalidRequestException(
                            "Format",
                            $"documentVectors[{i}][{j}]",
                            vector[j],
                            $"Document vector contains invalid value");
                }

                // Establish expected dimension from first vector
                if (!firstDimension.HasValue)
                {
                    firstDimension = vector.Length;

                    // Validate against expected dimension if provided
                    if (expectedDimension.HasValue && vector.Length != expectedDimension.Value)
                        throw new DimensionMismatchException(
                            expectedDimension.Value,
                            vector.Length,
                            $"documentVectors[0]");
                }
                else if (vector.Length != firstDimension.Value)
                {
                    throw new DimensionMismatchException(
                        firstDimension.Value,
                        vector.Length,
                        $"documentVectors[{i}]");
                }
            }
        }

        /// <summary>
        /// Validates batch parameters for batch operations.
        /// </summary>
        /// <exception cref="InvalidRequestException">When parameters are invalid.</exception>
        public static void ValidateBatchParameters(int batchSize, int totalItems)
        {
            if (batchSize < MIN_BATCH_SIZE || batchSize > MAX_BATCH_SIZE)
                throw new InvalidRequestException(
                    "Range",
                    "batchSize",
                    batchSize,
                    $"Batch size must be between {MIN_BATCH_SIZE} and {MAX_BATCH_SIZE}");

            if (totalItems <= 0)
                throw new InvalidRequestException(
                    "Validation",
                    "totalItems",
                    totalItems,
                    "Total items must be greater than 0");
        }

        /// <summary>
        /// Validates search options configuration.
        /// </summary>
        /// <exception cref="InvalidRequestException">When configuration is invalid.</exception>
        public static void ValidateSearchOptions(SearchOptions options)
        {
            if (options == null)
                throw new InvalidRequestException("Null Search Options", "options", null, "SearchOptions cannot be null");

            // Validate Top K parameters
            if (options.DefaultTopK <= 0)
                throw new InvalidRequestException(
                    "Range",
                    "DefaultTopK",
                    options.DefaultTopK,
                    "DefaultTopK must be greater than 0");

            if (options.MaxResults <= 0)
                throw new InvalidRequestException(
                    "Range",
                    "MaxResults",
                    options.MaxResults,
                    "MaxResults must be greater than 0");

            if (options.DefaultTopK > options.MaxResults)
                throw new InvalidRequestException(
                    "Constraint",
                    "DefaultTopK",
                    options.DefaultTopK,
                    $"DefaultTopK ({options.DefaultTopK}) cannot exceed MaxResults ({options.MaxResults})");

            // Validate relevance score
            if (options.MinimumRelevanceScore < 0 || options.MinimumRelevanceScore > 1)
                throw new InvalidRequestException(
                    "Range",
                    "MinimumRelevanceScore",
                    options.MinimumRelevanceScore,
                    "MinimumRelevanceScore must be between 0 and 1");

            // Validate weights sum to reasonable value for hybrid search
            if (options.UseHybridSearch)
            {
                double weightSum = options.SemanticWeight + options.LexicalWeight;
                if (Math.Abs(weightSum - 1.0) > 0.01)  // Allow 1% tolerance
                    throw new InvalidRequestException(
                        "Constraint",
                        "Weights",
                        weightSum,
                        $"SemanticWeight ({options.SemanticWeight}) + LexicalWeight ({options.LexicalWeight}) should sum to 1.0");
            }

            // Validate BM25 parameters
            if (options.UseLexicalSearch)
            {
                if (options.BM25K1 <= 0)
                    throw new InvalidRequestException(
                        "Range",
                        "BM25K1",
                        options.BM25K1,
                        "BM25K1 must be greater than 0");

                if (options.BM25B < 0 || options.BM25B > 1)
                    throw new InvalidRequestException(
                        "Range",
                        "BM25B",
                        options.BM25B,
                        "BM25B must be between 0 and 1");

                if (options.BM25MinDocumentFrequency < 0)
                    throw new InvalidRequestException(
                        "Range",
                        "BM25MinDocumentFrequency",
                        options.BM25MinDocumentFrequency,
                        "BM25MinDocumentFrequency must be non-negative");

                if (options.BM25MaxDocumentFrequency < 0 || options.BM25MaxDocumentFrequency > 1)
                    throw new InvalidRequestException(
                        "Range",
                        "BM25MaxDocumentFrequency",
                        options.BM25MaxDocumentFrequency,
                        "BM25MaxDocumentFrequency must be between 0 and 1");
            }

            // Validate cache parameters
            if (options.CacheExpirationHours < 0)
                throw new InvalidRequestException(
                    "Range",
                    "CacheExpirationHours",
                    options.CacheExpirationHours,
                    "CacheExpirationHours must be non-negative");

            // Validate query expansion parameters
            if (options.MaxQueryVariants <= 0)
                throw new InvalidRequestException(
                    "Range",
                    "MaxQueryVariants",
                    options.MaxQueryVariants,
                    "MaxQueryVariants must be greater than 0");
        }

        /// <summary>
        /// Validates filter dictionary.
        /// </summary>
        private static void ValidateFilters(Dictionary<string, object> filters)
        {
            if (filters == null)
                return;

            foreach (var kvp in filters)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    throw new InvalidRequestException(
                        "Format",
                        "FilterKey",
                        kvp.Key,
                        "Filter keys cannot be null or empty");

                // Validate filter value types are appropriate
                var value = kvp.Value;
                if (value != null)
                {
                    var valueType = value.GetType();
                    if (valueType != typeof(string) && valueType != typeof(int) && valueType != typeof(double)
                        && valueType != typeof(bool) && valueType != typeof(DateTime))
                    {
                        throw new InvalidRequestException(
                            "Type",
                            kvp.Key,
                            valueType.Name,
                            $"Filter value type '{valueType.Name}' is not supported");
                    }
                }
            }
        }

        /// <summary>
        /// Gets a result indicating if validation succeeded (returns null if valid, exception if not).
        /// Useful for try-validate patterns.
        /// </summary>
        public static InvalidRequestException TryValidateSearchRequest(SearchRequest request)
        {
            try
            {
                ValidateSearchRequest(request);
                return null;  // Valid
            }
            catch (InvalidRequestException ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Gets a result indicating if vector validation succeeded.
        /// </summary>
        public static DimensionMismatchException TryValidateQueryVector(float[] queryVector, int? expectedDimension = null)
        {
            try
            {
                ValidateQueryVector(queryVector, expectedDimension);
                return null;  // Valid
            }
            catch (DimensionMismatchException ex)
            {
                return ex;
            }
            catch
            {
                return null;  // Other exception type
            }
        }
    }
}
