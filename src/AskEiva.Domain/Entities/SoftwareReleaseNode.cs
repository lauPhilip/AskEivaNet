using System;

namespace AskEiva.Domain.Entities;

public class SoftwareReleaseNode
{
    public string Id { get; set; } = string.Empty;
    public string GroupCategory { get; set; } = string.Empty;     
    public string Product { get; set; } = string.Empty;           
    public string ReleaseType { get; set; } = string.Empty;     
    public string Version { get; set; } = string.Empty;           
    public string FullVersionTitle { get; set; } = string.Empty;  
    public string MetadataNote { get; set; } = string.Empty;   
    public DateTime ReleaseDate { get; set; }
    public string SectionHeader { get; set; } = string.Empty;
    public string ContentChunk { get; set; } = string.Empty;
    public string RefTickets { get; set; } = string.Empty;
}