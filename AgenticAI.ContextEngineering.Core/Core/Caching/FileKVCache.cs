// AgenticAI.ContextEngineering.Core/Caching/FileKVCache.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    /// <summary>
    /// File-based KV Cache implementation
    /// </summary>
    public class FileKVCache : IKVCache
    {
        private readonly string _cacheDirectory;
        private readonly ILogger<FileKVCache> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private long _hits = 0;
        private long _misses = 0;

        public string Name => "File";
        public int Count => GetFileCount();
        public long Size => GetTotalSize();

        public FileKVCache(
            IOptions<FileKVCacheOptions> options,
            ILogger<FileKVCache> logger = null)
        {
            _cacheDirectory = options?.Value?.CacheDirectory ?? Path.Combine(Path.GetTempPath(), "AgenticAI_Cache");
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            // Ensure directory exists
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }

            _logger?.LogInformation("FileKVCache initialized with directory: {Directory}", _cacheDirectory);
        }

        // For backward compatibility
        public FileKVCache(string cacheDirectory, ILogger<FileKVCache> logger = null)
        {
            _cacheDirectory = cacheDirectory ?? Path.Combine(Path.GetTempPath(), "AgenticAI_Cache");
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }

            _logger?.LogInformation("FileKVCache initialized with directory: {Directory}", _cacheDirectory);
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            try
            {
                var filePath = GetFilePath(key);
                if (!File.Exists(filePath))
                {
                    Interlocked.Increment(ref _misses);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);

                if (result != null)
                {
                    Interlocked.Increment(ref _hits);
                    // Update last access time
                    File.SetLastAccessTime(filePath, DateTime.UtcNow);
                }
                else
                {
                    Interlocked.Increment(ref _misses);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading from FileKVCache for key: {Key}", key);
                Interlocked.Increment(ref _misses);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var filePath = GetFilePath(key);
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);

                // Set expiration if provided
                if (expiration.HasValue)
                {
                    File.SetLastWriteTime(filePath, DateTime.UtcNow.Add(expiration.Value));
                }

                _logger?.LogDebug("Cached value to FileKVCache for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error writing to FileKVCache for key: {Key}", key);
                throw;
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var cached = await GetAsync<T>(key);
            if (cached != null)
            {
                return cached;
            }

            var value = await factory();
            if (value != null)
            {
                await SetAsync(key, value, expiration);
            }
            return value;
        }

        public Task<bool> ExistsAsync(string key)
        {
            var filePath = GetFilePath(key);
            return Task.FromResult(File.Exists(filePath));
        }

        public Task RemoveAsync(string key)
        {
            try
            {
                var filePath = GetFilePath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger?.LogDebug("Removed key from FileKVCache: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error removing key from FileKVCache: {Key}", key);
            }
            return Task.CompletedTask;
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                var files = Directory.GetFiles(_cacheDirectory, $"{pattern}*.json");
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error deleting file: {File}", file);
                    }
                }
                _logger?.LogDebug("Removed {Count} files matching pattern: {Pattern}", files.Length, pattern);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error removing files by pattern: {Pattern}", pattern);
            }
            await Task.CompletedTask;
        }

        public async Task ClearAsync()
        {
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    foreach (var file in Directory.GetFiles(_cacheDirectory))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error deleting file: {File}", file);
                        }
                    }
                    _logger?.LogInformation("Cleared all files from FileKVCache");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error clearing FileKVCache");
            }
            await Task.CompletedTask;
        }

        public Task<List<string>> GetKeysAsync()
        {
            try
            {
                var keys = new List<string>();
                if (Directory.Exists(_cacheDirectory))
                {
                    foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
                    {
                        var key = Path.GetFileNameWithoutExtension(file);
                        keys.Add(key);
                    }
                }
                return Task.FromResult(keys);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting keys from FileKVCache");
                return Task.FromResult(new List<string>());
            }
        }

        public CacheStatistics GetStatistics()
        {
            var totalItems = Count;
            var totalSize = Size;

            return new CacheStatistics
            {
                CacheName = "File",
                TotalItems = totalItems,
                TotalSizeBytes = totalSize,
                Hits = _hits,
                Misses = _misses,
                Evictions = 0,
                LastUpdated = DateTime.UtcNow,
                IsConnected = Directory.Exists(_cacheDirectory),
                ConnectionStatus = Directory.Exists(_cacheDirectory) ? "Connected" : "Disconnected",
                Metadata = new Dictionary<string, object>
                {
                    ["CacheDirectory"] = _cacheDirectory,
                    ["FileCount"] = totalItems
                }
            };
        }

        private string GetFilePath(string key)
        {
            // Sanitize key for file name
            var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_cacheDirectory, $"{safeKey}.json");
        }

        private int GetFileCount()
        {
            try
            {
                return Directory.Exists(_cacheDirectory)
                    ? Directory.GetFiles(_cacheDirectory, "*.json").Length
                    : 0;
            }
            catch
            {
                return 0;
            }
        }

        private long GetTotalSize()
        {
            try
            {
                if (!Directory.Exists(_cacheDirectory)) return 0;

                long totalSize = 0;
                foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
                {
                    try
                    {
                        totalSize += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // Skip files that can't be accessed
                    }
                }
                return totalSize;
            }
            catch
            {
                return 0;
            }
        }
    }

    public class FileKVCacheOptions
    {
        public string CacheDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "AgenticAI_Cache");
    }
}