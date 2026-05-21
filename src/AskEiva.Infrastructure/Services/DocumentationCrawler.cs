using System.Text.Json;
using System.Text.RegularExpressions;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;

namespace AskEiva.Infrastructure.Services;

public class DocumentationCrawler : IDocumentationCrawler
{
    private readonly HttpClient _httpClient;
    private static readonly Regex PdfLinkRegex = new(@"href=[""']([^""']+\.pdf)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public DocumentationCrawler(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<DocumentationNode>> CrawlSolutionsAsync(string categoryId)
    {
        // Freshdesk Solutions API endpoint for public articles
        var url = $"solutions/categories/{categoryId}/folders";
        var nodes = new List<DocumentationNode>();

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return nodes;

            var jsonStream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(jsonStream);

            foreach (var folder in doc.RootElement.EnumerateArray())
            {
                var folderName = folder.GetProperty("name").GetString() ?? "General";
                var folderId = folder.GetProperty("id").GetInt64();

                // Fetch articles contained within this manual directory (e.g., NaviScan 8)
                var articlesUrl = $"solutions/folders/{folderId}/articles";
                var articlesResponse = await _httpClient.GetAsync(articlesUrl);
                if (!articlesResponse.IsSuccessStatusCode) continue;

                var articlesStream = await articlesResponse.Content.ReadAsStreamAsync();
                using var articlesDoc = await JsonDocument.ParseAsync(articlesStream);

                foreach (var article in articlesDoc.RootElement.EnumerateArray())
                {
                    var bodyHtml = article.GetProperty("body").GetString() ?? string.Empty;
                    var articleId = article.GetProperty("id").GetInt64();

                    // Track explicitly embedded technical drawings or product manuals
                    var pdfAttachments = ExtractPdfLinks(bodyHtml);

                    nodes.Add(new DocumentationNode
                    {
                        SourceId = $"doc_{articleId}",
                        Title = article.GetProperty("title").GetString() ?? "Untitled Guide",
                        Category = folderName,
                        Content = bodyHtml, // Handed off to Domain.Utilities.TextSplitter for structural cleaning
                        SourceUrl = $"https://eiva.freshdesk.com/support/solutions/articles/{articleId}",
                        AssociatedPdfUrls = pdfAttachments
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Documentation Crawl Error] Failed processing solution array: {ex.Message}");
        }

        return nodes;
    }

    private List<string> ExtractPdfLinks(string htmlContent)
    {
        var pdfs = new List<string>();
        var matches = PdfLinkRegex.Matches(htmlContent);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                pdfs.Add(match.Groups[1].Value);
            }
        }
        return pdfs;
    }
}