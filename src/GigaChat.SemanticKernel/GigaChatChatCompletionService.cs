using System.Runtime.CompilerServices;
using System.Text.Json;
using GigaChatAPI;
using GigaChatAPI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GigaChat.SemanticKernel;

/// <summary>
/// Адаптер для интеграции GigaChat с Semantic Kernel
/// </summary>
public class GigaChatChatCompletionService : IChatCompletionService
{
    private readonly GigaChatClient _client;
    private readonly string _defaultModel;
    private readonly ILogger? _logger;

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public GigaChatChatCompletionService(
        string authKey,
        string defaultModel = "GigaChat",
        ILogger? logger = null)
    {
        _client = new GigaChatClient(authKey);
        _defaultModel = defaultModel;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var request = CreateChatRequest(chatHistory, executionSettings, kernel);

        // Отправляем запрос
        var response = await SendChatRequestAsync(request, cancellationToken);

        if (response == null || !response.Choices.Any())
            return [];

        var choice = response.Choices[0];

        // Если модель хочет вызвать функцию
        if (choice.FinishReason == "function_call" && choice.Message.FunctionCall != null && kernel != null)
        {
            return await HandleFunctionCallAsync(choice.Message.FunctionCall, chatHistory, executionSettings, kernel, cancellationToken);
        }

        // Обычный ответ
        return
            [
                new ChatMessageContent(
                    AuthorRole.Assistant,
                    choice.Message.Content ?? string.Empty,
                    metadata: CreateMetadata(response, choice)
                )
            ];
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = CreateChatRequest(chatHistory, executionSettings, kernel);
        request.Stream = true;

        await foreach (var chunk in _client.SendMessageStreamAsync(
            message: request.Messages.Last().Content,
            model: request.Model,
            temperature: request.Temperature ?? 0.7,
            cancellationToken: cancellationToken))
        {
            yield return new StreamingChatMessageContent(
                AuthorRole.Assistant,
                chunk
            );
        }
    }

    private ChatRequest CreateChatRequest(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings,
        Kernel? kernel)
    {
        var request = new ChatRequest
        {
            Model = _defaultModel,
            Stream = false,
            RepetitionPenalty = 1.0,
            //FunctionCall = "auto" // Автоматический вызов функций
            FunctionCall = GetFunctionCallMode(executionSettings, kernel)
        };

        // Конвертируем историю сообщений
        foreach (var message in chatHistory)
        {
            var chatMessage = new ChatMessage
            {
                Role = MapAuthorRole(message.Role),
                Content = message.Content ?? string.Empty
            };

            // Если это сообщение с результатом функции
            if (message.Role == AuthorRole.Tool && message.Metadata?.TryGetValue("function_name", out var funcName) == true)
            {
                chatMessage.Role = "function";
                chatMessage.Name = funcName?.ToString();
            }
            else if (message.Role == AuthorRole.Assistant && message.Metadata?.TryGetValue("function_call", out var function_call_obj) == true)
            {
                var functionCall = function_call_obj as FunctionCall;
                chatMessage.FunctionCall = functionCall;

            }

            request.Messages.Add(chatMessage);
        }

        // Добавляем функции из Kernel
        if (kernel != null && executionSettings is OpenAIPromptExecutionSettings openAiSettings
            && openAiSettings.ToolCallBehavior != null)
        {
            request.Functions = GetFunctionDefinitionsFromKernel(kernel);
        }

        // Применяем настройки
        ApplyExecutionSettings(request, executionSettings);

        return request;
    }

    private string GetFunctionCallMode(PromptExecutionSettings? settings, Kernel? kernel)
    {
        // Проверяем OpenAIPromptExecutionSettings
        if (settings is OpenAIPromptExecutionSettings openAiSettings)
        {
            var behavior = openAiSettings.ToolCallBehavior;

            if (behavior == ToolCallBehavior.AutoInvokeKernelFunctions ||
                behavior == ToolCallBehavior.EnableKernelFunctions)
            {
                return "auto";
            }
            else
            {
                return "none";
            }
        }
        else
        {
            throw new NotSupportedException("please use OpenAIPromptExecutionSettings");
        }

        // Если нет функций в Kernel - none, иначе auto
        return (kernel?.Plugins.Any() == true) ? "auto" : "none";
    }

