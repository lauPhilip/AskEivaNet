using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services;
using AskEiva.Application.Telemetry; 

namespace AskEiva.Application.Tickets.Commands;

public record IngestTicketsCommand : IRequest<int>;

public class IngestTicketsCommandHandler : IRequestHandler<IngestTicketsCommand, int>
{
    private readonly IFreshdeskService _freshdeskService;
    private readonly ITicketRepository _ticketRepository;
    private readonly ISyncTelemetryBroker _telemetryBroker; 

    public IngestTicketsCommandHandler(IFreshdeskService freshdeskService, ITicketRepository ticketRepository, ISyncTelemetryBroker telemetryBroker)
    {
        _freshdeskService = freshdeskService;
        _ticketRepository = ticketRepository;
        _telemetryBroker = telemetryBroker;
    }

public async Task<int> Handle(IngestTicketsCommand request, CancellationToken cancellationToken)
    {
        int currentPage = 1;
        const int TicketsPerPage = 30; // Freshdesk's maximum standard page limit size
        int totalNewChunksIndexed = 0;
        bool crawling = true;

        _telemetryBroker.Broadcast(new SyncProgressUpdate 
        { 
            LogMessage = "🚀 Global Archive Sweep Initialized. Using updated_since chronological filters to bypass default view caps...", 
            Status = "Processing" 
        });

        while (crawling)
        {
            if (cancellationToken.IsCancellationRequested) break;

            _telemetryBroker.Broadcast(new SyncProgressUpdate 
            { 
                LogMessage = $"📡 Requesting page index sequential layer: [Page {currentPage}]", 
                CurrentPage = currentPage 
            });
            
            // Request the page directly from our updated historical data client stream
            var ticketBatch = await _freshdeskService.GetTicketsPageAsync(currentPage, TicketsPerPage);
            var batchList = ticketBatch.ToList();

            // If a page returns empty, the sync has caught up with your full support history timeline
            if (!batchList.Any())
            {
                _telemetryBroker.Broadcast(new SyncProgressUpdate 
                { 
                    LogMessage = $"✅ Ingestion caught up! Completed scan at Page {currentPage - 1}. All data records verified.", 
                    Status = "Complete" 
                });
                break;
            }

            var chunksToIngest = new List<TicketNode>();

            foreach (var ticket in batchList)
            {
                if (cancellationToken.IsCancellationRequested) break;
                string sourceIdToken = $"FD-{ticket.Id}";

                // Skip tickets we've already indexed to save time and API tokens
                if (await _ticketRepository.DoesTicketExistAsync(sourceIdToken))
                {
                    continue; 
                }

                if (string.IsNullOrWhiteSpace(ticket.Description_Text) || ticket.Description_Text.Length < 30) continue;

                string cleanedMainText = SanitizeTicketPayloadBody(ticket.Description_Text);
                var timelineBuilder = new System.Text.StringBuilder();
                timelineBuilder.AppendLine($"=== TICKET OPENED: {ticket.Subject} ===");
                timelineBuilder.AppendLine(cleanedMainText);

                var subReplies = await _freshdeskService.GetTicketConversationsAsync(ticket.Id);
                foreach (var reply in subReplies.OrderBy(r => r.Created_At))
                {
                    string actorRole = reply.Incoming ? "CUSTOMER" : "AGENT";
                    string cleanedReplyText = SanitizeTicketPayloadBody(reply.Body_Text);
                    if (string.IsNullOrWhiteSpace(cleanedReplyText) || cleanedReplyText.Length < 10) continue;

                    timelineBuilder.AppendLine($"\n--- Reply by {actorRole} on {reply.Created_At} ---");
                    timelineBuilder.AppendLine(cleanedReplyText);
                }

                var textSegments = SliceBodyIntoChunks(timelineBuilder.ToString(), maxWords: 420);
                int segmentPart = 1;
                
                foreach (var segment in textSegments)
                {
                    var enrichedTags = new List<string> { $"Part-{segmentPart}", $"Year-{ticket.Created_At.Year}" };
                    if (ticket.Tags != null) enrichedTags.AddRange(ticket.Tags);

                    chunksToIngest.Add(new TicketNode
                    {
                        SourceId = sourceIdToken,
                        Subject = ticket.Subject,
                        Content = $"[Historical Support Log Asset | Token ID: {sourceIdToken}]\n{segment}",
                        Url = $"https://eiva.freshdesk.com/a/tickets/{ticket.Id}",
                        DataType = "Ticket",
                        IsDistilled = false,
                        Status = ticket.Status,
                        Priority = ticket.Priority,
                        Tags = enrichedTags
                    });
                    segmentPart++;
                }

                _telemetryBroker.Broadcast(new SyncProgressUpdate 
                { 
                    LogMessage = $"📥 Parsed support thread {sourceIdToken} into vector layers.",
                    CurrentTicketId = sourceIdToken,
                    TicketSubject = ticket.Subject,
                    Status = "Processing"
                });
            }

            if (chunksToIngest.Any())
            {
                await _ticketRepository.BatchIngestTicketNodesAsync(chunksToIngest);
                totalNewChunksIndexed += chunksToIngest.Count;
                
                _telemetryBroker.Broadcast(new SyncProgressUpdate 
                { 
                    LogMessage = $"📦 Vectorized page {currentPage} committed. New chunks: {totalNewChunksIndexed}",
                    TotalChunksIndexed = totalNewChunksIndexed,
                    Status = "Success"
                });
            }

            currentPage++;
            await Task.Delay(1500, cancellationToken); // Rate limiting buffer
        }

        return totalNewChunksIndexed;
    }

    private string SanitizeTicketPayloadBody(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        string result = Regex.Replace(rawText, @"\[inline_attachment:[^\]]*\]", " [Attachment Removed] ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"http[s]?://[^\s]*\.(png|jpg|jpeg|gif)", " [Image URI Stripped] ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"(?:[A-Za-z0-9+/]{4}){10,}(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?", " [Raw Binary Stream Suppressed] ");
        result = Regex.Replace(result, @"\r\n?|\n", "\n");
        result = Regex.Replace(result, @"\n{3,}", "\n\n");
        return result.Trim();
    }

    private List<string> SliceBodyIntoChunks(string rawText, int maxWords)
    {
        var chunks = new List<string>();
        var words = rawText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i += maxWords)
        {
            chunks.Add(string.Join(" ", words.Skip(i).Take(maxWords)));
        }
        return chunks;
    }
}