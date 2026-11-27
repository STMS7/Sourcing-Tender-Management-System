namespace Sourcing_Tender_Management_System.Services.AiIntelligence;


//Turns text into numbers. Similar meaning gives similar numbers.
public class EmbeddingClient(HttpClient http)
{
    private record EmbedResponse(float[][] Embeddings);

    public virtual async Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("api/embed", new { model = "nomic-embed-text", input = texts }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<EmbedResponse>(ct);
        return body!.Embeddings;
    }
}