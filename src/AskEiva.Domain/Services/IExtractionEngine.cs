using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Services;

public interface IExtractionEngine
{
    Task<IEnumerable<KnowledgeTriple>> ExtractTriplesAsync(string text, string sourceId);
}