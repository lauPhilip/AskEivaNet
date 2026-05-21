using System.IO;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AskEiva.Domain.Services;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;

namespace AskEiva.Infrastructure.Services;

public class MistralChatService : IMistralChatService
{
    private readonly HttpClient _httpClient;
    private const string MistralModel = "mistral-large-latest";

    public MistralChatService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<string> GenerateStreamingChatResponseAsync(
        string userQuestion, 
        IEnumerable<RetrievalMatch> semanticContext, 
        IEnumerable<KnowledgeTriple> structuralGraph,
        IEnumerable<ChatTurn> conversationHistory)
    {
        // 1. Compile your standard vector retrieval contexts
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("=== RETRIEVED REFERENCE CONTEXT ===");
        foreach (var match in semanticContext)
        {
            contextBuilder.AppendLine($"[{match.SourceType}] Title: {match.Title}\nContent: {match.Content}\n---");
        }

        // 2. Build the message array dynamically to include historical context
        var messagesList = new List<object>();

        // System instructions remain locked at position zero
        messagesList.Add(new { 
            role = "system", 
            content = "You are AskEIVA, an expert automated customer support engineer. Synthesize the provided support tickets, technical documentation, and conversation history to answer questions. Format outputs using Markdown and Markdown tables where appropriate." 
        });

        // Loop through and append rolling history turns chronologically
        foreach (var turn in conversationHistory)
        {
            messagesList.Add(new { 
                role = turn.IsUser ? "user" : "assistant", 
                content = turn.MessageText 
            });
        }

        // Finally, add the current query accompanied by the freshly harvested Weaviate data vectors
        messagesList.Add(new { 
            role = "user", 
            content = $"Context Data:\n{contextBuilder}\n\nNew User Question: {userQuestion}" 
        });

        var payload = new
        {
            model = MistralModel,
            messages = messagesList.ToArray(),
            temperature = 0.2,
            stream = true
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        
        if (!response.IsSuccessStatusCode)
        {
            yield return $"Mistral Stream Failure: {response.StatusCode}";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

            var data = line.Substring(6).Trim();
            if (data == "[DONE]") break;

            MistralStreamChunk? chunk = null;
            try { chunk = JsonSerializer.Deserialize<MistralStreamChunk>(data); } catch { continue; }

            var textChunk = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(textChunk))
            {
                yield return textChunk;
            }
        }
    }
}

// --- STREAMING-SPECIFIC DESERIALIZATION SCHEMAS ---
public class MistralStreamChunk
{
    [JsonPropertyName("choices")] public List<MistralStreamChoice>? Choices { get; set; }
}
public class MistralStreamChoice
{
    [JsonPropertyName("delta")] public MistralStreamDelta? Delta { get; set; }
}
public class MistralStreamDelta
{
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
}