using Flurl.Http;
using Grpc.Core;
using NAudio.Wave;
using SaluteSpeechAPI;
using Smartspeech.Synthesis.V2;

namespace SberDevApiPluginConsoleApp;

internal static class SpeechTest
{
    public static async Task Main()
    {
        var flurl = new FlurlClient();
        var authKey = Environment.GetEnvironmentVariable("SaluteSpeechAPI_AuthKey") ?? throw new ArgumentNullException("SaluteSpeechAPI_AuthKey");

        //var client = new SberDevApiServiceClient(flurl);

        //await client.Auth(authKey);

        //await client.TextToSpeech("Привет, Дима!");

        var text = "Привет, мир! Это тестовое сообщение.";

        // Create client with auth key only

        try
        {
#if RAW_CLIENT
    var host = "smartspeech.sber.ru";
    var outputFile = "output.wav";
    await using var client = new SynthesisClient(authKey);
    // Client will automatically authenticate on first request
    await client.SynthesizeAsync(
        text: text,
        outputFilePath: outputFile,
        audioEncoding: Options.Types.AudioEncoding.Wav,
        language: "ru-RU",
        voice: "May_24000"
    );

    Console.WriteLine($"Audio saved to {outputFile}");

    //Optional: Get token info
    var tokenInfo = await client.GetTokenInfoAsync();
    if (tokenInfo != null)
    {
        Console.WriteLine($"Token expires at: {tokenInfo.AsLocalTime}");
    } 
#endif

#if !CACHE_CLIENT
            using var client = new SynthesisCache(authKey);
            // Client will automatically authenticate on first request
            var audioFilePath = await client.SynthesizeWithCacheAsync(
                text: text,
                //outputFilePath: outputFile,
                audioEncoding: Options.Types.AudioEncoding.Wav,
                language: "ru-RU",
                voice: "May_24000"
            );

            Console.WriteLine($"Audio saved to {audioFilePath}");
#endif

            await PlayAsync(audioFilePath);
        }
        catch (RpcException ex)
        {
            Console.WriteLine($"RPC error: code = {ex.StatusCode}, details = {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }

        async Task PlayAsync(string filePath)
        {
            using var audioFile = new AudioFileReader(filePath);
            using var outputDevice = new WaveOutEvent();

            var tcs = new TaskCompletionSource();

            outputDevice.PlaybackStopped += (s, e) =>
            {
                if (e.Exception != null)
                    tcs.TrySetException(e.Exception);
                else
                    tcs.TrySetResult();
            };

            outputDevice.Init(audioFile);
            outputDevice.Play();

            await tcs.Task;
        }

    }
}
