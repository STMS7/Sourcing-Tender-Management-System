namespace Sourcing_Tender_Management_System.Services.AiAssistant;

public static class AiAssistantServiceCollectionExtensions
{
    /// <summary>Registers the bid analysis assistant and its local Ollama client (one line in Program.cs).</summary>
    public static IServiceCollection AddBidAnalysisAssistant(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.Section));
        var settings = configuration.GetSection(OllamaOptions.Section).Get<OllamaOptions>() ?? new OllamaOptions();
        services.AddHttpClient<IOllamaClient, OllamaClient>(http =>
        {
            http.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            // The service applies its own timeout; this is only a safety net.
            http.Timeout = TimeSpan.FromSeconds(Math.Max(10, settings.TimeoutSeconds) + 30);
        });
        services.AddScoped<IBidAnalysisService, BidAnalysisService>();
        return services;
    }
}
