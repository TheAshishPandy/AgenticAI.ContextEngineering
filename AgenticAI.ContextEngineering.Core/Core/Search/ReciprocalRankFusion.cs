// Core/Search/ReciprocalRankFusion.cs
using System;
using System.Collections.Generic;
using System.Linq;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Search
{
    public static class ReciprocalRankFusion
    {
        private const int DEFAULT_K = 60;

        /// <summary>
        /// Fuses multiple ranked result lists using RRF
        /// </summary>
        public static List<SearchResult> FuseResults(
            List<SearchResult> lexicalResults,
            List<SearchResult> semanticResults,
            int topK = 10,
            int k = DEFAULT_K)
        {
            if (lexicalResults == null || !lexicalResults.Any())
                return semanticResults ?? new List<SearchResult>();

            if (semanticResults == null || !semanticResults.Any())
                return lexicalResults ?? new List<SearchResult>();

            // Build rank maps
            var lexicalRankMap = BuildRankMap(lexicalResults);
            var semanticRankMap = BuildRankMap(semanticResults);

            // Get all unique IDs
            var allDocIds = lexicalRankMap.Keys
                .Union(semanticRankMap.Keys)
                .ToHashSet();

            // Calculate RRF scores
            var fusedScores = new Dictionary<string, (SearchResult Result, double Score)>();
            var documents = new Dictionary<string, SearchResult>();

            foreach (var doc in lexicalResults)
                documents[doc.Id] = doc;
            foreach (var doc in semanticResults)
                documents[doc.Id] = doc;

            foreach (var docId in allDocIds)
            {
                double score = 0.0;

                // Lexical contribution
                if (lexicalRankMap.TryGetValue(docId, out var lexicalRank))
                    score += 1.0 / (k + lexicalRank);

                // Semantic contribution
                if (semanticRankMap.TryGetValue(docId, out var semanticRank))
                    score += 1.0 / (k + semanticRank);

                if (documents.TryGetValue(docId, out var result))
                {
                    var fusedResult = new SearchResult
                    {
                        Id = result.Id,
                        Content = result.Content,
                        Title = result.Title,
                        Source = result.Source,
                        Score = score,
                        Metadata = new Dictionary<string, object>(result.Metadata),
                        ScoreBreakdown = new ScoreBreakdown
                        {
                            LexicalScore = lexicalRankMap.ContainsKey(docId) ? 1.0 / (k + lexicalRankMap[docId]) : 0,
                            SemanticScore = semanticRankMap.ContainsKey(docId) ? 1.0 / (k + semanticRankMap[docId]) : 0,
                            CombinedScore = score
                        }
                    };
                    fusedScores[docId] = (fusedResult, score);
                }
            }

            return fusedScores
                .OrderByDescending(x => x.Value.Score)
                .Take(topK)
                .Select(x => x.Value.Result)
                .ToList();
        }

        /// <summary>
        /// Build rank map (1-based ranking)
        /// </summary>
        private static Dictionary<string, int> BuildRankMap(List<SearchResult> results)
        {
            var rankMap = new Dictionary<string, int>();
            for (int i = 0; i < results.Count; i++)
            {
                rankMap[results[i].Id] = i + 1;
            }
            return rankMap;
        }

        /// <summary>
        /// Fuse with weighted scores (alternative to RRF)
        /// </summary>
        public static List<SearchResult> FuseWithWeights(
            List<SearchResult> lexicalResults,
            List<SearchResult> semanticResults,
            double lexicalWeight = 0.4,
            double semanticWeight = 0.6,
            int topK = 10)
        {
            var combined = new Dictionary<string, SearchResult>();

            // Normalize scores
            NormalizeScores(lexicalResults);
            NormalizeScores(semanticResults);

            // Add lexical results
            foreach (var result in lexicalResults)
            {
                if (combined.TryGetValue(result.Id, out var existing))
                {
                    existing.Score += result.Score * lexicalWeight;
                }
                else
                {
                    var newResult = CloneResult(result);
                    newResult.Score = result.Score * lexicalWeight;
                    combined[result.Id] = newResult;
                }
            }

            // Add semantic results
            foreach (var result in semanticResults)
            {
                if (combined.TryGetValue(result.Id, out var existing))
                {
                    existing.Score += result.Score * semanticWeight;
                }
                else
                {
                    var newResult = CloneResult(result);
                    newResult.Score = result.Score * semanticWeight;
                    combined[result.Id] = newResult;
                }
            }

            return combined.Values
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .ToList();
        }

        private static void NormalizeScores(List<SearchResult> results)
        {
            if (!results.Any()) return;

            var maxScore = results.Max(r => r.Score);
            if (maxScore > 0)
            {
                foreach (var result in results)
                {
                    result.Score /= maxScore;
                }
            }
        }

        private static SearchResult CloneResult(SearchResult original)
        {
            return new SearchResult
            {
                Id = original.Id,
                Content = original.Content,
                Title = original.Title,
                Source = original.Source,
                Metadata = new Dictionary<string, object>(original.Metadata)
            };
        }
    }
}