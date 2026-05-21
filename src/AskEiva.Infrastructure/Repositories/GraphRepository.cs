using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;

namespace AskEiva.Infrastructure.Repositories;

public class GraphRepository : IGraphRepository
{
    private readonly HttpClient _httpClient;

    public GraphRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

public async Task<IEnumerable<KnowledgeTriple>> GetAllTriplesAsync()
    {
        // 💡 FIXED: Queried 'evidence_id' instead of 'source_ticket_id', and dropped the missing 'extracted_at'
        var query = new
        {
            query = "{ Get { EntityGraph { subject predicate object evidence_id } } }"
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("v1/graphql", query);
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<KnowledgeTriple>();

            var rawJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(rawJson);
            
            if (doc.RootElement.TryGetProperty("data", out var dataProp) &&
                dataProp.TryGetProperty("Get", out var getProp) &&
                getProp.TryGetProperty("EntityGraph", out var graphArray) &&
                graphArray.ValueKind == JsonValueKind.Array)
            {
                var triples = new List<KnowledgeTriple>();
                foreach (var item in graphArray.EnumerateArray())
                {
                    string sub = item.TryGetProperty("subject", out var s) ? s.GetString() ?? "" : "";
                    string pred = item.TryGetProperty("predicate", out var p) ? p.GetString() ?? "" : "";
                    string obj = item.TryGetProperty("object", out var o) ? o.GetString() ?? "" : "";
                    
                    // 💡 FIXED: Map 'evidence_id' down to your domain model's tracking property
                    string evidenceId = item.TryGetProperty("evidence_id", out var evId) ? evId.GetString() ?? "" : "";

                    triples.Add(new KnowledgeTriple
                    {
                        Subject = sub,
                        Predicate = pred,
                        Object = obj,
                        SourceTicketId = evidenceId, // Safely paths the database ID reference
                        ExtractedAt = DateTime.UtcNow // Fallback runtime stamp since it's not in the schema
                    });
                }
                return triples;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Graph Error] Failed to read semantic vector triples: {ex.Message}");
        }

        return Enumerable.Empty<KnowledgeTriple>();
    }

    // --- EXISTING SAVE ARRANGEMENT PIPELINE ---
    public async Task SaveTriplesAsync(IEnumerable<KnowledgeTriple> triples)
    {
        var url = "v1/objects";

        foreach (var triple in triples)
        {
            var payload = new
            {
                @class = "EntityGraph",
                properties = new
                {
                    subject = triple.Subject,
                    predicate = triple.Predicate,
                    @object = triple.Object,
                    source_ticket_id = triple.SourceTicketId,
                    extracted_at = triple.ExtractedAt.ToString("o")
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
                Console.WriteLine($"[Weaviate Graph Error] Failed to write semantic triple connection: {ex.Message}");
            }
        }
    }
}