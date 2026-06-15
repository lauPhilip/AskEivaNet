using System;

namespace AskEiva.WebUI.Models;

/// <summary>
/// Provides a centralized in-memory state store and broker used to track, compute, 
/// and stream long-running background graph distillation progress metrics across separate Blazor UI components.
/// </summary>
public class MeshBuildStateStore
{
    /// <summary>
    /// Gets or sets a value indicating whether a global multi-hop graph context compilation task is currently running.
    /// </summary>
    public bool IsProcessing { get; set; } = false;

    /// <summary>
    /// Gets or sets the total running number of support ticket units processed inside the active background task batch.
    /// </summary>
    public int CurrentCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the total boundary cap size quantity of target elements loaded into the processing memory stack.
    /// </summary>
    public int TotalCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the specific alphanumeric ticket tracking identifier key currently undergoing compilation (e.g., "FD-1039").
    /// </summary>
    public string CurrentTicketId { get; set; } = "None";

    /// <summary>
    /// Gets or sets the natural text execution string detail or terminal milestone trace message emitted by the engine worker.
    /// </summary>
    public string StatusMessage { get; set; } = "Idle";

    /// <summary>
    /// Gets the calculated mathematical whole-percentage integer tracking the active synchronization task progress.
    /// </summary>
    public int Percentage => TotalCount > 0 ? (int)((double)CurrentCount / TotalCount * 100) : 0;

    /// <summary>
    /// Event multicast delegate triggered whenever internal parameters mutate, notifying listening UI components to force re-render cycles.
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Atomically updates all internal execution parameters and invokes the state tracking delegate to dispatch state refreshes.
    /// </summary>
    /// <param name="current">The ongoing sequential item row number currently handled by the compiler core.</param>
    /// <param name="total">The aggregate limit boundary count size indicating the overall task queue mass.</param>
    /// <param name="ticketId">The targeted ticket anchor identifier currently open inside the background task loop.</param>
    /// <param name="message">The logging operational statement token string dispatched by the executing worker service.</param>
    public void UpdateProgress(int current, int total, string ticketId, string message)
    {
        CurrentCount = current;
        TotalCount = total;
        CurrentTicketId = ticketId;
        StatusMessage = message;
        
        // Notify any active page wrappers (e.g., ConfigurationPortal) to refresh their progress layout elements immediately
        OnStateChanged?.Invoke();
    }
}