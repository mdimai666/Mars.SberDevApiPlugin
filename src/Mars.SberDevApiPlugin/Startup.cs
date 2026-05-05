using Mars.Host.Shared.Services;
using Mars.Nodes.Core;
using Mars.Nodes.Core.Implements;
using Mars.Plugin.Abstractions;
using Mars.Plugin.Kit.Host;
using Mars.Plugin.PluginHost;
using Mars.SberDevApiPlugin;
using Mars.SberDevApiPlugin.Front;
using Mars.SberDevApiPlugin.Front.Nodes;
using Mars.SberDevApiPlugin.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: WebApplicationPlugin(typeof(MainSberDevApiPlugin))]

namespace Mars.SberDevApiPlugin;

public class MainSberDevApiPlugin : WebApplicationPlugin
{
    public const string PluginPackageName = "mdimai666.Mars.SberDevApiPlugin";

    public override void ConfigureWebApplicationBuilder(WebApplicationBuilder builder, PluginSettings settings)
    {
        builder.Services.AddSingleton<SaluteSpeechManager>();

    }

    public override void ConfigureWebApplication(WebApplication app, PluginSettings settings)
    {
        app.Services.AutoHostRegisterHelper([GetType().Assembly, typeof(SaluteSpeechSynthesisNode).Assembly]);

        var logger = MarsLogger.GetStaticLogger<MainSberDevApiPlugin>();

        //logger.LogWarning($"> {PluginPackageName} - Work!!!!2" + Locale.Username);

        var op = app.Services.GetRequiredService<IOptionService>();

#if DEBUG
        app.UseDevelopingServePluginFilesDefinition(this.GetType().Assembly, settings, [typeof(SberDevApiPluginFront).Assembly, GetType().Assembly]);
#endif

        //op.RegisterOption<Example1Plugin1>(appendToInitialSiteData: true);
        //op.SetConstOption(new Example1PluginConstOptionForFront() { ForFrontValue = "123" }, appendToInitialSiteData: true);
    }

}
