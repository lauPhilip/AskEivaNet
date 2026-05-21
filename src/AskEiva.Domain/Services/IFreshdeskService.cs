using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Services;

public interface IFreshdeskService
{
    Task<IEnumerable<TicketNode>> FetchTicketsAsync(int page, int perPage, DateTime? updatedSince = null);
    Task<string> FetchTicketConversationsAsync(long ticketId);
}