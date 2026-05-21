using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;

namespace AskEiva.Infrastructure.Services;

public class FreshdeskService : IFreshdeskService
{
    private readonly HttpClient _httpClient;
    private const string ConversationEndpoint = "tickets/{0}/conversations";

    public FreshdeskService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<TicketNode>> FetchTicketsAsync(int page, int perPage, DateTime? updatedSince = null)
    {
        var url = $"tickets?page={page}&per_page={perPage}&include=description";
        
        if (updatedSince.HasValue)
        {
            url += $"&updated_since={updatedSince.Value:yyyy-MM-ddTHH:mm:ssZ}";
        }

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonStream = await response.Content.ReadAsStreamAsync();
            var rawTickets = await JsonSerializer.DeserializeAsync<List<JsonElement>>(jsonStream);

            var ticketNodes = new List<TicketNode>();
            if (rawTickets == null) return ticketNodes;

            foreach (var element in rawTickets)
            {
                var id = element.GetProperty("id").GetInt64();
                var descriptionHtml = element.TryGetProperty("description", out var descProp) ? descProp.GetString() : "";
                
                // Fetch the sub-content dialogue thread for full context
                var dialogueThread = await FetchTicketConversationsAsync(id);

                ticketNodes.Add(new TicketNode
                {
                    SourceId = $"ticket_{id}",
                    Subject = element.TryGetProperty("subject", out var subjProp) ? subjProp.GetString() ?? "No Subject" : "No Subject",
                    Content = $"Initial Request:\n{descriptionHtml}\n\nDialogue Thread:\n{dialogueThread}",
                    Status = element.TryGetProperty("status", out var statProp) ? statProp.GetInt32() : 2,
                    Priority = element.TryGetProperty("priority", out var prioProp) ? prioProp.GetInt32() : 1,
                    Url = $"{_httpClient.BaseAddress?.ToString().Replace("/api/v2", "")}/a/tickets/{id}"
                });
            }

            return ticketNodes;
        }
        catch (Exception ex)
        {
            // Production grade note: In the next step, we'll swap Console with ILogger
            Console.WriteLine($"[Error] Failed to fetch tickets from Freshdesk: {ex.Message}");
            return Enumerable.Empty<TicketNode>();
        }
    }

    public async Task<string> FetchTicketConversationsAsync(long ticketId)
    {
        var url = string.Format(ConversationEndpoint, ticketId);
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return string.Empty;

            var jsonStream = await response.Content.ReadAsStreamAsync();
            var conversations = await JsonSerializer.DeserializeAsync<List<JsonElement>>(jsonStream);
            
            if (conversations == null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var comment in conversations)
            {
                var body = comment.TryGetProperty("body_text", out var bodyProp) ? bodyProp.GetString() : "";
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var type = comment.TryGetProperty("private", out var privProp) && privProp.GetBoolean() 
                        ? "Private Note" 
                        : "Reply";
                    
                    sb.AppendLine($"\n**[{type}]**\n{body}");
                }
            }
            return sb.ToString();
        }
        catch
        {
            return string.Empty; // Fail gracefully on individual thread crawls
        }
    }
}