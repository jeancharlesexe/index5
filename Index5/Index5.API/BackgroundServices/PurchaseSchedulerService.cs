using Index5.Application.Services;
using Index5.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Index5.API.BackgroundServices;

public class PurchaseSchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PurchaseSchedulerService> _logger;

    public PurchaseSchedulerService(IServiceProvider serviceProvider, ILogger<PurchaseSchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Purchase Scheduler Service is starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                
                using (var scope = _serviceProvider.CreateScope())
                {
                    var engineService = scope.ServiceProvider.GetRequiredService<PurchaseEngineService>();
                    var repo = scope.ServiceProvider.GetRequiredService<ICustodyRepository>();

                    if (engineService.IsDueToday(now))
                    {
                        bool alreadyExecuted = await repo.HasScheduledPurchaseTodayAsync(now);

                        if (!alreadyExecuted)
                        {
                            _logger.LogInformation("📅 Today {Date} is a scheduled purchase day. Executing engine...", now.ToShortDateString());
                            await engineService.ExecutePurchaseAsync();
                            _logger.LogInformation("✅ Scheduled purchase executed successfully.");
                        }
                        else
                        {
                             _logger.LogInformation("ℹ️ Scheduled purchase for today {Date} has already been executed.", now.ToShortDateString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error occurred in Purchase Scheduler Service.");
            }

            // Verifica a cada 1 hora se hoje é dia de compra
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
