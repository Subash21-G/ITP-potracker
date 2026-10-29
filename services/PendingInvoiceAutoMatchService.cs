namespace POTracker.Services;

public sealed class PendingInvoiceAutoMatchService(
    IServiceScopeFactory scopeFactory,
    ILogger<PendingInvoiceAutoMatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Allow application startup to complete before querying the database.
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var matcher = scope.ServiceProvider.GetRequiredService<PendingInvoiceMatcher>();
                var count = await matcher.MatchAllPendingInvoicesAsync(stoppingToken);
                if (count > 0)
                    logger.LogInformation("Automatically matched {Count} pending invoice rows.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Pending invoice auto-match failed; it will retry on the next cycle.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
