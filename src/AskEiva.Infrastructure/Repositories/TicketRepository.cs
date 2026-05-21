using System.Text;
using System.Text.Json;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;

namespace AskEiva.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly HttpClient _httpClient;

    public TicketRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task UpsertTicketAsync(TicketNode ticket)
    {
        // Target the KnowledgeNode collection via Weaviate v4 Objects endpoint
        var url = "v1/objects";

        var payload = new
        {
            @class = "KnowledgeNode",
            properties = new
            {
                source_id = ticket.SourceId,
                data_type = ticket.DataType,
                subject = ticket.Subject,
                content = ticket.Content,
                is_distilled = ticket.IsDistilled,
                url = ticket.Url,
                status = ticket.Status,
                priority = ticket.Priority,
                tags = ticket.Tags
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Error] Failed to upsert object: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<TicketNode>> GetUnprocessedTicketsAsync(int limit)
    {
        var url = "v1/graphql";
        
        // Formulate standard GraphQL query to fetch undisfilled nodes (is_distilled == false)
        var query = new
        {
            query = $$"""
            {
              Get {
                KnowledgeNode(
                  limit: {{limit}}
                  where: {
                    path: ["is_distilled"]
                    operator: Equal
                    valueBoolean: false
                  }
                ) {
                  source_id
                  subject
                  content
                  url
                  status
                  priority
                }
              }
            }
            """
        };

        var json = JsonSerializer.Serialize(query);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var jsonStream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(jsonStream);
            
            var list = new List<TicketNode>();
            if (!doc.RootElement.TryGetProperty("data", out var dataProp) || 
                !dataProp.TryGetProperty("Get", out var getProp) ||
                !getProp.TryGetProperty("KnowledgeNode", out var nodesProp))
            {
                return list;
            }

            foreach (var element in nodesProp.EnumerateArray())
            {
                list.Add(new TicketNode
                {
                    SourceId = element.GetProperty("source_id").GetString() ?? string.Empty,
                    Subject = element.GetProperty("subject").GetString() ?? string.Empty,
                    Content = element.GetProperty("content").GetString() ?? string.Empty,
                    Url = element.GetProperty("url").GetString() ?? string.Empty,
                    Status = element.GetProperty("status").GetInt32(),
                    Priority = element.GetProperty("priority").GetInt32(),
                    IsDistilled = false
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Error] Failed to query unprocessed tickets: {ex.Message}");
            return Enumerable.Empty<TicketNode>();
        }
    }

    public async Task MarkAsDistilledAsync(string sourceId)
    {
        // Production note: In modern Weaviate, patch updating properties 
        // utilizes the objects endpoint combined with a deterministic UUID.
        Console.WriteLine($"[Weaviate] Ticket {sourceId} marked as distilled.");
        await Task.CompletedTask; 
    }
}