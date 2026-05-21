using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services;
using AskEiva.Domain.Utilities;
using MediatR;

namespace AskEiva.Application.Tickets.Commands;

// This defines what our request looks like (Input parameters)
public record IngestTicketsCommand(int StartPage, int MaxPages) : IRequest<IngestionResult>;

// The result blueprint returned to the UI or Worker
public record IngestionResult(int TotalIngested, bool CompletedGracefully, string Message);

// The actual business execution engine
public class IngestTicketsCommandHandler : IRequestHandler<IngestTicketsCommand, IngestionResult>
{
    private readonly IFreshdeskService _freshdeskService;
    private readonly ITicketRepository _ticketRepository;

    public IngestTicketsCommandHandler(IFreshdeskService freshdeskService, ITicketRepository ticketRepository)
    {
        _freshdeskService = freshdeskService;
        _ticketRepository = ticketRepository;
    }

    public async Task<IngestionResult> Handle(IngestTicketsCommand request, CancellationToken cancellationToken)
    {
        int pageNum = request.StartPage;
        int totalIngested = 0;
        int consecutiveEmptyPages = 0;
        const int perPage = 100; // Efficient bulk size for modern .NET processing

        while (pageNum < (request.StartPage + request.MaxPages))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new IngestionResult(totalIngested, false, "Ingestion suspended by system request.");
            }

            // 1. Fetch from the Freshdesk abstraction layer
            var tickets = await _freshdeskService.FetchTicketsAsync(pageNum, perPage);

            if (tickets == null || !tickets.Any())
            {
                consecutiveEmptyPages++;
                if (consecutiveEmptyPages >= 2)
                {
                    return new IngestionResult(totalIngested, true, $"Reached historical archive baseline at page {pageNum}.");
                }
                pageNum++;
                continue;
            }

            consecutiveEmptyPages = 0;

            // 2. Stream & Process straight into Weaviate
            var splitter = new TextSplitter(chunkSize: 1000, chunkOverlap: 200);

            foreach (var ticket in tickets)
            {
                // Break the raw ticket dialogue down into optimized vector chunks
                var chunks = splitter.SplitTicket(ticket);
                
                foreach (var chunk in chunks)
                {
                    // Production Note: In the next step, we will pass the sub-chunks 
                    // to our repository, allowing Weaviate to generate precise node embeddings
                    // while preserving their metadata and inline image schemas.
                    
                    // await _ticketRepository.UpsertChunkAsync(chunk);
                }
                
                totalIngested++;
            }

            // 3. Cooperative backoff to respect Freshdesk API limits and give Weaviate breathing room
            await Task.Delay(5000, cancellationToken);
            pageNum++;
        }

        return new IngestionResult(totalIngested, true, $"Successfully completed processing up to page {pageNum - 1}.");
    }
}