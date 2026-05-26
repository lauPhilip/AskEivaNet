namespace AskEiva.Domain.ValueObjects;

public record RetrievalMatch(
    string SourceId,
    string Title,
    string Content,
    string SourceUrl,
    float ConfidenceScore,
    string SourceType, // "Ticket", "Documentation", or "ReleaseNote"
    List<string> ImageUrls,
    string ProductContext = "", 
    string VersionContext = ""  
);