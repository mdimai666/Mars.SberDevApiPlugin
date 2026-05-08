using System.ComponentModel;
using System.Text;
using System.Text.Json;
using GigaChat.SemanticKernel;
using GigaChatAPI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SberDevApiPluginConsoleApp;

internal static class GigaChatSemanticKernelTest
{
    public static async Task Main()
    {
        await FunctionCallAuto();
        //await FunctionCallRaw();
    }

    public static async Task FunctionCallAuto()
    {
        var authKey = Environment.GetEnvironmentVariable("GigaChatAPI_AuthKey") ?? throw new ArgumentNullException("GigaChatAPI_AuthKey");

        // Использование плагина с GigaChat
        var builder = Kernel.CreateBuilder();
        builder.AddGigaChatChatCompletion(authKey, "GigaChat");
        //builder.AddGigaChatChatCompletion(authKey, "GigaChat-Pro");
        var kernel = builder.Build();

        kernel.ImportPluginFromType<WeatherPlugin>();

        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var chat = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddUserMessage("Какая погода в Москве?");
        //history.AddUserMessage("Покажи список доступных функций");

        var response = await chat.GetChatMessageContentsAsync(history, settings, kernel);
        Console.WriteLine($"Ответ: {response[0].Content}");

        // Просмотр использованных токенов
        if (response[0].Metadata?.TryGetValue("usage", out var usage) == true)
        {
            Console.WriteLine($"Tokens used: {JsonSerializer.Serialize(usage)}");
        }
    }

    public class WeatherPlugin
    {
#if !true
        [KernelFunction("get_weather")]
        [Description("Получает текущую погоду в городе")]
        public string GetWeather([Description("Название города")] string city)
        {
            return $"В {city} сейчас +15°C и солнечно";
        }
#else

        [KernelFunction("get_weather")]
        [Description("Получает текущую погоду в городе")]
        public WatherResult GetWeather([Description("Название города")] string city)
        {
            return new() { TextResponse = $"В {city} сейчас +15°C и солнечно" };
        }
#endif
    }

    public class WatherResult
    {
        public string TextResponse { get; set; } = "";
    }

    public static async Task FunctionCallRaw()
    {
        var authKey = Environment.GetEnvironmentVariable("GigaChatAPI_AuthKey") ?? throw new ArgumentNullException("GigaChatAPI_AuthKey");

        var request = new
        {
            model = "GigaChat-Pro",  // Попробуйте также "GigaChat-2-Pro"
            messages = new[]
            {
                new { role = "user", content = "Какая погода в Москве?" }
            },
            functions = new[]
            {
                new
                {
                    name = "get_weather",  // Простое имя без "_"
                    description = "Получает погоду в городе",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            city = new
                            {
                                type = "string",
                                description = "Название города"
                            }
                        },
                        required = new[] { "city" }
                    }
                }
            },
            function_call = "auto"
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var client = new GigaChatClient(authKey);
        var httpClient = await client.GetAuthorizedClientAsync();

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("https://gigachat.devices.sberbank.ru/api/v1/chat/completions", content);

        var responseText = await response.Content.ReadAsStringAsync();
        Console.WriteLine("\nResponse:");
        Console.WriteLine(responseText);

        // Проверяем, есть ли function_call в ответе
        if (responseText.Contains("function_call"))
        {
            Console.WriteLine("\n✓ Functions are working!");
        }
        else
        {
            Console.WriteLine("\n✗ Functions are not working. Check API version and permissions.");
        }
    }
}
