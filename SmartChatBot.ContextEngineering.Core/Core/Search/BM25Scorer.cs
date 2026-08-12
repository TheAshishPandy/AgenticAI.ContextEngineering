// Core/Search/BM25Scorer.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartChatBot.ContextEngineering.Core.Search
{
    /// <summary>
    /// BM25 (Best Matching 25) scoring algorithm
    /// </summary>
    public class BM25Scorer
    {
        private readonly double _k1;
        private readonly double _b;
        private readonly Dictionary<string, int> _documentFrequencies;
        private readonly int _totalDocuments;
        private readonly double _averageDocumentLength;

        public BM25Scorer(
            Dictionary<string, int> documentFrequencies,
            int totalDocuments,
            double averageDocumentLength,
            double k1 = 1.2,
            double b = 0.75)
        {
            _documentFrequencies = documentFrequencies;
            _totalDocuments = totalDocuments;
            _averageDocumentLength = averageDocumentLength;
            _k1 = k1;
            _b = b;
        }

        /// <summary>
        /// Calculate BM25 score for a document
        /// </summary>
        public double Score(Dictionary<string, int> termFrequencies, int documentLength)
        {
            double score = 0.0;
            var terms = termFrequencies.Keys;

            foreach (var term in terms)
            {
                var tf = termFrequencies[term];
                var df = _documentFrequencies.GetValueOrDefault(term, 0);

                if (df == 0) continue;

                // IDF component
                var idf = Math.Log((_totalDocuments - df + 0.5) / (df + 0.5) + 1.0);

                // TF component with BM25 saturation
                var numerator = tf * (_k1 + 1);
                var denominator = tf + _k1 * (1 - _b + _b * (documentLength / _averageDocumentLength));

                score += idf * (numerator / denominator);
            }

            return score;
        }

        /// <summary>
        /// Calculate term importance (IDF)
        /// </summary>
        public double GetTermImportance(string term)
        {
            var df = _documentFrequencies.GetValueOrDefault(term, 0);
            if (df == 0) return 0;

            return Math.Log((_totalDocuments - df + 0.5) / (df + 0.5) + 1.0);
        }

        /// <summary>
        /// Get top scoring terms for a document
        /// </summary>
        public Dictionary<string, double> GetTermScores(Dictionary<string, int> termFrequencies, int documentLength)
        {
            var scores = new Dictionary<string, double>();
            var terms = termFrequencies.Keys;

            foreach (var term in terms)
            {
                var tf = termFrequencies[term];
                var df = _documentFrequencies.GetValueOrDefault(term, 0);

                if (df == 0) continue;

                var idf = Math.Log((_totalDocuments - df + 0.5) / (df + 0.5) + 1.0);
                var numerator = tf * (_k1 + 1);
                var denominator = tf + _k1 * (1 - _b + _b * (documentLength / _averageDocumentLength));

                scores[term] = idf * (numerator / denominator);
            }

            return scores;
        }
    }
}