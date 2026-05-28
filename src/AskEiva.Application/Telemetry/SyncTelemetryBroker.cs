using System;

namespace AskEiva.Application.Telemetry;

public interface ISyncTelemetryBroker
{
    event Action<SyncProgressUpdate>? OnProgressUpdated;
    void Broadcast(SyncProgressUpdate update);
}

public class SyncTelemetryBroker : ISyncTelemetryBroker
{
    public event Action<SyncProgressUpdate>? OnProgressUpdated;

    public void Broadcast(SyncProgressUpdate update)
    {
        OnProgressUpdated?.Invoke(update);
    }
}

public class SyncProgressUpdate
{
    public string LogMessage { get; set; } = string.Empty;
    public int CurrentPage { get; set; }
    public string CurrentTicketId { get; set; } = string.Empty;
    public string TicketSubject { get; set; } = string.Empty;
    public int ChunksGenerated { get; set; }
    public int TotalChunksIndexed { get; set; }
    public string Status { get; set; } = "Processing"; // Processing, Success, Skip, Complete
}