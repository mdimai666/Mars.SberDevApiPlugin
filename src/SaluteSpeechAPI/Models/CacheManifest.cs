using System.Text.Json.Serialization;

namespace SaluteSpeechAPI.Models;

public class CacheManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 2; // Increased version for new format

    [JsonPropertyName("max_cache_size")]
    public int MaxCacheSize { get; set; } = 1000;

    [JsonPropertyName("max_age_days")]
    public int MaxAgeDays { get; set; } = 7;

    [JsonPropertyName("entries")]
    public Dictionary<string, CacheEntry> Entries { get; set; } = [];

    [JsonPropertyName("total_size_bytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("last_cleanup_at")]
    public long LastCleanupAt { get; set; }

    [JsonPropertyName("cache_hits")]
    public int CacheHits { get; set; }

    [JsonPropertyName("cache_misses")]
    public int CacheMisses { get; set; }
}
