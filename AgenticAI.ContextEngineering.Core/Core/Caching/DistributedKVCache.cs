using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    public class DistributedKVCache : IKVCache
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<DistributedKVCache> _logger;
        private readonly object _statsLock = new object();
        private CacheStatistics _statistics = new() { CacheName = "Distributed" };
        private readonly SemaphoreSlim _healthCheckLock = new(1, 1);
        private DateTime _lastHealthCheck = DateTime.MinValue;
        private bool _isConnected = true;
        private string _connectionStatus = "Connected";

        public string Name => "Distributed";
        public int Count
        {
            get
            {
                // Distributed cache doesn't support count easily
                // Return approximate or 0
                return _statistics.TotalItems;
            }
        }
        public long Size => _statistics.TotalSizeBytes;

        public DistributedKVCache(IDistributedCache cache, ILogger<DistributedKVCache> logger)
        {
            _cache = cache;
            _logger = logger;

            // Initial health check
            Task.Run(() => CheckHealthAsync());
        }

        private async Task CheckHealthAsync()
        {
            try
            {
                await _healthCheckLock.WaitAsync();
                try
                {
                    var testKey = $"health_check_{Guid.NewGuid()}";
                    await _cache.SetAsync(testKey, Encoding.UTF8.GetBytes("OK"), new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                    });

                    var data = await _cache.GetAsync(testKey);
                    _isConnected = data != null && Encoding.UTF8.GetString(data) == "OK";
                    _connectionStatus = _isConnected ? "Connected" : "Connection Failed";

                    await _cache.RemoveAsync(testKey);
                    _lastHealthCheck = DateTime.UtcNow;

                    lock (_statsLock)
                    {
                        _statistics.IsConnected = _isConnected;
                        _statistics.ConnectionStatus = _connectionStatus;
                    }
                }
                finally
                {
                    _healthCheckLock.Release();
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _connectionStatus = $"Error: {ex.Message}";
                _logger.LogWarning($"Distributed cache health check failed: {ex.Message}");

                lock (_statsLock)
                {
                    _statistics.IsConnected = false;
                    _statistics.ConnectionStatus = _connectionStatus;
                }
            }
        }

        private async Task<bool> EnsureConnectedAsync()
        {
            if (_isConnected) return true;

            // Try to reconnect if it's been more than 30 seconds
            if ((DateTime.UtcNow - _lastHealthCheck).TotalSeconds > 30)
            {
                await CheckHealthAsync();
            }

            return _isConnected;
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            try
            {
                if (!await EnsureConnectedAsync())
                {
                    lock (_statsLock) _statistics.Misses++;
                    return null;
                }

                var data = await _cache.GetAsync(key);
                if (data == null || data.Length == 0)
                {
                    lock (_statsLock) _statistics.Misses++;
                    return null;
                }

                var json = Encoding.UTF8.GetString(data);
                var value = JsonSerializer.Deserialize<T>(json);

                if (value != null)
                {
                    lock (_statsLock)
                    {
                        _statistics.Hits++;
                        _statistics.TotalSizeBytes += data.Length;
                    }
                    return value;
                }

                lock (_statsLock) _statistics.Misses++;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"DistributedCache Get error: {key}");
                _isConnected = false;
                lock (_statsLock)
                {
                    _statistics.Misses++;
                    _statistics.IsConnected = false;
                    _statistics.ConnectionStatus = $"Error: {ex.Message}";
                }
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                if (value == null) return;

                if (!await EnsureConnectedAsync())
                {
                    _logger.LogWarning($"Distributed cache not connected, cannot set: {key}");
                    return;
                }

                var json = JsonSerializer.Serialize(value);
                var data = Encoding.UTF8.GetBytes(json);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromDays(7),
                    SlidingExpiration = TimeSpan.FromHours(12)
                };

                await _cache.SetAsync(key, data, options);

                lock (_statsLock)
                {
                    _statistics.TotalItems++;
                    _statistics.TotalSizeBytes += data.Length;
                }

                _logger.LogDebug($"✅ DistributedCache SET: {key}, Size: {data.Length} bytes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"DistributedCache Set error: {key}");
                _isConnected = false;
                lock (_statsLock)
                {
                    _statistics.IsConnected = false;
                    _statistics.ConnectionStatus = $"Error: {ex.Message}";
                }
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
            try
            {
                if (!await EnsureConnectedAsync()) return false;

                var data = await _cache.GetAsync(key);
                return data != null && data.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                if (!await EnsureConnectedAsync()) return;

                await _cache.RemoveAsync(key);
                lock (_statsLock)
                {
                    _statistics.TotalItems = Math.Max(0, _statistics.TotalItems - 1);
                }
                _logger.LogDebug($"🗑️ DistributedCache REMOVE: {key}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"DistributedCache Remove error: {key}");
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            // Distributed cache doesn't support pattern-based removal natively
            // This would need to be implemented with Redis SCAN or similar
            _logger.LogWarning($"DistributedCache RemoveByPatternAsync not fully supported for pattern: {pattern}");
            await Task.CompletedTask;
        }

        public async Task ClearAsync()
        {
            // Distributed cache doesn't have a clear all method
            _logger.LogWarning("DistributedCache ClearAsync not supported - use Redis FLUSHDB or SQL TRUNCATE");
            await Task.CompletedTask;
        }

        public async Task<List<string>> GetKeysAsync()
        {
            // Distributed cache doesn't support getting all keys
            _logger.LogWarning("DistributedCache GetKeysAsync not supported");
            return await Task.FromResult(new List<string>());
        }

        public CacheStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                _statistics.IsConnected = _isConnected;
                _statistics.ConnectionStatus = _connectionStatus;
                _statistics.LastUpdated = DateTime.UtcNow;
                return _statistics;
            }
        }
    }
}