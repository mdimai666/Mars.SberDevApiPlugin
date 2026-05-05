using System.Text.Json;
using SaluteSpeechAPI.Models;
using Smartspeech.Synthesis.V2;

namespace SaluteSpeechAPI;

public class SynthesisCache : IDisposable
{
    private const int DefaultMaxCacheSize = 1000;
    private const int DefaultMaxAgeDays = 7;
    private const long MaxCacheSizeBytes = 1024 * 1024 * 1024; // 1 GB default limit

    private readonly string _cacheDirectory;
    private readonly string _manifestPath;
    private CacheManifest _manifest;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly SynthesisClient _client;
    private readonly bool _debugMode;

    public string AuthKey => _client.AuthKey;

    public SynthesisCache(string authKey, string? cacheDirectory = null,
                          int maxCacheSize = DefaultMaxCacheSize,
                          int maxAgeDays = DefaultMaxAgeDays,
                          bool debugMode = false)
    {
        _cacheDirectory = string.IsNullOrWhiteSpace(cacheDirectory)
                            ? Path.Combine(Path.GetTempPath(), "salutespeech_cache")
                            : cacheDirectory;
        _manifestPath = Path.Combine(_cacheDirectory, "manifest.json");
        _client = new SynthesisClient(authKey);
        _debugMode = debugMode;

        Directory.CreateDirectory(_cacheDirectory);

        LoadManifest();

        if (_manifest.MaxCacheSize != maxCacheSize || _manifest.MaxAgeDays != maxAgeDays)
        {
            _manifest.MaxCacheSize = maxCacheSize;
            _manifest.MaxAgeDays = maxAgeDays;
            SaveManifest();
        }

        if (_debugMode)
        {
            Console.WriteLine($"Cache initialized at: {cacheDirectory}");
            Console.WriteLine($"Current entries: {_manifest.Entries.Count}");
            Console.WriteLine($"Cache hits: {_manifest.CacheHits}, misses: {_manifest.CacheMisses}");
        }
    }

    private void LoadManifest()
    {
        if (File.Exists(_manifestPath))
        {
            var json = File.ReadAllText(_manifestPath);
            _manifest = JsonSerializer.Deserialize<CacheManifest>(json) ?? new CacheManifest();

            // Handle version upgrade
            if (_manifest.Version < 2)
            {
                UpgradeManifest();
            }

            // Clean up orphaned entries (files that exist but not in manifest)
            CleanupOrphanedFiles();
        }
        else
        {
            _manifest = new CacheManifest();
            SaveManifest();
        }
    }

    private void UpgradeManifest()
    {
        if (_debugMode) Console.WriteLine("Upgrading cache manifest to version 2");

        // Recompute all cache keys using new method
        var newEntries = new Dictionary<string, CacheEntry>();
        foreach (var entry in _manifest.Entries.Values)
        {
            var newKey = entry.GetCacheKey();
            newEntries[newKey] = entry;

            // Rename audio file if needed
            var oldFilePath = entry.AudioFilePath;
            var newFilePath = GetAudioFilePath(newKey, entry.AudioEncoding);
            if (oldFilePath != newFilePath && File.Exists(oldFilePath))
            {
                try
                {
                    File.Move(oldFilePath, newFilePath);
                    entry.AudioFilePath = newFilePath;
                }
                catch { /* Ignore rename errors */ }
            }
        }

        _manifest.Entries = newEntries;
        _manifest.Version = 2;
        SaveManifest();
    }

    private void SaveManifest()
    {
        var json = JsonSerializer.Serialize(_manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, json);
    }

    private void CleanupOrphanedFiles()
    {
        var audioFiles = Directory.GetFiles(_cacheDirectory, "*.wav")
            .Concat(Directory.GetFiles(_cacheDirectory, "*.opus"))
            .Concat(Directory.GetFiles(_cacheDirectory, "*.pcm"))
            .Concat(Directory.GetFiles(_cacheDirectory, "*.alaw"))
            .Concat(Directory.GetFiles(_cacheDirectory, "*.g729"));

        var manifestFiles = _manifest.Entries.Values.Select(e => e.AudioFilePath).ToHashSet();

        foreach (var file in audioFiles)
        {
            if (!manifestFiles.Contains(file))
            {
                try
                {
                    if (_debugMode) Console.WriteLine($"Removing orphaned file: {file}");
                    File.Delete(file);
                }
                catch { /* Ignore deletion errors */ }
            }
        }
    }

    private string GetAudioFilePath(string cacheKey, string audioEncoding)
    {
        var extension = audioEncoding.ToLower() switch
        {
            "wav" => "wav",
            "opus" => "opus",
            "pcm_s16le" => "pcm",
            "pcm_alaw" => "alaw",
            "g729" => "g729",
            _ => "audio"
        };
        return Path.Combine(_cacheDirectory, $"{cacheKey}.{extension}");
    }

