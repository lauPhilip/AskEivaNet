namespace AskEiva.Domain.Entities;

public class KnowledgeTriple
{
    public string Id { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Predicate { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public string SourceTicketId { get; set; } = string.Empty;
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
}