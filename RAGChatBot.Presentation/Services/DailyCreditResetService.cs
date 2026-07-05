using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAGChatBot.BLL.Services;

namespace RAGChatBot.Presentation.Services
{
    public class DailyCreditResetService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DailyCreditResetService> _logger;

        public DailyCreditResetService(IServiceScopeFactory scopeFactory, ILogger<DailyCreditResetService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[CreditReset] Background service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Tính thời gian đến 00:00 UTC+7 ngày tiếp theo
                var nowUtc7 = DateTime.UtcNow.AddHours(7);
                var nextMidnight = nowUtc7.Date.AddDays(1);
                var delay = nextMidnight - nowUtc7;

                _logger.LogInformation("[CreditReset] Next reset at {NextReset} UTC+7 (in {Delay})", nextMidnight, delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var creditService = scope.ServiceProvider.GetRequiredService<ICreditService>();
                    await creditService.ResetDailyCreditsForFreeStudentsAsync();
                    _logger.LogInformation("[CreditReset] Daily credits reset at {Time} UTC+7", DateTime.UtcNow.AddHours(7));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CreditReset] Error resetting daily credits.");
                }
            }
        }
    }
}
