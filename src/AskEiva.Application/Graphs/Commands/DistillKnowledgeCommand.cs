using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services;
using MediatR;

namespace AskEiva.Application.Graphs.Commands;

// The request contract matching MediatR syntax
public record DistillKnowledgeCommand(int BatchSize) : IRequest<DistillResult>;

// The return payload for tracking execution metrics
public record DistillResult(int TriplesExtracted, int TicketsProcessed, string Message);

public class DistillKnowledgeCommandHandler : IRequestHandler<DistillKnowledgeCommand, DistillResult>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IGraphRepository _graphRepository;
    private readonly IExtractionEngine _extractionEngine;

    public DistillKnowledgeCommandHandler(
        ITicketRepository ticketRepository,
        IGraphRepository graphRepository,
        IExtractionEngine extractionEngine)
    {
        _ticketRepository = ticketRepository;
        _graphRepository = graphRepository;
        _extractionEngine = extractionEngine;
    }

    public async Task<DistillResult> Handle(DistillKnowledgeCommand request, CancellationToken cancellationToken)
    {
        // 1. Grab un-distilled ticket payload blocks from Weaviate
        var pendingTickets = await _ticketRepository.GetUnprocessedTicketsAsync(request.BatchSize);
        
        int processedCount = 0;
        int totalTriplesCreated = 0;

        foreach (var ticket in pendingTickets)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // 2. Offload to the extracted execution engine (Mistral LLM abstraction)
            var triples = await _extractionEngine.ExtractTriplesAsync(ticket.Content, ticket.SourceId);
            
            if (triples.Any())
            {
                // 3. Update the high-performance Neural Map collection in Weaviate
                await _graphRepository.SaveTriplesAsync(triples);
                totalTriplesCreated += triples.Count();
            }

            // 4. Update state flag to eliminate processing collisions
            await _ticketRepository.MarkAsDistilledAsync(ticket.SourceId);
            processedCount++;
        }

        return new DistillResult(
            totalTriplesCreated, 
            processedCount, 
            "Knowledge distillation batch run completed successfully."
        );
    }
}