    private List<FunctionDefinition> GetFunctionDefinitionsFromKernel(Kernel kernel)
    {
        var functions = new List<FunctionDefinition>();

        foreach (var plugin in kernel.Plugins)
        {
            foreach (var function in plugin)
            {
                var funcDef = new FunctionDefinition
                {
                    Name = $"{plugin.Name}.{function.Name}",
                    Description = function.Description ?? string.Empty,
                    Parameters = function.JsonSchema,
                    ReturnParameters = function.Metadata.ReturnParameter.Schema
                };

                functions.Add(funcDef);
            }
        }

        return functions;
    }

    private void ApplyExecutionSettings(ChatRequest request, PromptExecutionSettings? settings)
    {
        if (settings == null) return;

        if (settings is OpenAIPromptExecutionSettings openAiSettings)
        {
            request.Temperature = openAiSettings.Temperature;
            request.MaxTokens = openAiSettings.MaxTokens;
            request.TopP = openAiSettings.TopP;
        }
        else if (settings.ExtensionData != null)
        {
            if (settings.ExtensionData.TryGetValue("temperature", out var temp))
                request.Temperature = Convert.ToDouble(temp);
            if (settings.ExtensionData.TryGetValue("max_tokens", out var maxTokens))
                request.MaxTokens = Convert.ToInt32(maxTokens);
            if (settings.ExtensionData.TryGetValue("top_p", out var topP))
                request.TopP = Convert.ToDouble(topP);
        }
    }

    private Task<ChatResponse> SendChatRequestAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        //var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        return _client.SendMessageFullResponseAsync(request, cancellationToken);
    }

    private async Task<IReadOnlyList<ChatMessageContent>> HandleFunctionCallAsync(
        FunctionCall functionCall,
        ChatHistory originalHistory,
        PromptExecutionSettings? executionSettings,
        Kernel kernel,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation($"Function call requested: {functionCall.Name}");

        // Разбираем имя функции (формат: "PluginName.FunctionName")
        var parts = functionCall.Name.Split('.', 2);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"Invalid function name format: {functionCall.Name}");
        }

        var pluginName = parts[0];
        var functionName = parts[1];

        // Получаем и вызываем функцию
        kernel.Plugins.TryGetPlugin(pluginName, out var plugin);
        var function = plugin?[functionName];

        if (function == null)
        {
            throw new InvalidOperationException($"Function not found: {functionCall.Name}");
        }

        // Вызываем функцию с аргументами
        var arguments = new KernelArguments();
        if (functionCall.Arguments != null)
        {
            foreach (var arg in functionCall.Arguments)
            {
                arguments[arg.Key] = arg.Value;
            }
        }

        var result = await kernel.InvokeAsync(function, arguments, cancellationToken: cancellationToken);

        // Создаем новую историю для продолжения диалога
        var newHistory = new ChatHistory();

        // Копируем все сообщения из оригинальной истории
        foreach (var msg in originalHistory)
        {
            newHistory.AddMessage(msg.Role, msg.Content ?? string.Empty);
        }

        // Добавляем сообщение ассистента с вызовом функции
        newHistory.AddMessage(
            AuthorRole.Assistant,
            string.Empty,
            metadata: new Dictionary<string, object?>
            {
                ["function_call"] = functionCall
            }
        );

        var functionResultContent = result.ValueType == typeof(string)
                                        ? JsonSerializer.Serialize(new { text = result.GetValue<string>() })// Api почему то принимает только json
                                        : JsonSerializer.Serialize(result.GetValue<object>());

        // Добавляем результат функции как ответ от "function" роли
        newHistory.AddMessage(
            AuthorRole.Tool, // Будет преобразовано в "function" при сериализации
            functionResultContent,
            metadata: new Dictionary<string, object?>
            {
                ["function_name"] = functionCall.Name,
            }
        );

        // Получаем финальный ответ
        return await GetChatMessageContentsAsync(newHistory, executionSettings, kernel, cancellationToken);
    }

    private Dictionary<string, object?> CreateMetadata(ChatResponse response, Choice choice)
    {
        return new Dictionary<string, object?>
        {
            ["model"] = response.Model,
            ["usage"] = response.Usage,
            ["finish_reason"] = choice.FinishReason,
            ["created"] = response.Created
        };
    }

    private string MapAuthorRole(AuthorRole role)
    {
        return role.Label switch
        {
            "user" => "user",
            "assistant" => "assistant",
            "system" => "system",
            "tool" => "function",
            _ => "user"
        };
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
