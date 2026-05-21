using AskEiva.Application.Tickets.Commands;
using MediatR;

namespace AskEiva.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        this._serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("⚓ AskEIVA Background Sync Worker initiated.");

        // Production Rule: Background Services are singletons. 
        // To use MediatR handlers cleanly without memory leaks, we create a scoped lifecycle.
        using (var scope = _serviceProvider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                _logger.LogInformation("[Sync] Spawning ticket ingestion array...");

                // Execute the continuous ingestion. 
                // Adjust StartPage and MaxPages parameters as needed for your historical sweeps.
                var command = new IngestTicketsCommand(StartPage: 1, MaxPages: 50);
                var result = await mediator.Send(command, stoppingToken);

                if (result.CompletedGracefully)
                {
                    _logger.LogInformation("[Sync] Success: {Message}. Total Records Processed: {Count}", 
                        result.Message, result.TotalIngested);
                }
                else
                {
                    _logger.LogWarning("[Sync] Interrupted: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚨 Critical structural failure in synchronization pipeline.");
            }
        }

        _logger.LogInformation("⚓ AskEIVA Background Service execution block finalized. Entering sleep state.");
    }
}