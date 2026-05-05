using Mars.Plugin.Front;
using Mars.Plugin.Front.Abstractions;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Mars.SberDevApiPlugin.Front;

public class SberDevApiPluginFront : IWebAssemblyPluginFront
{
    public void ConfigureServices(WebAssemblyHostBuilder builder)
    {
        Console.WriteLine("> plugin ConfigureServices!");

        //NodesLocator.RegisterAssembly(typeof(TelegramSenderNode).Assembly);
        //NodeFormsLocator.RegisterAssembly(typeof(TelegramSenderNodeForm).Assembly);
    }

    public void ConfigureApplication(WebAssemblyHost app)
    {
        app.Services.AutoFrontRegisterHelper([GetType().Assembly]);

        //Console.WriteLine("> plugin ConfigureApplication!" + Locale.Username);
    }
}
