using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Repositories;

public interface IDocumentationRepository
{
    Task UpsertDocumentationAsync(DocumentationNode docNode);
}