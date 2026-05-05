using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace SaluteSpeechAPI.Models;

public class CacheEntry
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("audio_encoding")]
    public string AudioEncoding { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("voice")]
    public string Voice { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("audio_file_path")]
    public string AudioFilePath { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("last_accessed_at")]
    public long LastAccessedAt { get; set; }

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    [JsonIgnore]
    public DateTimeOffset CreatedAtLocal => DateTimeOffset.FromUnixTimeMilliseconds(CreatedAt).ToLocalTime();

    [JsonIgnore]
    public DateTimeOffset LastAccessedAtLocal => DateTimeOffset.FromUnixTimeMilliseconds(LastAccessedAt).ToLocalTime();

    public string GetCacheKey()
    {
        // Create a deterministic cache key
        var normalizedText = NormalizeText(Text);
        var keyString = $"{normalizedText}|{AudioEncoding}|{Language}|{Voice}|{ContentType}";

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(keyString);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash)[..16]; // First 16 characters of hash
    }

    private string NormalizeText(string text)
    {
        // Normalize text to ensure consistent keys
        // Remove extra whitespace, normalize unicode, etc.
        return text.Trim().Replace("\r\n", "\n").Replace("\r", "\n");
    }

    public bool IsExpired(int maxAgeDays = 7)
    {
        var age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - CreatedAt;
        var maxAgeMs = maxAgeDays * 24 * 3600 * 1000L;
        return age > maxAgeMs;
    }
}
