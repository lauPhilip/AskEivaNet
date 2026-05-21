namespace AskEiva.Domain.Entities;

public class DocumentationNode
{
    public string SourceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // e.g., "NaviPac", "NaviScan"
    public string Content { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public List<string> AssociatedPdfUrls { get; set; } = new();
    public DateTime CrawledAt { get; set; } = DateTime.UtcNow;
}