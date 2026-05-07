using GigaChatAPI;

namespace SberDevApiPluginConsoleApp;

internal static class GigaChatTest
{
    public static async Task Main()
    {
        var authKey = Environment.GetEnvironmentVariable("GigaChatAPI_AuthKey") ?? throw new ArgumentNullException("GigaChatAPI_AuthKey");

        using var client = new GigaChatClient(authKey);

        try
        {
            var models = await client.GetModelsAsync();
            foreach (var model in models)
            {
                Console.WriteLine($"- {model.Id} (owned by: {model.OwnedBy})");
            }

            // First call (cache miss)
            Console.WriteLine("\n=== First call (cache miss) ===");
            var response1 = await client.SendMessageAsync(
                "Привет! Расскажи анекдот про программистов."
            );
            Console.WriteLine("Response: " + response1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
