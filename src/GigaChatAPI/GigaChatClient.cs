using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GigaChatAPI.Models;

namespace GigaChatAPI;

/// <summary>
/// <seealso href="https://developers.sber.ru/docs/ru/gigachat/quickstart/ind-using-api#generatsiya-teksta"/>
/// </summary>
public class GigaChatClient : IDisposable
{
    private const string DefaultApiUrl = "https://gigachat.devices.sberbank.ru";
    private const string DefaultOAuthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

    private readonly HttpClient _httpClient;
    private readonly string _authKey;
    private GigaChatTokenResponse? _currentToken;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public string AuthKey => _authKey;

    public GigaChatClient(string authKey, string? baseUrl = null)
    {
        _authKey = authKey ?? throw new ArgumentNullException(nameof(authKey));
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl ?? DefaultApiUrl)
        };
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    private async Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_currentToken == null || IsTokenExpired())
            {
                await AuthenticateAsync(cancellationToken);
            }
            return _currentToken!.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private bool IsTokenExpired()
    {
        if (_currentToken == null) return true;
        // Add 5 minute buffer
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= _currentToken.ExpiresAt - 300;
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        using var authClient = new HttpClient();
        var requestContent = new FormUrlEncodedContent(new[]
        {
                new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
            });

        var request = new HttpRequestMessage(HttpMethod.Post, DefaultOAuthUrl)
        {
            Content = requestContent
        };

        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("RqUID", Guid.NewGuid().ToString());
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _authKey);

        var response = await authClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        _currentToken = await response.Content.ReadFromJsonAsync<GigaChatTokenResponse>(cancellationToken: cancellationToken);

        if (_currentToken == null)
        {
            throw new InvalidOperationException("Failed to authenticate: token response is null");
        }
    }

    public async Task<HttpClient> GetAuthorizedClientAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetBearerTokenAsync(cancellationToken);
        var client = new HttpClient
        {
            BaseAddress = _httpClient.BaseAddress
        };
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        return client;
    }

    public async Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        using var client = await GetAuthorizedClientAsync(cancellationToken);
        var response = await client.GetAsync("/api/v1/models", cancellationToken);
        response.EnsureSuccessStatusCode();

        var modelsResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(cancellationToken: cancellationToken);
        return modelsResponse?.Data ?? [];
    }

    public async Task<string> SendMessageAsync(
        string message,
        string model = "GigaChat",
        double temperature = 0.7,
        int? maxTokens = null,
        List<ChatMessage>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ChatRequest
        {
            Model = model,
            Stream = false,
            RepetitionPenalty = 1.0,
            Temperature = temperature,
            MaxTokens = maxTokens
        };

        // Build messages list with history
        if (conversationHistory != null)
        {
            request.Messages.AddRange(conversationHistory);
        }

        request.Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = message
        });

        using var client = await GetAuthorizedClientAsync(cancellationToken);
        var response = await client.PostAsJsonAsync("/api/v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken);

        if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
        {
            throw new InvalidOperationException("No response from GigaChat");
        }

        return chatResponse.Choices[0].Message.Content;
    }

    public Task<ChatResponse> SendMessageFullResponseAsync(
        string message,
        string model = "GigaChat",
        double temperature = 0.7,
        int? maxTokens = null,
        List<ChatMessage>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ChatRequest
        {
            Model = model,
            Stream = false,
            RepetitionPenalty = 1.0,
            Temperature = temperature,
            MaxTokens = maxTokens
        };

        if (conversationHistory != null)
        {
            request.Messages.AddRange(conversationHistory);
        }

        if (!string.IsNullOrEmpty(message))
        {
            request.Messages.Add(new ChatMessage
            {
                Role = "user",
                Content = message
            });
        }

        return SendMessageFullResponseAsync(request, cancellationToken);
    }

    public async Task<ChatResponse> SendMessageFullResponseAsync(ChatRequest request, CancellationToken cancellationToken)
    { 
        var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        using var client = await GetAuthorizedClientAsync(cancellationToken);
        var response = await client.PostAsJsonAsync("/api/v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken);

        if (chatResponse == null)
        {
            throw new InvalidOperationException("No response from GigaChat");
        }

        return chatResponse;
    }

    public async IAsyncEnumerable<string> SendMessageStreamAsync(
        string message,
        string model = "GigaChat",
        double temperature = 0.7,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new ChatRequest
        {
            Model = model,
            Stream = true,
            RepetitionPenalty = 1.0,
            Temperature = temperature,
            Messages =
        [
            new() { Role = "user", Content = message }
        ]
        };

        using var client = await GetAuthorizedClientAsync(cancellationToken);
        var requestContent = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        using var response = await client.PostAsync("/api/v1/chat/completions", requestContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("data: "))
            {
                var jsonData = line.Substring(6);
                if (jsonData == "[DONE]") break;

                // Process without try-catch to avoid yield restrictions
                if (TryParseStreamChunk(jsonData, out var content))
                {
                    yield return content;
                }
            }
        }
    }

    private bool TryParseStreamChunk(string jsonData, out string content)
    {
        content = string.Empty;
        try
        {
            var chunkResponse = JsonSerializer.Deserialize<ChatResponse>(jsonData);
            if (chunkResponse?.Choices != null && chunkResponse.Choices.Count > 0)
            {
                content = chunkResponse.Choices[0].Message.Content;
                return true;
            }
        }
        catch (JsonException)
        {
            // Skip invalid JSON chunks
        }
        return false;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _tokenLock?.Dispose();
    }
}
