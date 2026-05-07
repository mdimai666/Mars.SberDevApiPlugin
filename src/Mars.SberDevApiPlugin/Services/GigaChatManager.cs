using GigaChatAPI;
using GigaChatAPI.Models;
using Mars.Host.Shared.Services;
using Mars.Host.Shared.Startup;
using Mars.SberDevApiPlugin.Front.Nodes.GigaChat;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Mars.SberDevApiPlugin.Services;

internal class GigaChatManager : IMarsAppLifetimeService
{
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(30);

    private readonly INodeService _nodeService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<GigaChatManager> _logger;

    public GigaChatManager(INodeService nodeService, IMemoryCache memoryCache, ILogger<GigaChatManager> logger)
    {
        _nodeService = nodeService;
        _memoryCache = memoryCache;
        _logger = logger;
        _nodeService.OnAssignNodes += _nodeService_OnAssignNodes;
    }

    private void _nodeService_OnAssignNodes()
    {
        var configNodes = _nodeService.BaseNodes.Values.OfType<GigaChatConfigNode>().ToArray();
        RefreshConfigs(configNodes);
    }

    public void RefreshConfigs(GigaChatConfigNode[] configs)
    {
        _logger.LogTrace("RefreshConfigs");

        foreach (var config in configs)
        {
            var instanceKey = ClientCacheKey(config.Id);

            if (_memoryCache.TryGetValue<GigaChatClient>(instanceKey, out var client))
            {
                if (client.AuthKey != config.AuthKey)
                    _memoryCache.Remove(instanceKey);
            }
        }
    }

    public static string ClientCacheKey(string configId)
        => $"GigaChatClient-{configId}";

    public GigaChatClient GetClient(GigaChatConfigNode configNode)
    {
        var instanceKey = ClientCacheKey(configNode.Id);
        return _memoryCache.GetOrCreate(instanceKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheTtl;
            var client = new GigaChatClient(configNode.AuthKey);
            return client;
        })!;
    }

    [StartupOrder(11)]
    public Task OnStartupAsync()
    {
        _nodeService_OnAssignNodes();
        return Task.CompletedTask;
    }

    public const string GigaChatModelsCacheKey = "GigaChatModelsCacheKey";

    public Task<List<ModelInfo>?> Models(string configNodeId)
    {
        var configNode = _nodeService.BaseNodes.GetValueOrDefault(configNodeId);
        if (configNode is null) return Task.FromResult((List<ModelInfo>?)null);

        return _memoryCache.GetOrCreateAsync(GigaChatModelsCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
            var authKey = (configNode as GigaChatConfigNode).AuthKey;
            var client = new GigaChatClient(authKey);
            var models = client.GetModelsAsync();
            return models;
        })!;
    }
}
