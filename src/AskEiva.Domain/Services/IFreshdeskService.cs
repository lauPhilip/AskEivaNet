using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AskEiva.Domain.Services;

public interface IFreshdeskService
{
    Task<IEnumerable<FreshdeskTicketDto>> GetTicketsPageAsync(int page, int perPage = 30);
    
    Task<IEnumerable<FreshdeskConversationDto>> GetTicketConversationsAsync(long ticketId);
}

public class FreshdeskTicketDto
{
    public long Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description_Text { get; set; } = string.Empty;
    public int Status { get; set; }
    public int Priority { get; set; }
    public DateTime Created_At { get; set; }
    public DateTime Updated_At { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class FreshdeskConversationDto
{
    public long Id { get; set; }
    public string Body_Text { get; set; } = string.Empty;
    public bool Incoming { get; set; } // True = Client/Surveyor, False = EIVA Agent
    public DateTime Created_At { get; set; }
}