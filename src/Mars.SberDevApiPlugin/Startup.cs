using Mars.Options.Abstractions.Services;
using Mars.Plugin.Abstractions;
using Mars.Plugin.Kit.Host;
using Mars.SberDevApiPlugin;
using Mars.SberDevApiPlugin.Front;
using Mars.SberDevApiPlugin.Front.Nodes.GigaChat;
using Mars.SberDevApiPlugin.Front.Nodes.SaluteSpeech;
using Mars.SberDevApiPlugin.Services;
using Mars.Server.Abstractions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

[assembly: MarsPlugin(typeof(MainSberDevApiPlugin))]

namespace Mars.SberDevApiPlugin;

public class MainSberDevApiPlugin : MarsPlugin
{
    public const string PluginPackageName = "mdimai666.Mars.SberDevApiPlugin";

    public override void ConfigureWebApplicationBuilder(WebApplicationBuilder builder, PluginSettings settings)
    {
        builder.Services.AddSingleton<SaluteSpeechManager>();
        builder.Services.AddSingleton<GigaChatManager>();
    }

    public override void ConfigureWebApplication(WebApplication app, PluginSettings settings)
    {
        app.Services.AutoHostRegisterHelper([GetType().Assembly, typeof(SaluteSpeechSynthesisNode).Assembly]);

        var logger = MarsLogger.GetStaticLogger<MainSberDevApiPlugin>();

        //logger.LogWarning($"> {PluginPackageName} - Work!!!!2" + Locale.Username);

        var op = app.Services.GetRequiredService<IOptionService>();

        //op.RegisterOption<Example1Plugin1>(appendToInitialSiteData: true);
        //op.SetConstOption(new Example1PluginConstOptionForFront() { ForFrontValue = "123" }, appendToInitialSiteData: true);

        app.MapGet(GigaChatRequestNode.ModelsApiEndpoint, async (GigaChatManager manager, string configNodeId) =>
        {
            return (await manager.Models(configNodeId))?.ToDictionary(s => s.Id, s => s.Id);
        });
    }

}
