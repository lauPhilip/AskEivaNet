using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;
using UglyToad.PdfPig;

namespace AskEiva.Infrastructure.Services;

public class ReleaseNotesScraper : IReleaseNotesScraper
{
    private readonly HttpClient _httpClient;

    public ReleaseNotesScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<SoftwareReleaseNode>> ScrapeAndChunkAllReleaseNotesAsync()
    {
        var globalDiscoveredNodes = new List<SoftwareReleaseNode>();
        
        // Locate the centralized structural manifest layout document
        string manifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "release_notes_manifest.json");

        if (!File.Exists(manifestPath))
        {
            Console.WriteLine($"[Scraper Configuration Error] Manifest document not located at: {manifestPath}");
            return globalDiscoveredNodes;
        }

        try
        {
            string jsonContent = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<ReleaseNotesManifestDto>(jsonContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (manifest?.Categories == null) return globalDiscoveredNodes;

            foreach (var category in manifest.Categories)
            {
                if (category.Products == null) continue;

                foreach (var product in category.Products)
                {
                    if (product.Versions == null) continue;

                    foreach (var target in product.Versions)
                    {
                        try
                        {
                            // 🚀 THE DELTA ENFORCEMENT PASSTHROUGH:
                            // The outer MediatR Command checks Weaviate BEFORE committing chunks.
                            // To build a truly performant architecture, we can move that query here in a future step 
                            // to prevent even downloading the PDF bytes if the version is already indexed.

                            string clearUrl = Uri.UnescapeDataString(target.RelativeUrl);
                            Console.WriteLine($"[Scraper Network Stream] Accessing binary endpoint path: {clearUrl}");

                            // Fetch the live binary file stream layout natively into transient memory buffers
                            string safeRelativePath = target.RelativeUrl;

                            // If the manifest string contains raw spaces, convert them cleanly to web-safe escape characters
                            if (safeRelativePath.Contains(" "))
                            {
                                // Breaks the path by slash segments to escape individual words safely without corrupting the directory slashes
                                var segments = safeRelativePath.Split('/');
                                for (int i = 0; i < segments.Length; i++)
                                {
                                    // Encodes items like "NaviPac 4.11.1_Release notes.pdf" -> "NaviPac%204.11.1_Release%20notes.pdf"
                                    segments[i] = Uri.EscapeDataString(Uri.UnescapeDataString(segments[i]));
                                }
                                safeRelativePath = string.Join("/", segments);
                            }

                            Console.WriteLine($"[Scraper Network Stream] Requesting resilient URI path: {safeRelativePath}");
                            byte[] downloadedPdfBytes = await _httpClient.GetByteArrayAsync(safeRelativePath);
                            
                            // Extract raw text characters natively from the byte array using PdfPig
                            string rawDocumentText = ExtractTextFromPdfBytes(downloadedPdfBytes);
                            
                            // Segment the raw text layout string into high-fidelity context chunks
                            var fileSegmentedChunks = ChunkReleaseNotesContent(rawDocumentText, product.Name, target.Version, target.ReleaseDate);
                            globalDiscoveredNodes.AddRange(fileSegmentedChunks);
                            
                            Console.WriteLine($"[Scraper Index Task] Successfully structured target record: {product.Name} v{target.Version} ({fileSegmentedChunks.Count} semantic chunks generated).");
                        }
                        catch (Exception assetEx)
                        {
                            Console.WriteLine($"[Scraper Connectivity Exception] Skipped unresolvable resource element track [{target.RelativeUrl}]: {assetEx.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception globalEx)
        {
            Console.WriteLine($"[Scraper Runtime Crash] Manifest execution loop collapsed: {globalEx.Message}");
        }

        return globalDiscoveredNodes;
    }

    private string ExtractTextFromPdfBytes(byte[] pdfBytes)
    {
        var sb = new StringBuilder();
        
        using (var memoryStream = new MemoryStream(pdfBytes))
        using (var document = PdfDocument.Open(memoryStream))
        {
            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }
        }

        return sb.ToString();
    }

private List<SoftwareReleaseNode> ChunkReleaseNotesContent(string text, string product, string version, DateTime date)
{
    var chunks = new List<SoftwareReleaseNode>();
    
    // 💡 SYSTEM OPTIMIZATION: Removed strict line-start anchors so it matches headings embedded near line breaks.
    // This captures headers like "2.1.1 Kernel" or "4.2.8.3 Minor fixes" even with leading noise.
    var sectionRegex = new Regex(@"(?<header>\d+\.\d+\.\d+\s+[A-Za-z0-9\s\(\)\.\-\/]+)\r?\n(?<content>.*?)(?=\d+\.\d+\.\d+\s+[A-Za-z0-9\s\(\)\.\-\/]+|\z)", RegexOptions.Singleline);
    var ticketRegex = new Regex(@"\[(?:FD|J|DO)?-?(\d+)\]", RegexOptions.IgnoreCase);

    var matches = sectionRegex.Matches(text);
    foreach (Match match in matches)
    {
        string header = match.Groups["header"].Value.Trim();
        string content = match.Groups["content"].Value.Trim();

        // Skip table of contents references or tiny layout fragments
        if (string.IsNullOrWhiteSpace(content) || content.Length < 30 || header.Contains("Contents")) continue;

        // Automatically extract any internal Jira or Freshdesk ticket references inside this specific section chunk
        var ticketMatches = ticketRegex.Matches(content);
        var ticketsList = ticketMatches.Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct();
            
        string joinedTickets = string.Join(" ", ticketsList);

        chunks.Add(new SoftwareReleaseNode
        {
            Product = product,
            Version = version,
            ReleaseDate = date,
            SectionHeader = header,
            ContentChunk = content,
            RefTickets = joinedTickets
        });
    }

    // Comprehensive fallback pass: If custom heading markers differ across legacy formats, chunk by page boundaries
    if (!chunks.Any())
    {
        // Splits along page layout boundaries safely
        var pages = text.Split(new[] { "--- PAGE" }, StringSplitOptions.RemoveEmptyEntries);
        int pageCounter = 1;
        foreach (var pageText in pages)
        {
            if (pageText.Length < 100) continue;
            
            // Extract ticket keys out of the raw page text block
            var ticketMatches = ticketRegex.Matches(pageText);
            var ticketsList = ticketMatches.Cast<Match>().Select(m => m.Groups[1].Value).Distinct();
            string joinedTickets = string.Join(" ", ticketsList);

            chunks.Add(new SoftwareReleaseNode
            {
                Product = product,
                Version = version,
                ReleaseDate = date,
                SectionHeader = $"General Specifications - Page {pageCounter}",
                ContentChunk = pageText.Trim(),
                RefTickets = joinedTickets
            });
            pageCounter++;
        }
    }

    return chunks;
}

    // --- Local Strongly-Typed Mapping Data Transfer Objects ---
    private class ReleaseNotesManifestDto { public List<CategoryDto>? Categories { get; set; } }
    private class CategoryDto { public string Name { get; set; } = string.Empty; public List<ProductDto>? Products { get; set; } }
    private class ProductDto { public string Name { get; set; } = string.Empty; public List<VersionDto>? Versions { get; set; } }
    private class VersionDto { public string Version { get; set; } = string.Empty; public DateTime ReleaseDate { get; set; } public string RelativeUrl { get; set; } = string.Empty; }
}