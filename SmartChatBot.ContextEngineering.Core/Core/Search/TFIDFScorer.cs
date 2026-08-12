// Core/Search/TFIDFScorer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmartChatBot.ContextEngineering.Core.Search
{
    /// <summary>
    /// TF-IDF (Term Frequency-Inverse Document Frequency) scoring algorithm
    /// </summary>
    public class TFIDFScorer
    {
        private readonly Dictionary<string, int> _documentFrequencies;
        private int _totalDocuments;  // ✅ REMOVED 'readonly' - now mutable
        private readonly double _minDocumentFrequency;
        private readonly double _maxDocumentFrequency;

        // Cache for IDF values to improve performance
        private readonly Dictionary<string, double> _idfCache;

        public TFIDFScorer(
            Dictionary<string, int> documentFrequencies,
            int totalDocuments,
            double minDocumentFrequency = 1,
            double maxDocumentFrequency = 0.8)
        {
            _documentFrequencies = documentFrequencies ?? new Dictionary<string, int>();
            _totalDocuments = totalDocuments;
            _minDocumentFrequency = minDocumentFrequency;
            _maxDocumentFrequency = maxDocumentFrequency;
            _idfCache = new Dictionary<string, double>();

            // Pre-calculate IDF values
            PreCalculateIDF();
        }

        /// <summary>
        /// Pre-calculate IDF values for all terms
        /// </summary>
        private void PreCalculateIDF()
        {
            foreach (var term in _documentFrequencies.Keys)
            {
                _idfCache[term] = CalculateIDF(term);
            }
        }

        /// <summary>
        /// Calculate TF-IDF score for a document
        /// </summary>
        public double Score(Dictionary<string, int> termFrequencies)
        {
            if (termFrequencies == null || !termFrequencies.Any())
                return 0.0;

            double score = 0.0;
            double documentLength = termFrequencies.Values.Sum();

            foreach (var kvp in termFrequencies)
            {
                var term = kvp.Key;
                var tf = kvp.Value;

                // Get IDF from cache or calculate
                if (!_idfCache.TryGetValue(term, out var idf))
                {
                    idf = CalculateIDF(term);
                    _idfCache[term] = idf;
                }

                // TF-IDF = TF * IDF
                // Using log normalization for TF
                var tfNormalized = 1 + Math.Log(tf);
                score += tfNormalized * idf;
            }

            // Normalize by document length
            if (documentLength > 0)
            {
                score /= documentLength;
            }

            return score;
        }

        /// <summary>
        /// Calculate TF-IDF score with BM25-style length normalization
        /// </summary>
        public double ScoreWithLengthNormalization(
            Dictionary<string, int> termFrequencies,
            int documentLength,
            double averageDocumentLength,
            double k1 = 1.2,
            double b = 0.75)
        {
            if (termFrequencies == null || !termFrequencies.Any() || documentLength == 0)
                return 0.0;

            double score = 0.0;

            foreach (var kvp in termFrequencies)
            {
                var term = kvp.Key;
                var tf = kvp.Value;

                if (!_idfCache.TryGetValue(term, out var idf))
                {
                    idf = CalculateIDF(term);
                    _idfCache[term] = idf;
                }

                // BM25-style TF normalization
                var numerator = tf * (k1 + 1);
                var denominator = tf + k1 * (1 - b + b * (documentLength / averageDocumentLength));
                var tfNormalized = numerator / denominator;

                score += tfNormalized * idf;
            }

            return score;
        }

        /// <summary>
        /// Calculate TF-IDF scores for all terms in a document
        /// </summary>
        public Dictionary<string, double> GetTermScores(Dictionary<string, int> termFrequencies)
        {
            var scores = new Dictionary<string, double>();

            foreach (var kvp in termFrequencies)
            {
                var term = kvp.Key;
                var tf = kvp.Value;

                if (!_idfCache.TryGetValue(term, out var idf))
                {
                    idf = CalculateIDF(term);
                    _idfCache[term] = idf;
                }

                var tfNormalized = 1 + Math.Log(tf);
                scores[term] = tfNormalized * idf;
            }

            return scores;
        }

        /// <summary>
        /// Calculate TF-IDF for a single term
        /// </summary>
        public double ScoreTerm(string term, int termFrequency)
        {
            if (string.IsNullOrEmpty(term) || termFrequency == 0)
                return 0.0;

            if (!_idfCache.TryGetValue(term, out var idf))
            {
                idf = CalculateIDF(term);
                _idfCache[term] = idf;
            }

            var tfNormalized = 1 + Math.Log(termFrequency);
            return tfNormalized * idf;
        }

        /// <summary>
        /// Calculate Inverse Document Frequency (IDF) for a term
        /// </summary>
        private double CalculateIDF(string term)
        {
            if (!_documentFrequencies.TryGetValue(term, out var documentFrequency))
                return 0.0;

            // Skip terms that appear in too many or too few documents
            var documentRatio = (double)documentFrequency / _totalDocuments;

            if (documentRatio > _maxDocumentFrequency || documentFrequency < _minDocumentFrequency)
                return 0.0;

            // Standard IDF formula: log(N / df)
            // Using smoothed version to avoid division by zero
            return Math.Log((_totalDocuments + 1.0) / (documentFrequency + 1.0));
        }

        /// <summary>
        /// Get document frequency for a term
        /// </summary>
        public int GetDocumentFrequency(string term)
        {
            return _documentFrequencies.GetValueOrDefault(term, 0);
        }

        /// <summary>
        /// Get IDF value for a term (from cache)
        /// </summary>
        public double GetIDF(string term)
        {
            return _idfCache.GetValueOrDefault(term, 0.0);
        }

        /// <summary>
        /// Get top scoring terms
        /// </summary>
        public List<(string Term, double Score)> GetTopTerms(
            Dictionary<string, int> termFrequencies,
            int topN = 10)
        {
            var scores = GetTermScores(termFrequencies);

            return scores
                .OrderByDescending(kvp => kvp.Value)
                .Take(topN)
                .Select(kvp => (kvp.Key, kvp.Value))
                .ToList();
        }

        /// <summary>
        /// Calculate TF-IDF similarity between two documents
        /// </summary>
        public double CalculateSimilarity(
            Dictionary<string, int> tf1,
            Dictionary<string, int> tf2)
        {
            if (tf1 == null || !tf1.Any() || tf2 == null || !tf2.Any())
                return 0.0;

            // Get all terms
            var allTerms = tf1.Keys.Union(tf2.Keys).ToList();

            // Build TF-IDF vectors
            var vector1 = new List<double>();
            var vector2 = new List<double>();

            foreach (var term in allTerms)
            {
                var tf1Val = tf1.GetValueOrDefault(term, 0);
                var tf2Val = tf2.GetValueOrDefault(term, 0);

                var idf = GetIDF(term);

                vector1.Add(tf1Val * idf);
                vector2.Add(tf2Val * idf);
            }

            // Calculate cosine similarity
            return CosineSimilarity(vector1, vector2);
        }

        /// <summary>
        /// Calculate cosine similarity between two vectors
        /// </summary>
        private double CosineSimilarity(List<double> v1, List<double> v2)
        {
            if (v1.Count != v2.Count || v1.Count == 0)
                return 0.0;

            double dotProduct = 0.0;
            double norm1 = 0.0;
            double norm2 = 0.0;

            for (int i = 0; i < v1.Count; i++)
            {
                dotProduct += v1[i] * v2[i];
                norm1 += v1[i] * v1[i];
                norm2 += v2[i] * v2[i];
            }

            if (norm1 == 0 || norm2 == 0)
                return 0.0;

            return dotProduct / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
        }

        /// <summary>
        /// Build TF-IDF vector for a document
        /// </summary>
        public Dictionary<string, double> BuildTFIDFVector(Dictionary<string, int> termFrequencies)
        {
            var vector = new Dictionary<string, double>();

            foreach (var kvp in termFrequencies)
            {
                var term = kvp.Key;
                var tf = kvp.Value;

                if (!_idfCache.TryGetValue(term, out var idf))
                {
                    idf = CalculateIDF(term);
                    _idfCache[term] = idf;
                }

                vector[term] = (1 + Math.Log(tf)) * idf;
            }

            return vector;
        }

        /// <summary>
        /// Tokenize text into terms
        /// </summary>
        public List<string> Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var cleaned = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9\s]", "");
            return cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 2)
                .ToList();
        }

        /// <summary>
        /// Build term frequencies from text
        /// </summary>
        public Dictionary<string, int> BuildTermFrequencies(string text)
        {
            var tokens = Tokenize(text);
            var tf = new Dictionary<string, int>();

            foreach (var token in tokens)
            {
                tf[token] = tf.GetValueOrDefault(token, 0) + 1;
            }

            return tf;
        }

        /// <summary>
        /// Update document frequencies with new documents
        /// </summary>
        public void UpdateDocumentFrequencies(List<Dictionary<string, int>> documentTFs)
        {
            if (documentTFs == null || !documentTFs.Any())
                return;

            var newDF = new Dictionary<string, int>();

            foreach (var tf in documentTFs)
            {
                foreach (var term in tf.Keys)
                {
                    newDF[term] = newDF.GetValueOrDefault(term, 0) + 1;
                }
            }

            // Merge with existing
            foreach (var kvp in newDF)
            {
                _documentFrequencies[kvp.Key] = _documentFrequencies.GetValueOrDefault(kvp.Key, 0) + kvp.Value;
            }

            // ✅ FIX: Update total documents (no 'readonly' issue now)
            _totalDocuments += documentTFs.Count;

            // Recalculate IDF cache
            PreCalculateIDF();
        }

        /// <summary>
        /// Get statistics about the TF-IDF model
        /// </summary>
        public TFIDFStatistics GetStatistics()
        {
            return new TFIDFStatistics
            {
                TotalDocuments = _totalDocuments,
                UniqueTerms = _documentFrequencies.Count,
                AverageDocumentFrequency = _documentFrequencies.Values.Any() ? _documentFrequencies.Values.Average() : 0,
                MaxDocumentFrequency = _documentFrequencies.Values.Any() ? _documentFrequencies.Values.Max() : 0,
                MinDocumentFrequency = _documentFrequencies.Values.Any() ? _documentFrequencies.Values.Min() : 0,
                VocabularySize = _documentFrequencies.Count,
                TermsWithHighIDF = _idfCache.Count(kvp => kvp.Value > 5.0),
                TermsWithLowIDF = _idfCache.Count(kvp => kvp.Value < 1.0)
            };
        }
    }

    /// <summary>
    /// TF-IDF statistics
    /// </summary>
    public class TFIDFStatistics
    {
        public int TotalDocuments { get; set; }
        public int UniqueTerms { get; set; }
        public double AverageDocumentFrequency { get; set; }
        public int MaxDocumentFrequency { get; set; }
        public int MinDocumentFrequency { get; set; }
        public int VocabularySize { get; set; }
        public int TermsWithHighIDF { get; set; }
        public int TermsWithLowIDF { get; set; }
    }
}