    private string GetEncodingString(Options.Types.AudioEncoding encoding)
    {
        return encoding switch
        {
            Options.Types.AudioEncoding.Wav => "wav",
            Options.Types.AudioEncoding.Opus => "opus",
            Options.Types.AudioEncoding.PcmS16Le => "pcm_s16le",
            Options.Types.AudioEncoding.PcmAlaw => "pcm_alaw",
            Options.Types.AudioEncoding.G729 => "g729",
            _ => "wav"
        };
    }

    private Options.Types.AudioEncoding GetEncodingFromString(string encoding)
    {
        return encoding switch
        {
            "wav" => Options.Types.AudioEncoding.Wav,
            "opus" => Options.Types.AudioEncoding.Opus,
            "pcm_s16le" => Options.Types.AudioEncoding.PcmS16Le,
            "pcm_alaw" => Options.Types.AudioEncoding.PcmAlaw,
            "g729" => Options.Types.AudioEncoding.G729,
            _ => Options.Types.AudioEncoding.Wav
        };
    }

    private string GetContentTypeString(Text.Types.ContentType contentType)
    {
        return contentType == Text.Types.ContentType.Text ? "text" : "ssml";
    }

    private Text.Types.ContentType GetContentTypeFromString(string contentType)
    {
        return contentType == "text" ? Text.Types.ContentType.Text : Text.Types.ContentType.Ssml;
    }

