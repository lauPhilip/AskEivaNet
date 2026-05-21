namespace AskEiva.Domain.ValueObjects;

public record TextChunk(
    string ChunkId,
    string SourceId,
    string Content,
    int SequenceNumber,
    List<string> ImageUrls,
    Dictionary<string, string> Metadata
);