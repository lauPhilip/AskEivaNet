using System.Text;
using System.Text.Json;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;

namespace AskEiva.Infrastructure.Repositories;

public class DocumentationRepository : IDocumentationRepository
{
    private readonly HttpClient _httpClient;

    public DocumentationRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task UpsertDocumentationAsync(DocumentationNode docNode)
    {
        var url = "v1/objects";

        var payload = new
        {
            @class = "DocumentationNode",
            properties = new
            {
                source_id = docNode.SourceId,
                title = docNode.Title,
                category = docNode.Category,
                content = docNode.Content,
                source_url = docNode.SourceUrl,
                pdf_links = docNode.AssociatedPdfUrls,
                crawled_at = docNode.CrawledAt.ToString("o")
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
    }
}