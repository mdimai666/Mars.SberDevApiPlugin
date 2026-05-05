namespace SaluteSpeechAPI.Models;

public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public long TotalSizeBytes { get; set; }
    public int MaxEntries { get; set; }
    public int MaxAgeDays { get; set; }
    public long AverageEntrySizeBytes { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double HitRate { get; set; }

    public string TotalSizeFormatted => FormatBytes(TotalSizeBytes);
    public string AverageSizeFormatted => FormatBytes(AverageEntrySizeBytes);

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public override string ToString()
    {
        return $"Cache Stats: {TotalEntries}/{MaxEntries} entries, {TotalSizeFormatted} total, " +
               $"avg {AverageSizeFormatted}, max age {MaxAgeDays} days, " +
               $"hit rate: {HitRate:F1}% ({CacheHits}/{CacheHits + CacheMisses})";
    }
}
