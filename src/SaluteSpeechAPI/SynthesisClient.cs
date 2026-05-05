using System.Net.Http.Json;
using Grpc.Core;
using Grpc.Net.Client;
using SaluteSpeechAPI.Dto;
using Smartspeech.Synthesis.V2;

namespace SaluteSpeechAPI;

public class SynthesisClient : IAsyncDisposable
{
    private const string DefaultOAuthUrl = @"https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

    private readonly string _authKey;
    private readonly string _host;
    private readonly string _oauthUrl;
    private SberAccessTokenResponse? _currentToken;
    private GrpcChannel? _channel;
    private SmartSpeech.SmartSpeechClient? _client;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly HttpClient _httpClient = new();

    public string AuthKey => _authKey;

    public SynthesisClient(string authKey, string host = "smartspeech.sber.ru", string? oauthUrl = null)
    {
        _authKey = authKey ?? throw new ArgumentNullException(nameof(authKey));
        _host = host;
        _oauthUrl = oauthUrl ?? DefaultOAuthUrl;
    }

    private async Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_currentToken == null || _currentToken.IsExpired)
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

    private async Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var requestContent = new FormUrlEncodedContent(new[]
        {
                new KeyValuePair<string, string>("scope", "SALUTE_SPEECH_PERS")
            });

        var request = new HttpRequestMessage(HttpMethod.Post, _oauthUrl)
        {
            Content = requestContent
        };

        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("RqUID", Guid.NewGuid().ToString());
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _authKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        _currentToken = await response.Content.ReadFromJsonAsync<SberAccessTokenResponse>(cancellationToken: cancellationToken);

        if (_currentToken == null)
        {
            throw new InvalidOperationException("Failed to authenticate: token response is null");
        }

        // Recreate gRPC channel with new token
        await RecreateChannelAsync(cancellationToken);
    }

    private async Task RecreateChannelAsync(CancellationToken cancellationToken = default)
    {
        if (_channel != null)
        {
            await _channel.ShutdownAsync();
            _channel.Dispose();
        }

        var options = new GrpcChannelOptions
        {
            Credentials = ChannelCredentials.Create(
                ChannelCredentials.SecureSsl,
                CallCredentials.FromInterceptor(async (context, metadata) =>
                {
                    var token = await GetBearerTokenAsync(cancellationToken);
                    metadata.Add("authorization", $"Bearer {token}");
                })
            )
        };

        _channel = GrpcChannel.ForAddress($"https://{_host}", options);
        _client = new SmartSpeech.SmartSpeechClient(_channel);
    }

    private SmartSpeech.SmartSpeechClient GetClient()
    {
        if (_client == null)
        {
            throw new InvalidOperationException("Client not initialized. Call InitializeAsync first or use auto-initializing methods.");
        }
        return _client;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await AuthenticateAsync(cancellationToken);
    }

    public async Task<SberAccessTokenResponse?> GetTokenInfoAsync()
    {
        await _tokenLock.WaitAsync();
        try
        {
            return _currentToken != null ? new SberAccessTokenResponse
            {
                AccessToken = _currentToken.AccessToken,
                ExpiresAt = _currentToken.ExpiresAt
            } : null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        await AuthenticateAsync(cancellationToken);
    }

    public async Task SynthesizeAsync(
        string text,
        string outputFilePath,
        Options.Types.AudioEncoding audioEncoding = Options.Types.AudioEncoding.Wav,
        string language = "ru-RU",
        string voice = "May_24000",
        Text.Types.ContentType contentType = Text.Types.ContentType.Text,
        CancellationToken cancellationToken = default)
    {
        // Ensure we have a valid token and client
        if (_client == null)
        {
            await InitializeAsync(cancellationToken);
        }

        using var call = GetClient().Synthesize(cancellationToken: cancellationToken);

        // Send options
        var options = new Options
        {
            AudioEncoding = audioEncoding,
            Language = language,
            Voice = voice
        };

        await call.RequestStream.WriteAsync(new SynthesisRequest { Options = options });

        // Send text
        var textMessage = new Text
        {
            Text_ = text,
            ContentType = contentType
        };

        await call.RequestStream.WriteAsync(new SynthesisRequest { Text = textMessage });
        await call.RequestStream.CompleteAsync();

        // Receive audio
        using var fileStream = File.Create(outputFilePath);
        string? requestId = null;

        try
        {
            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                switch (response.ResponseCase)
                {
                    case SynthesisResponse.ResponseOneofCase.Audio:
                        if (response.Audio.AudioChunk != null)
                        {
                            await fileStream.WriteAsync(response.Audio.AudioChunk.Memory, cancellationToken);

                            if (response.Audio.AudioDuration != null)
                            {
                                var seconds = response.Audio.AudioDuration.Seconds;
                                var nanos = response.Audio.AudioDuration.Nanos;
                                var duration = TimeSpan.FromSeconds(seconds) + TimeSpan.FromTicks(nanos / 100);
                                Console.WriteLine($"Got {duration.TotalSeconds:F3} seconds of audio");
                            }
                        }
                        break;

                    case SynthesisResponse.ResponseOneofCase.BackendInfo:
                        Console.WriteLine($"Backend: {response.BackendInfo.ModelName} v{response.BackendInfo.ModelVersion}");
                        break;
                }
            }
        }
        finally
        {
            // Get request ID from trailing metadata
            var trailers = call.GetTrailers();
            var requestIdEntry = trailers.FirstOrDefault(m => m.Key == "x-request-id");
            if (requestIdEntry != null)
            {
                requestId = requestIdEntry.Value;
                Console.WriteLine($"RequestID: {requestId}");
            }
        }

        Console.WriteLine("Synthesis has finished");
    }

    public async Task<byte[]> SynthesizeToBytesAsync(
        string text,
        Options.Types.AudioEncoding audioEncoding = Options.Types.AudioEncoding.Wav,
        string language = "ru-RU",
        string voice = "May_24000",
        Text.Types.ContentType contentType = Text.Types.ContentType.Text,
        CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            await InitializeAsync(cancellationToken);
        }

        using var memoryStream = new MemoryStream();

        using var call = GetClient().Synthesize(cancellationToken: cancellationToken);

        var options = new Options
        {
            AudioEncoding = audioEncoding,
            Language = language,
            Voice = voice
        };

        await call.RequestStream.WriteAsync(new SynthesisRequest { Options = options });

        var textMessage = new Text
        {
            Text_ = text,
            ContentType = contentType
        };

        await call.RequestStream.WriteAsync(new SynthesisRequest { Text = textMessage });
        await call.RequestStream.CompleteAsync();

        await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
        {
            if (response.ResponseCase == SynthesisResponse.ResponseOneofCase.Audio &&
                response.Audio.AudioChunk != null)
            {
                await memoryStream.WriteAsync(response.Audio.AudioChunk.Memory, cancellationToken);
            }
        }

        return memoryStream.ToArray();
    }

    public async Task StreamSynthesizeAsync(
        string text,
        Func<byte[], Task> onAudioChunk,
        Action<string>? onBackendInfo = null,
        Options.Types.AudioEncoding audioEncoding = Options.Types.AudioEncoding.Wav,
        string language = "ru-RU",
        string voice = "May_24000",
        Text.Types.ContentType contentType = Text.Types.ContentType.Text,
        CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            await InitializeAsync(cancellationToken);
        }

        using var call = GetClient().Synthesize(cancellationToken: cancellationToken);

        var options = new Options
        {
            AudioEncoding = audioEncoding,
            Language = language,
            Voice = voice
        };

        await call.RequestStream.WriteAsync(new SynthesisRequest { Options = options });

        var textMessage = new Text
        {
            Text_ = text,
            ContentType = contentType
        };

        await call.RequestStream.WriteAsync(new SynthesisRequest { Text = textMessage });
        await call.RequestStream.CompleteAsync();

        try
        {
            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                switch (response.ResponseCase)
                {
                    case SynthesisResponse.ResponseOneofCase.Audio:
                        if (response.Audio.AudioChunk != null)
                        {
                            await onAudioChunk(response.Audio.AudioChunk.ToByteArray());
                        }
                        break;

                    case SynthesisResponse.ResponseOneofCase.BackendInfo:
                        onBackendInfo?.Invoke($"{response.BackendInfo.ModelName} v{response.BackendInfo.ModelVersion}");
                        break;
                }
            }
        }
        finally
        {
            var trailers = call.GetTrailers();
            var requestIdEntry = trailers.FirstOrDefault(m => m.Key == "x-request-id");
            if (requestIdEntry != null)
            {
                Console.WriteLine($"RequestID: {requestIdEntry.Value}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
        {
            await _channel.ShutdownAsync();
            _channel.Dispose();
        }
        _httpClient.Dispose();
        _tokenLock.Dispose();
    }
}
