namespace AskEiva.Domain.ValueObjects;

public record RetrievalMatch(
    string SourceId,
    string Title,
    string Content,
    string SourceUrl,
    float ConfidenceScore,
    string SourceType, // "Ticket" or "Documentation"
    List<string> ImageUrls
);