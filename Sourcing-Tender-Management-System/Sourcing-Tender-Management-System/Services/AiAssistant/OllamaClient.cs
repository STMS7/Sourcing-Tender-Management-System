using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Sourcing_Tender_Management_System.Services.AiAssistant;

/// <summary>Settings from the "Ollama" section of appsettings.json.</summary>
public class OllamaOptions
{
    public const string Section = "Ollama";
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5:7b";
    public int TimeoutSeconds { get; set; } = 120;
    /// <summary>Context window in tokens. 8192 fits a 7B model on an 8 GB GPU.</summary>
    public int ContextTokens { get; set; } = 8192;
    public double Temperature { get; set; } = 0.1;
    /// <summary>Maximum tokens the model may write. Output length is most of the wait, so keep it short.</summary>
    public int MaxOutputTokens { get; set; } = 600;
}

public interface IOllamaClient
{
    string ModelName { get; }
    /// <summary>Sends one chat turn and returns the model's raw JSON text. Throws on network or server errors.</summary>
    Task<string> ChatJsonAsync(string system, string user, object jsonSchema, CancellationToken ct);
}

/// <summary>Minimal client for a local Ollama server (POST /api/chat, no streaming, structured JSON output).</summary>
public class OllamaClient(HttpClient http, IOptions<OllamaOptions> options) : IOllamaClient
{
    private readonly OllamaOptions settings = options.Value;
    public string ModelName => settings.Model;

    public async Task<string> ChatJsonAsync(string system, string user, object jsonSchema, CancellationToken ct)
    {
        //It builds the request body sent to Ollama:
        var request = new ChatRequest(
            settings.Model,
            [new ChatMessage("system", system), new ChatMessage("user", user)],
            Stream: false,
            Format: jsonSchema,
            Options: new ChatOptions(settings.Temperature, settings.ContextTokens, settings.MaxOutputTokens),
            KeepAlive: "10m");

        //Sends the request to Ollama and waits for the response
        using var response = await http.PostAsJsonAsync("api/chat", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Ollama returned {(int)response.StatusCode}: {body}");
        }
        var reply = await response.Content.ReadFromJsonAsync<ChatResponse>(ct);
        return reply?.Message?.Content ?? throw new InvalidOperationException("Ollama returned an empty answer.");
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] ChatMessage[] Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("format")] object Format,
        [property: JsonPropertyName("options")] ChatOptions Options,
        [property: JsonPropertyName("keep_alive")] string KeepAlive);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatOptions(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("num_ctx")] int NumCtx,
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private sealed record ChatResponse([property: JsonPropertyName("message")] ChatMessage? Message);
}
