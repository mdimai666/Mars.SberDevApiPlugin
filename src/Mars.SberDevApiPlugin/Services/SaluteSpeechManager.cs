using Mars.Host.Shared.Services;
using Mars.Host.Shared.Startup;
using Mars.SberDevApiPlugin.Front.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SaluteSpeechAPI;

namespace Mars.SberDevApiPlugin.Services;

internal class SaluteSpeechManager : IMarsAppLifetimeService
{
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(30);

    private readonly INodeService _nodeService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SaluteSpeechManager> _logger;

    public SaluteSpeechManager(INodeService nodeService, IMemoryCache memoryCache, ILogger<SaluteSpeechManager> logger)
    {
        _nodeService = nodeService;
        _memoryCache = memoryCache;
        _logger = logger;
        _nodeService.OnAssignNodes += _nodeService_OnAssignNodes;
    }

    private void _nodeService_OnAssignNodes()
    {
        var configNodes = _nodeService.BaseNodes.Values.OfType<SaluteSpeechConfigNode>().ToArray();
        RefreshConfigs(configNodes);
    }

    public void RefreshConfigs(SaluteSpeechConfigNode[] configs)
    {
        _logger.LogTrace("RefreshConfigs");

        foreach (var config in configs)
        {
            var instanceKey = ClientCacheKey(config.Id);

            if (_memoryCache.TryGetValue<SynthesisCache>(instanceKey, out var client))
            {
                if (client.AuthKey != config.AuthKey)
                    _memoryCache.Remove(instanceKey);
            }
        }
    }

    public static string ClientCacheKey(string configId)
        => $"SynthesisCache-{configId}";

    public SynthesisCache GetClient(SaluteSpeechConfigNode configNode)
    {
        var instanceKey = ClientCacheKey(configNode.Id);
        return _memoryCache.GetOrCreate(instanceKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheTtl;
            var client = new SynthesisCache(configNode.AuthKey);
            return client;
        })!;
    }

    [StartupOrder(11)]
    public Task OnStartupAsync()
    {
        _nodeService_OnAssignNodes();
        return Task.CompletedTask;
    }
}
