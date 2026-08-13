// Core/TokenReducer/ImportGraph.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.TokenReducer
{
    /// <summary>
    /// Builds and manages import dependency graphs
    /// </summary>
    public class ImportGraph
    {
        private readonly ILogger<ImportGraph> _logger;
        private readonly TokenReducerConfig _config;

        public ImportGraph(ILogger<ImportGraph> logger, TokenReducerConfig config)
        {
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// Build import graph from chunks
        /// </summary>
        public async Task<Dictionary<string, List<string>>> BuildGraphAsync(
            List<CodeChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            var graph = new Dictionary<string, List<string>>();

            foreach (var chunk in chunks)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var fileName = System.IO.Path.GetFileName(chunk.FilePath);
                var imports = chunk.Imports;

                if (!graph.ContainsKey(fileName))
                {
                    graph[fileName] = new List<string>();
                }

                foreach (var import in imports)
                {
                    if (!graph[fileName].Contains(import))
                    {
                        graph[fileName].Add(import);
                    }
                }
            }

            _logger.LogDebug($"Built import graph with {graph.Count} nodes");
            return graph;
        }

        /// <summary>
        /// Find dependencies for a file
        /// </summary>
        public List<string> FindDependencies(
            string filePath,
            Dictionary<string, List<string>> graph)
        {
            var fileName = System.IO.Path.GetFileName(filePath);
            if (graph.TryGetValue(fileName, out var dependencies))
            {
                return dependencies;
            }

            return new List<string>();
        }

        /// <summary>
        /// Find reverse dependencies (files that import this file)
        /// </summary>
        public List<string> FindReverseDependencies(
            string filePath,
            Dictionary<string, List<string>> graph)
        {
            var fileName = System.IO.Path.GetFileName(filePath);
            var reverseDeps = new List<string>();

            foreach (var kvp in graph)
            {
                if (kvp.Value.Contains(fileName))
                {
                    reverseDeps.Add(kvp.Key);
                }
            }

            return reverseDeps;
        }

        /// <summary>
        /// Get import chain for a file (2-hop)
        /// </summary>
        public List<string> GetImportChain(
            string filePath,
            Dictionary<string, List<string>> graph,
            int maxHops = 2)
        {
            var fileName = System.IO.Path.GetFileName(filePath);
            var chain = new List<string> { fileName };
            var visited = new HashSet<string> { fileName };

            GetImportChainRecursive(fileName, graph, chain, visited, 0, maxHops);

            return chain;
        }

        private void GetImportChainRecursive(
            string currentNode,
            Dictionary<string, List<string>> graph,
            List<string> chain,
            HashSet<string> visited,
            int currentHop,
            int maxHops)
        {
            if (currentHop >= maxHops) return;

            if (graph.TryGetValue(currentNode, out var dependencies))
            {
                foreach (var dep in dependencies)
                {
                    if (!visited.Contains(dep))
                    {
                        visited.Add(dep);
                        chain.Add(dep);
                        GetImportChainRecursive(dep, graph, chain, visited, currentHop + 1, maxHops);
                    }
                }
            }
        }

        /// <summary>
        /// Calculate import complexity score
        /// </summary>
        public double CalculateComplexity(Dictionary<string, List<string>> graph)
        {
            if (!graph.Any()) return 0;

            var totalEdges = graph.Values.Sum(v => v.Count);
            var avgEdges = (double)totalEdges / graph.Count;

            return avgEdges;
        }

        /// <summary>
        /// Find circular dependencies
        /// </summary>
        public List<List<string>> FindCircularDependencies(Dictionary<string, List<string>> graph)
        {
            var cycles = new List<List<string>>();
            var visited = new HashSet<string>();
            var path = new List<string>();

            foreach (var node in graph.Keys)
            {
                if (!visited.Contains(node))
                {
                    FindCyclesDFS(node, graph, visited, path, cycles);
                }
            }

            return cycles;
        }

        private void FindCyclesDFS(
            string node,
            Dictionary<string, List<string>> graph,
            HashSet<string> visited,
            List<string> path,
            List<List<string>> cycles)
        {
            if (path.Contains(node))
            {
                var cycle = path.Skip(path.IndexOf(node)).ToList();
                cycles.Add(cycle);
                return;
            }

            if (visited.Contains(node)) return;

            visited.Add(node);
            path.Add(node);

            if (graph.TryGetValue(node, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    FindCyclesDFS(neighbor, graph, visited, path, cycles);
                }
            }

            path.RemoveAt(path.Count - 1);
        }
    }
}