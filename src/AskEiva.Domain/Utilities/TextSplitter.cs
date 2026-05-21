using System.Text.RegularExpressions;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Utilities;

public class TextSplitter
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    
    // Regex to extract image source links from Freshdesk HTML content
    private static readonly Regex ImageRegex = new(@"<img[^>]+src=[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public TextSplitter(int chunkSize = 1000, int chunkOverlap = 200)
    {
        _chunkSize = chunkSize;
        _chunkOverlap = chunkOverlap;
    }

    public IEnumerable<TextChunk> SplitTicket(TicketNode ticket)
    {
        var chunks = new List<TextChunk>();
        
        // 1. Extract and preserve any embedded image links before cleaning the text
        var imageUrls = ExtractImageUrls(ticket.Content);
        
        // 2. Clean HTML formatting to optimize token/character efficiency for Weaviate embeddings
        var cleanText = CleanHtml(ticket.Content);
        
        if (string.IsNullOrWhiteSpace(cleanText))
            return chunks;

        // 3. Sliding Window Chunking Logic
        int position = 0;
        int sequence = 0;

        while (position < cleanText.Length)
        {
            int length = Math.Min(_chunkSize, cleanText.Length - position);
            var content = cleanText.Substring(position, length);

            // Refine split boundary to avoid breaking a sentence or word midway if possible
            if (position + length < cleanText.Length)
            {
                int lastSeparator = content.LastIndexOfAny(new[] { '.', '\n', ' ' });
                if (lastSeparator > _chunkSize * 0.5) // Only adjust if it doesn't shrink the chunk excessively
                {
                    length = lastSeparator + 1;
                    content = cleanText.Substring(position, length);
                }
            }

            var metadata = new Dictionary<string, string>
            {
                { "Subject", ticket.Subject },
                { "Url", ticket.Url },
                { "Status", ticket.Status.ToString() }
            };

            chunks.Add(new TextChunk(
                ChunkId: $"{ticket.SourceId}_ch_{sequence}",
                SourceId: ticket.SourceId,
                Content: content.Trim(),
                SequenceNumber: sequence,
                ImageUrls: imageUrls, // Attaches original ticket images to every chunk for visibility
                Metadata: metadata
            ));

            sequence++;
            position += (length - _chunkOverlap) > 0 ? (length - _chunkOverlap) : length;
        }

        return chunks;
    }

    private List<string> ExtractImageUrls(string htmlContent)
    {
        var urls = new List<string>();
        var matches = ImageRegex.Matches(htmlContent);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                urls.Add(match.Groups[1].Value);
            }
        }
        return urls;
    }

    private string CleanHtml(string htmlContent)
    {
        if (string.IsNullOrEmpty(htmlContent)) return string.Empty;
        
        // Replace breaks and paragraphs with standard newlines to keep layout structures intact
        string text = htmlContent.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("</p>", "\n");
        
        // Strip out the remaining HTML brackets
        return Regex.Replace(text, "<[^>]*>", string.Empty);
    }
}