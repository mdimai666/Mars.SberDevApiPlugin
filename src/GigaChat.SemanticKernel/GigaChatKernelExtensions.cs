using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.SemanticKernel;

public static class GigaChatKernelExtensions
{
    /// <summary>
    /// Добавляет GigaChat chat completion в Kernel
    /// </summary>
    public static IKernelBuilder AddGigaChatChatCompletion(
        this IKernelBuilder builder,
        string authKey,
        string modelId = "GigaChat",
        string? serviceId = null,
        ILogger? logger = null)
    {
        builder.Services.AddKeyedSingleton<IChatCompletionService>(
            serviceId ?? "gigachat",
            (sp, key) => new GigaChatChatCompletionService(authKey, modelId, logger)
        );

        return builder;
    }

}
