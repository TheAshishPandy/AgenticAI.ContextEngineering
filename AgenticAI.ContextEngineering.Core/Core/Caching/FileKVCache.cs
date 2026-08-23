using AgenticAI.ContextEngineering.Core.Core.Models.Caching;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    public class FileKVCache : IKVCache
    {
        private readonly ILogger<FileKVCache> _logger;
        private readonly string _cacheDirectory;
        private readonly ConcurrentDictionary<string, CacheItemMetadata> _metadata = new();
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly object _statsLock = new object();
        private CacheStatistics _statistics = new() { CacheName = "File" };

        public string Name => "File";
        public int Count => _metadata.Count;
        public long Size => _metadata.Values.Sum(m => m.SizeEstimate);

        public FileKVCache(ILogger<FileKVCache> logger, string cacheDirectory = "Cache/KV")
        {
            _logger = logger;
            _cacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cacheDirectory);
            if (!Directory.Exists(_cacheDirectory)) Directory.CreateDirectory(_cacheDirectory);
            LoadMetadata();
        }

        private string GetFilePath(string key)
        {
            var safeKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key))
                .Replace("/", "_").Replace("=", "").Replace("+", "-");
            return Path.Combine(_cacheDirectory, $"{safeKey}.cache");
        }

        private string GetMetaPath(string key) => GetFilePath(key) + ".meta";

        private void LoadMetadata()
        {
            try
            {
                var metaFiles = Directory.GetFiles(_cacheDirectory, "*.cache.meta");
                foreach (var metaFile in metaFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(metaFile);
                        var meta = JsonSerializer.Deserialize<CacheItemMetadata>(json);
                        if (meta != null && meta.Expiry > DateTime.UtcNow)
                        {
                            var key = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(metaFile));
                            _metadata[key] = meta;
                        }
                        else
                        {
                            File.Delete(metaFile);
                            var cacheFile = metaFile.Replace(".meta", "");
                            if (File.Exists(cacheFile)) File.Delete(cacheFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to load metadata: {ex.Message}");
                    }
                }
                _logger.LogInformation($"📊 Loaded {_metadata.Count} file cache items");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load file cache metadata");
            }
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            try
            {
                if (!_metadata.TryGetValue(key, out var meta) || meta.Expiry < DateTime.UtcNow)
                {
                    lock (_statsLock) _statistics.Misses++;
                    return null;
                }

                var filePath = GetFilePath(key);
                if (!File.Exists(filePath))
                {
                    _metadata.TryRemove(key, out _);
                    lock (_statsLock) _statistics.Misses++;
                    return null;
                }

                await _fileLock.WaitAsync();
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var value = JsonSerializer.Deserialize<T>(json);

                    if (value != null)
                    {
                        lock (_statsLock) _statistics.Hits++;
                        meta.AccessCount++;
                        meta.LastAccessed = DateTime.UtcNow;
                        return value;
                    }
                }
                finally { _fileLock.Release(); }

                lock (_statsLock) _statistics.Misses++;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"FileCache Get error: {key}");
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                if (value == null) return;

                var ttl = expiration ?? TimeSpan.FromDays(7);
                var filePath = GetFilePath(key);
                var json = JsonSerializer.Serialize(value);

                await _fileLock.WaitAsync();
                try
                {
                    await File.WriteAllTextAsync(filePath, json);

                    var meta = new CacheItemMetadata
                    {
                        Created = DateTime.UtcNow,
                        LastAccessed = DateTime.UtcNow,
                        AccessCount = 1,
                        SizeEstimate = json.Length,
                        Expiry = DateTime.UtcNow.Add(ttl)
                    };

                    var metaPath = GetMetaPath(key);
                    await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(meta));
                    _metadata[key] = meta;

                    lock (_statsLock)
                    {
                        _statistics.TotalItems = _metadata.Count;
                        _statistics.TotalSizeBytes = Size;
                    }
                }
                finally { _fileLock.Release(); }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"FileCache Set error: {key}");
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var value = await GetAsync<T>(key);
            if (value != null) return value;

            value = await factory();
            if (value != null) await SetAsync(key, value, expiration);
            return value;
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await Task.FromResult(_metadata.TryGetValue(key, out var meta) &&
                meta.Expiry > DateTime.UtcNow &&
                File.Exists(GetFilePath(key)));
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                var filePath = GetFilePath(key);
                if (File.Exists(filePath)) File.Delete(filePath);
                var metaPath = GetMetaPath(key);
                if (File.Exists(metaPath)) File.Delete(metaPath);
                _metadata.TryRemove(key, out _);
                lock (_statsLock)
                {
                    _statistics.TotalItems = _metadata.Count;
                    _statistics.TotalSizeBytes = Size;
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"FileCache Remove error: {key}");
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            var keys = _metadata.Keys.Where(k =>
                k.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in keys) await RemoveAsync(key);
        }

        public async Task ClearAsync()
        {
            var files = Directory.GetFiles(_cacheDirectory, "*.*");
            foreach (var file in files) File.Delete(file);
            _metadata.Clear();
            lock (_statsLock)
            {
                _statistics.TotalItems = 0;
                _statistics.TotalSizeBytes = 0;
            }
            await Task.CompletedTask;
        }

        public async Task<List<string>> GetKeysAsync()
            => await Task.FromResult(_metadata.Keys.ToList());

        public CacheStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                _statistics.TotalItems = _metadata.Count;
                _statistics.TotalSizeBytes = Size;
                _statistics.LastUpdated = DateTime.UtcNow;
                return _statistics;
            }
        }
    }
}