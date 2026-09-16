using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Services
{
    public sealed class MaterialRmGradeBackfillService :
        IHostedService
    {
        private readonly IServiceScopeFactory
            _scopeFactory;

        private readonly ILogger<
            MaterialRmGradeBackfillService> _logger;

        public MaterialRmGradeBackfillService(
            IServiceScopeFactory scopeFactory,
            ILogger<MaterialRmGradeBackfillService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task StartAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                using var scope =
                    _scopeFactory.CreateScope();

                var context =
                    scope.ServiceProvider
                        .GetRequiredService<
                            ApplicationDbContext>();

                var materials =
                    await context.Materials
                        .Where(material =>
                            material.RMGrade == null ||
                            material.RMGrade.Trim() ==
                                string.Empty)
                        .ToListAsync(
                            cancellationToken);

                var updatedCount = 0;

                foreach (var material in materials)
                {
                    var grade =
                        MaterialRmGradeExtractor.Extract(
                            material.ShortDescription,
                            material.BriefDescription);

                    if (grade == null)
                    {
                        continue;
                    }

                    material.RMGrade = grade;
                    updatedCount++;
                }

                if (updatedCount == 0)
                {
                    return;
                }

                await context.SaveChangesAsync(
                    cancellationToken);

                _logger.LogInformation(
                    "Filled RM Grade from descriptions for {MaterialCount} materials.",
                    updatedCount);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Normal application shutdown during startup.
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Unable to backfill material RM Grade values during startup.");
            }
        }

        public Task StopAsync(
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
