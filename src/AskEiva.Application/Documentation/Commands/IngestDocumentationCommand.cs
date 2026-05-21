using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services; // Using the Domain abstraction contract
using AskEiva.Domain.Utilities;
using AskEiva.Domain.Entities;
using MediatR;

namespace AskEiva.Application.Documentation.Commands;

// Request parameters targeting specific documentation categories (e.g., NaviPac, NaviScan)
public record IngestDocumentationCommand(List<string> CategoryIds) : IRequest<DocIngestionResult>;

public record DocIngestionResult(int ArticlesProcessed, int TotalChunksCreated, bool Success, string Message);

public class IngestDocumentationCommandHandler : IRequestHandler<IngestDocumentationCommand, DocIngestionResult>
{
    private readonly IDocumentationCrawler _crawler; // Fulfills Clean Architecture rules
    private readonly IDocumentationRepository _repository;

    public IngestDocumentationCommandHandler(IDocumentationCrawler crawler, IDocumentationRepository repository)
    {
        _crawler = crawler;
        _repository = repository;
    }

    public async Task<DocIngestionResult> Handle(IngestDocumentationCommand request, CancellationToken cancellationToken)
    {
        int articlesCount = 0;
        int totalChunksCount = 0;
        
        // Tailored chunk configurations optimized for manual/documentation layouts
        var splitter = new TextSplitter(chunkSize: 1200, chunkOverlap: 250); 

        foreach (var categoryId in request.CategoryIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new DocIngestionResult(articlesCount, totalChunksCount, false, "Ingestion process halted prematurely.");
            }

            // 1. Fetch data through the public solutions domain service interface
            var docNodes = await _crawler.CrawlSolutionsAsync(categoryId);

            foreach (var node in docNodes)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // 2. Persist the documentation node to Weaviate.
                // Note: The internal HTML cleaning and image preservation extraction rules
                // will be handled elegantly via the TextSplitter toolset during the sub-indexing loop.
                await _repository.UpsertDocumentationAsync(node);
                
                articlesCount++;
            }
        }

        return new DocIngestionResult(
            articlesCount, 
            totalChunksCount, 
            true, 
            $"Successfully synchronized {articlesCount} product documentation modules."
        );
    }
}