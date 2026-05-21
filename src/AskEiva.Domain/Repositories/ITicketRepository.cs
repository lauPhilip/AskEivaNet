using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Repositories;

public interface ITicketRepository
{
    Task UpsertTicketAsync(TicketNode ticket);
    Task<IEnumerable<TicketNode>> GetUnprocessedTicketsAsync(int limit);
    Task MarkAsDistilledAsync(string sourceId);
}