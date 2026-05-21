using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;

namespace AskEiva.Infrastructure.Services;

public class MistralExtractionEngine : IExtractionEngine
{
    private readonly HttpClient _httpClient;

    public MistralExtractionEngine(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<KnowledgeTriple>> ExtractTriplesAsync(string text, string sourceId)
    {
        var url = "v1/chat/completions";

        // System prompt to force Mistral into strict, clean JSON Graph extraction mode
        var systemPrompt = "You are an advanced knowledge graph extraction system. " +
                           "Analyze the provided technical support text and extract relationships as a JSON array of triples. " +
                           "Each object in the array MUST contain exactly three fields: 'subject', 'predicate', and 'object'. " +
                           "Keep terms concise, technical, and relevant to EIVA software/hardware configurations.";

        var payload = new
        {
            model = "mistral-large-latest",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Extract triples from this text:\n\n{text}" }
            },
            // Enforce structured JSON schema output natively at the API boundary
            response_format = new { type = "json_object" }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var jsonStream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(jsonStream);
            
            // Navigate the standard OpenAI/Mistral response matrix
            var root = doc.RootElement;
            var choiceText = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            if (string.IsNullOrWhiteSpace(choiceText)) return Enumerable.Empty<KnowledgeTriple>();

            // Parse the inner JSON block returned by the model
            using var innerDoc = JsonDocument.Parse(choiceText);
            var list = new List<KnowledgeTriple>();

            // Mistral wraps the object, look for an array inside
            if (innerDoc.RootElement.TryGetProperty("triples", out var triplesArray) || 
                innerDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var targetArray = innerDoc.RootElement.ValueKind == JsonValueKind.Array ? innerDoc.RootElement : triplesArray;

                foreach (var item in targetArray.EnumerateArray())
                {
                    list.Add(new KnowledgeTriple
                    {
                        Id = Guid.NewGuid().ToString(),
                        Subject = item.GetProperty("subject").GetString() ?? string.Empty,
                        Predicate = item.GetProperty("predicate").GetString() ?? string.Empty,
                        Object = item.GetProperty("object").GetString() ?? string.Empty,
                        SourceTicketId = sourceId,
                        ExtractedAt = DateTime.UtcNow
                    });
                }
            }

            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LLM Extraction Error] Failed parsing graph matrix for ticket {sourceId}: {ex.Message}");
            return Enumerable.Empty<KnowledgeTriple>();
        }
    }
}