namespace AskEiva.Domain.Entities;

public class TicketNode
{
    public string SourceId { get; set; } = string.Empty;
    public string DataType { get; set; } = "Ticket";
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsDistilled { get; set; } = false;
    public string Url { get; set; } = string.Empty;
    public int Status { get; set; }
    public int Priority { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}