    public async Task<string> SynthesizeWithCacheAsync(
        string text,
        Options.Types.AudioEncoding audioEncoding = Options.Types.AudioEncoding.Wav,
        string language = "ru-RU",
        string voice = "May_24000",
        Text.Types.ContentType contentType = Text.Types.ContentType.Text,
        CancellationToken cancellationToken = default)
    {
        // Create temporary entry to generate cache key
        var tempEntry = new CacheEntry
        {
            Text = text,
            AudioEncoding = GetEncodingString(audioEncoding),
            Language = language,
            Voice = voice,
            ContentType = GetContentTypeString(contentType)
        };

        var cacheKey = tempEntry.GetCacheKey();
        var encodingStr = GetEncodingString(audioEncoding);
        var audioFilePath = GetAudioFilePath(cacheKey, encodingStr);

        if (_debugMode)
        {
            Console.WriteLine($"Cache key: {cacheKey}");
            Console.WriteLine($"Text hash: {text.GetHashCode()}");
            Console.WriteLine($"Audio encoding: {audioEncoding} -> {encodingStr}");
            Console.WriteLine($"Expected file: {audioFilePath}");
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Check if entry exists and is valid
            if (_manifest.Entries.TryGetValue(cacheKey, out var entry))
            {
                if (!entry.IsExpired(_manifest.MaxAgeDays) && File.Exists(entry.AudioFilePath))
                {
                    // Update last accessed time
                    entry.LastAccessedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    _manifest.CacheHits++;
                    SaveManifest();

                    if (_debugMode)
                    {
                        Console.WriteLine($"✓ CACHE HIT for key: {cacheKey}");
                        Console.WriteLine($"  Created: {entry.CreatedAtLocal}");
                        Console.WriteLine($"  Size: {entry.SizeBytes} bytes");
                    }
                    else
                    {
                        Console.WriteLine($"Cache hit for text: '{TruncateText(text)}'");
                    }
                    return entry.AudioFilePath;
                }
                else
                {
                    if (_debugMode) Console.WriteLine($"Entry expired or file missing, removing...");
                    RemoveEntry(cacheKey);
                }
            }

            // Cache miss
            _manifest.CacheMisses++;
            if (_debugMode)
            {
                Console.WriteLine($"✗ CACHE MISS for key: {cacheKey}");
                Console.WriteLine($"Entries in cache: {_manifest.Entries.Count}");
                Console.WriteLine($"Available keys: {string.Join(", ", _manifest.Entries.Keys.Take(5))}");
            }
            else
            {
                Console.WriteLine($"Cache miss for text: '{TruncateText(text)}'");
            }

            // Synthesize new audio
            await _client.SynthesizeAsync(text, audioFilePath, audioEncoding, language, voice, contentType, cancellationToken);

            // Create cache entry
            var fileInfo = new FileInfo(audioFilePath);
            var newEntry = new CacheEntry
            {
                Text = text,
                AudioEncoding = encodingStr,
                Language = language,
                Voice = voice,
                ContentType = GetContentTypeString(contentType),
                AudioFilePath = audioFilePath,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                LastAccessedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                SizeBytes = fileInfo.Length
            };

            _manifest.Entries[cacheKey] = newEntry;
            _manifest.TotalSizeBytes += newEntry.SizeBytes;

            // Check cache limits
            await EnforceCacheLimitsAsync(cancellationToken);

            SaveManifest();

            if (_debugMode) Console.WriteLine($"Added to cache, total entries: {_manifest.Entries.Count}");
            return audioFilePath;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<byte[]> SynthesizeToBytesWithCacheAsync(
        string text,
        Options.Types.AudioEncoding audioEncoding = Options.Types.AudioEncoding.Wav,
        string language = "ru-RU",
        string voice = "May_24000",
        Text.Types.ContentType contentType = Text.Types.ContentType.Text,
        CancellationToken cancellationToken = default)
    {
        var audioPath = await SynthesizeWithCacheAsync(text, audioEncoding, language, voice, contentType, cancellationToken);
        return await File.ReadAllBytesAsync(audioPath, cancellationToken);
    }

    private void RemoveEntry(string cacheKey)
    {
        if (_manifest.Entries.TryGetValue(cacheKey, out var entry))
        {
            _manifest.TotalSizeBytes -= entry.SizeBytes;
            _manifest.Entries.Remove(cacheKey);

            // Delete audio file
            try
            {
                if (File.Exists(entry.AudioFilePath))
                {
                    File.Delete(entry.AudioFilePath);
                    if (_debugMode) Console.WriteLine($"Deleted file: {entry.AudioFilePath}");
                }
            }
            catch { /* Ignore deletion errors */ }
        }
    }

    private async Task EnforceCacheLimitsAsync(CancellationToken cancellationToken)
    {
        // Enforce max entry count
        while (_manifest.Entries.Count > _manifest.MaxCacheSize)
        {
            // Remove least recently accessed entry
            var oldest = _manifest.Entries
                .OrderBy(e => e.Value.LastAccessedAt)
                .First();
            if (_debugMode) Console.WriteLine($"Enforcing cache limit, removing oldest entry: {oldest.Key}");
            RemoveEntry(oldest.Key);
        }

        // Enforce max total size (optional)
        if (_manifest.TotalSizeBytes > MaxCacheSizeBytes)
        {
            var toRemove = _manifest.Entries
                .OrderBy(e => e.Value.LastAccessedAt)
                .ToList();

            foreach (var entry in toRemove)
            {
                if (_manifest.TotalSizeBytes <= MaxCacheSizeBytes)
                    break;
                RemoveEntry(entry.Key);
            }
        }

        // Periodic cleanup (once per day)
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dayMs = 24 * 3600 * 1000L;
        if (now - _manifest.LastCleanupAt > dayMs)
        {
            await CleanupExpiredEntriesAsync(cancellationToken);
            _manifest.LastCleanupAt = now;
            SaveManifest();
        }
    }

    private async Task CleanupExpiredEntriesAsync(CancellationToken cancellationToken)
    {
        var expiredKeys = _manifest.Entries
            .Where(e => e.Value.IsExpired(_manifest.MaxAgeDays))
            .Select(e => e.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            RemoveEntry(key);
        }

        if (expiredKeys.Any())
        {
            Console.WriteLine($"Cleaned up {expiredKeys.Count} expired cache entries");
            SaveManifest();
        }
    }

    public async Task ClearCacheAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // Delete all audio files
            foreach (var entry in _manifest.Entries.Values)
            {
                try
                {
                    if (File.Exists(entry.AudioFilePath))
                        File.Delete(entry.AudioFilePath);
                }
                catch { }
            }

            _manifest.Entries.Clear();
            _manifest.TotalSizeBytes = 0;
            _manifest.CacheHits = 0;
            _manifest.CacheMisses = 0;
            SaveManifest();
            Console.WriteLine("Cache cleared successfully");
        }
        finally
        {
            _lock.Release();
        }
    }

    public CacheStatistics GetCacheStatistics()
    {
        var stats = new CacheStatistics
        {
            TotalEntries = _manifest.Entries.Count,
            TotalSizeBytes = _manifest.TotalSizeBytes,
            MaxEntries = _manifest.MaxCacheSize,
            MaxAgeDays = _manifest.MaxAgeDays,
            AverageEntrySizeBytes = _manifest.Entries.Any()
                ? _manifest.TotalSizeBytes / _manifest.Entries.Count
                : 0,
            CacheHits = _manifest.CacheHits,
            CacheMisses = _manifest.CacheMisses,
            HitRate = _manifest.CacheHits + _manifest.CacheMisses > 0
                ? (double)_manifest.CacheHits / (_manifest.CacheHits + _manifest.CacheMisses) * 100
                : 0
        };
        return stats;
    }

    public List<CacheEntry> GetAllEntries()
    {
        return _manifest.Entries.Values.OrderByDescending(e => e.LastAccessedAt).ToList();
    }

    private string TruncateText(string text, int maxLength = 50)
    {
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }

    public void Dispose()
    {
        _lock?.Dispose();
        _client?.DisposeAsync().AsTask().Wait();
    }
}
