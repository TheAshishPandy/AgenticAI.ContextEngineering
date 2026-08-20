// Core/Search/ReciprocalRankFusion.cs
using System;
using System.Collections.Generic;
using System.Linq;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Search
{
    public static class ReciprocalRankFusion
    {
        public static List<SearchResult> FuseResults(
            List<SearchResult> lexicalResults,
            List<SearchResult> semanticResults,
            int topK = 10,
            int k = 60)
        {
            if (lexicalResults == null) lexicalResults = new List<SearchResult>();
            if (semanticResults == null) semanticResults = new List<SearchResult>();

            if (!lexicalResults.Any() && !semanticResults.Any())
                return new List<SearchResult>();

            if (!lexicalResults.Any())
                return semanticResults.Take(topK).ToList();

            if (!semanticResults.Any())
                return lexicalResults.Take(topK).ToList();

            var scoreMap = new Dictionary<string, (SearchResult Result, double Score)>();

            // ✅ Process lexical results
            for (int rank = 0; rank < lexicalResults.Count; rank++)
            {
                var result = lexicalResults[rank];
                var key = GetResultKey(result);
                var rrfScore = 1.0 / (k + rank + 1);

                if (scoreMap.ContainsKey(key))
                {
                    var existing = scoreMap[key];
                    scoreMap[key] = (existing.Result, existing.Score + rrfScore);
                }
                else
                {
                    scoreMap[key] = (result, rrfScore);
                }
            }

            // ✅ Process semantic results
            for (int rank = 0; rank < semanticResults.Count; rank++)
            {
                var result = semanticResults[rank];
                var key = GetResultKey(result);
                var rrfScore = 1.0 / (k + rank + 1);

                if (scoreMap.ContainsKey(key))
                {
                    var existing = scoreMap[key];
                    scoreMap[key] = (existing.Result, existing.Score + rrfScore);
                }
                else
                {
                    scoreMap[key] = (result, rrfScore);
                }
            }

            // ✅ Return top K results ordered by fused score
            return scoreMap
                .OrderByDescending(x => x.Value.Score)
                .Take(topK)
                .Select(x => x.Value.Result)
                .ToList();
        }

        private static string GetResultKey(SearchResult result)
        {
            // Use content hash as key to deduplicate
            if (!string.IsNullOrEmpty(result.Id))
                return result.Id;

            // Fallback to content hash
            return result.Content?.GetHashCode().ToString() ?? Guid.NewGuid().ToString();
        }
    }
}