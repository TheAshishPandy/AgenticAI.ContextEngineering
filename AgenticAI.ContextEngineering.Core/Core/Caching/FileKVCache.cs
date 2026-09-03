// AgenticAI.ContextEngineering.Core/Caching/FileKVCache.cs
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    public class FileKVCache : IKVCache
    {
        private readonly string _filePath;
        private readonly ILogger<FileKVCache>? _logger;
        private Dictionary<string, (object Value, DateTime Expiry)> _cache;

        public string Name => "File";
        public int Count => _cache.Count;
        public long Size => 0;

        public FileKVCache(string filePath, ILogger<FileKVCache>? logger = null)
        {
            _filePath = filePath;
            _logger = logger;
            _cache = LoadCache();
        }

        public Task<T> GetAsync<T>(string key) where T : class
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.Expiry > DateTime.UtcNow)
                {
                    return Task.FromResult(entry.Value as T);
                }
                _cache.Remove(key);
                SaveCache();
            }
            return Task.FromResult<T>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            var expiry = expiration.HasValue
                ? DateTime.UtcNow.Add(expiration.Value)
                : DateTime.UtcNow.AddDays(1);

            _cache[key] = (value, expiry);
            SaveCache();
            return Task.CompletedTask;
        }

        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var result = GetAsync<T>(key).Result;
            if (result != null)
                return Task.FromResult(result);

            var value = factory().Result;
            SetAsync(key, value, expiration).Wait();
            return Task.FromResult(value);
        }

        public Task<bool> ExistsAsync(string key)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.Expiry > DateTime.UtcNow)
                    return Task.FromResult(true);
                _cache.Remove(key);
                SaveCache();
            }
            return Task.FromResult(false);
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            SaveCache();
            return Task.CompletedTask;
        }

        public Task RemoveByPatternAsync(string pattern)
        {
            var keysToRemove = new List<string>();
            foreach (var key in _cache.Keys)
            {
                if (key.Contains(pattern))
                    keysToRemove.Add(key);
            }
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
            SaveCache();
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            _cache.Clear();
            SaveCache();
            return Task.CompletedTask;
        }

        public Task<List<string>> GetKeysAsync()
        {
            return Task.FromResult(new List<string>(_cache.Keys));
        }

        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                CacheName = "File",
                TotalItems = _cache.Count,
                LastUpdated = DateTime.UtcNow
            };
        }

        private Dictionary<string, (object Value, DateTime Expiry)> LoadCache()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var data = JsonSerializer.Deserialize<Dictionary<string, FileCacheEntry>>(json, options);
                    if (data != null)
                    {
                        var result = new Dictionary<string, (object Value, DateTime Expiry)>();
                        foreach (var kvp in data)
                        {
                            result[kvp.Key] = (kvp.Value.Value, kvp.Value.Expiry);
                        }
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load cache from file");
                }
            }
            return new Dictionary<string, (object Value, DateTime Expiry)>();
        }

        private void SaveCache()
        {
            try
            {
                var data = new Dictionary<string, FileCacheEntry>();
                foreach (var kvp in _cache)
                {
                    data[kvp.Key] = new FileCacheEntry
                    {
                        Value = kvp.Value.Value,
                        Expiry = kvp.Value.Expiry
                    };
                }
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save cache to file");
            }
        }

        private class FileCacheEntry
        {
            public object Value { get; set; }
            public DateTime Expiry { get; set; }
        }
    }
}