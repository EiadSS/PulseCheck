namespace PulseCheck.Api.Services;

public sealed class HealthCheckWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthCheckWorker> _logger;

    public HealthCheckWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<HealthCheckWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _configuration.GetValue("PulseCheck:WorkerIntervalSeconds", 10);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(2, intervalSeconds)));

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<HealthCheckRunner>();
            var count = await runner.RunDueChecksAsync(stoppingToken);

            if (count > 0)
            {
                _logger.LogInformation("Completed {Count} monitor checks.", count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PulseCheck worker iteration failed.");
        }
    }
}
