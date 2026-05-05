using System.Text.Json.Serialization;

namespace SaluteSpeechAPI.Dto;

public class SberAccessTokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }

    [JsonPropertyName("expires_at")]
    public required long ExpiresAt { get; set; }

    public DateTimeOffset AsLocalTime => DateTimeOffset.FromUnixTimeMilliseconds(ExpiresAt).ToLocalTime();

    public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= ExpiresAt - 60000; // 1 minute buffer
}
