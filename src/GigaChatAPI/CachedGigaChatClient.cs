using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GigaChatAPI.Models;

namespace GigaChatAPI;

#if false
public class CachedChatEntry
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("last_accessed_at")]
    public long LastAccessedAt { get; set; }

    [JsonPropertyName("tokens_used")]
    public int TokensUsed { get; set; }

    public string GetCacheKey()
    {
        var keyString = $"{Message.Trim()}|{Model}|{Temperature}";
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(keyString);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash)[..16];
    }

    public bool IsExpired(int maxAgeDays = 7)
    {
        var age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - CreatedAt;
        var maxAgeMs = maxAgeDays * 24 * 3600 * 1000L;
        return age > maxAgeMs;
    }
}

public class ChatCacheManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("max_cache_size")]
    public int MaxCacheSize { get; set; } = 1000;

    [JsonPropertyName("max_age_days")]
    public int MaxAgeDays { get; set; } = 7;

    [JsonPropertyName("entries")]
    public Dictionary<string, CachedChatEntry> Entries { get; set; } = [];

    [JsonPropertyName("cache_hits")]
    public int CacheHits { get; set; }

    [JsonPropertyName("cache_misses")]
    public int CacheMisses { get; set; }
}

public class CachedGigaChatClient : IDisposable
{
    private readonly GigaChatClient _client;
    private readonly string _cacheDirectory;
    private readonly string _manifestPath;
    private ChatCacheManifest _manifest;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly int _maxCacheSize;
    private readonly int _maxAgeDays;

    public CachedGigaChatClient(string authKey, string cacheDirectory = "gigachat_cache",
                                 int maxCacheSize = 1000, int maxAgeDays = 7)
    {
        _client = new GigaChatClient(authKey);
        _cacheDirectory = cacheDirectory;
        _manifestPath = Path.Combine(cacheDirectory, "chat_manifest.json");
        _maxCacheSize = maxCacheSize;
        _maxAgeDays = maxAgeDays;

        Directory.CreateDirectory(cacheDirectory);
        LoadManifest();
    }

    private void LoadManifest()
    {
        if (File.Exists(_manifestPath))
        {
            var json = File.ReadAllText(_manifestPath);
            _manifest = JsonSerializer.Deserialize<ChatCacheManifest>(json) ?? new ChatCacheManifest();
        }
        else
        {
            _manifest = new ChatCacheManifest
            {
                MaxCacheSize = _maxCacheSize,
                MaxAgeDays = _maxAgeDays
            };
            SaveManifest();
        }
    }

    private void SaveManifest()
    {
        var json = JsonSerializer.Serialize(_manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, json);
    }

    public async Task<string> SendMessageWithCacheAsync(
        string message,
        string model = "GigaChat",
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        var tempEntry = new CachedChatEntry
        {
            Message = message,
            Model = model,
            Temperature = temperature
        };
        var cacheKey = tempEntry.GetCacheKey();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Check cache
            if (_manifest.Entries.TryGetValue(cacheKey, out var entry))
            {
                if (!entry.IsExpired(_maxAgeDays))
                {
                    entry.LastAccessedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    _manifest.CacheHits++;
                    SaveManifest();
                    Console.WriteLine($"Cache hit for: '{TruncateText(message)}'");
                    return entry.Response;
                }
                else
                {
                    // Remove expired entry
                    _manifest.Entries.Remove(cacheKey);
                }
            }

            // Cache miss - call API
            _manifest.CacheMisses++;
            Console.WriteLine($"Cache miss for: '{TruncateText(message)}'");

            var response = await _client.SendMessageAsync(message, model, temperature,
                conversationHistory: null, cancellationToken: cancellationToken);

            // Get full response for token usage
            var fullResponse = await _client.SendMessageFullResponseAsync(message, model, temperature,
                cancellationToken: cancellationToken);

            // Cache the response
            var newEntry = new CachedChatEntry
            {
                Message = message,
                Model = model,
                Temperature = temperature,
                Response = response,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                LastAccessedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                TokensUsed = fullResponse.Usage?.TotalTokens ?? 0
            };

            _manifest.Entries[cacheKey] = newEntry;

            // Enforce cache size limit
            while (_manifest.Entries.Count > _maxCacheSize)
            {
                var oldest = _manifest.Entries
                    .OrderBy(e => e.Value.LastAccessedAt)
                    .First();
                _manifest.Entries.Remove(oldest.Key);
            }

            SaveManifest();
            return response;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> SendWithConversationCacheAsync(
        string message,
        List<ChatMessage> conversationHistory,
        string model = "GigaChat",
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        // Create a cache key that includes conversation
        var historyKey = string.Join("|", conversationHistory.Select(m => $"{m.Role}:{m.Content}"));
        var cacheKey = $"{message}|{model}|{temperature}|{historyKey}".GetHashCode().ToString("X8");

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_manifest.Entries.TryGetValue(cacheKey, out var entry) && !entry.IsExpired(_maxAgeDays))
            {
                entry.LastAccessedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _manifest.CacheHits++;
                SaveManifest();
                return entry.Response;
            }

            _manifest.CacheMisses++;
            var response = await _client.SendMessageAsync(message, model, temperature, conversationHistory, cancellationToken);

            var newEntry = new CachedChatEntry
            {
                Message = message,
                Model = model,
                Temperature = temperature,
                Response = response,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                LastAccessedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _manifest.Entries[cacheKey] = newEntry;

            while (_manifest.Entries.Count > _maxCacheSize)
            {
                var oldest = _manifest.Entries.OrderBy(e => e.Value.LastAccessedAt).First();
                _manifest.Entries.Remove(oldest.Key);
            }

            SaveManifest();
            return response;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        return await _client.GetModelsAsync(cancellationToken);
    }

    public CacheStatistics GetCacheStatistics()
    {
        return new CacheStatistics
        {
            TotalEntries = _manifest.Entries.Count,
            MaxEntries = _manifest.MaxCacheSize,
            MaxAgeDays = _manifest.MaxAgeDays,
            CacheHits = _manifest.CacheHits,
            CacheMisses = _manifest.CacheMisses,
            HitRate = _manifest.CacheHits + _manifest.CacheMisses > 0
                ? (double)_manifest.CacheHits / (_manifest.CacheHits + _manifest.CacheMisses) * 100
                : 0
        };
    }

    public void ClearCache()
    {
        _manifest.Entries.Clear();
        _manifest.CacheHits = 0;
        _manifest.CacheMisses = 0;
        SaveManifest();
        Console.WriteLine("Cache cleared");
    }

    private string TruncateText(string text, int maxLength = 40)
    {
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }

    public void Dispose()
    {
        _client?.Dispose();
        _lock?.Dispose();
    }
}

#endif
