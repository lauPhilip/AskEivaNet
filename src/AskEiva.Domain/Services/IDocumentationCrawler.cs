using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Services;

public interface IDocumentationCrawler
{
    Task<IEnumerable<DocumentationNode>> CrawlSolutionsAsync(string categoryId);
}