using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services;
using MediatR;

namespace AskEiva.Application.ReleaseNotes.Commands;

public record IngestReleaseNotesCommand : IRequest<IngestResultDto>;

public record IngestResultDto(bool IsSuccess, int TotalChunksIngested, string StatusDetails);

public class IngestReleaseNotesCommandHandler : IRequestHandler<IngestReleaseNotesCommand, IngestResultDto>
{
    private readonly IReleaseNotesScraper _scraper;
    private readonly IKnowledgeRetrievalRepository _retrievalRepository;

    public IngestReleaseNotesCommandHandler(IReleaseNotesScraper scraper, IKnowledgeRetrievalRepository retrievalRepository)
    {
        _scraper = scraper;
        _retrievalRepository = retrievalRepository;
    }

    public async Task<IngestResultDto> Handle(IngestReleaseNotesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Trigger the fully dynamic web scraping matrix loop
            var allDiscoveredNodes = await _scraper.ScrapeAndChunkAllReleaseNotesAsync();
            var nodesList = allDiscoveredNodes.ToList();

            if (!nodesList.Any())
            {
                return new IngestResultDto(true, 0, "All available release note documents are up to date. No new records found.");
            }

            // 2. Filter nodes on-the-fly to enforce strict delta updates
            var uniqueGroupedVersions = nodesList
                .Select(n => new { n.Product, n.Version })
                .Distinct()
                .ToList();

            var newNodesToIngest = new System.Collections.Generic.List<AskEiva.Domain.Entities.SoftwareReleaseNode>();

            foreach (var versionGroup in uniqueGroupedVersions)
            {
                // Check Weaviate dynamically using our fast GraphQL lookups to block duplicates
                bool alreadyIndexed = await _retrievalRepository.DoesProductVersionExistAsync(versionGroup.Product, versionGroup.Version);
                
                if (!alreadyIndexed)
                {
                    // Isolate and add only missing data nodes to the queue
                    var targetChunks = nodesList.Where(n => n.Product == versionGroup.Product && n.Version == versionGroup.Version);
                    newNodesToIngest.AddRange(targetChunks);
                }
            }

            if (!newNodesToIngest.Any())
            {
                return new IngestResultDto(true, 0, "Delta Verification Passed: All crawled versions already exist inside the vector database storage.");
            }

            // 3. Batch commit only the new data nodes to Weaviate
            await _retrievalRepository.BatchIngestReleaseNodesAsync(newNodesToIngest);

            return new IngestResultDto(
                IsSuccess: true,
                TotalChunksIngested: newNodesToIngest.Count,
                StatusDetails: $"Dynamic Sync Completed: Ingested {newNodesToIngest.Count} fresh text nodes across discovered update tracks."
            );
        }
        catch (Exception ex)
        {
            return new IngestResultDto(false, 0, $"Ingestion pipeline failure trace: {ex.Message}");
        }
    }
}