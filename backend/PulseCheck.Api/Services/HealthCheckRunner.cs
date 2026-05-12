using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PulseCheck.Api.Data;
using PulseCheck.Api.Hubs;
using PulseCheck.Api.Models;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Api.Services;

public sealed class HealthCheckRunner
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MonitorStatusClassifier _classifier;
    private readonly IncidentService _incidentService;
    private readonly SslCertificateInspector _sslInspector;
    private readonly NotificationService _notifications;
    private readonly IHubContext<MonitorHub> _hub;
    private readonly ILogger<HealthCheckRunner> _logger;

    public HealthCheckRunner(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        MonitorStatusClassifier classifier,
        IncidentService incidentService,
        SslCertificateInspector sslInspector,
        NotificationService notifications,
        IHubContext<MonitorHub> hub,
        ILogger<HealthCheckRunner> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _classifier = classifier;
        _incidentService = incidentService;
        _sslInspector = sslInspector;
        _notifications = notifications;
        _hub = hub;
        _logger = logger;
    }

    public async Task<int> RunDueChecksAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var monitors = await _db.Monitors
            .Where(monitor => !monitor.IsPaused && monitor.NextCheckAt <= now)
            .OrderBy(monitor => monitor.NextCheckAt)
            .Take(25)
            .ToListAsync(cancellationToken);

        foreach (var monitor in monitors)
        {
            try
            {
                await CheckAsync(monitor, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check monitor {MonitorId}", monitor.Id);
            }
        }

        return monitors.Count;
    }

    public async Task CheckAsync(MonitorEntity monitor, CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var previousStatus = monitor.CurrentStatus;
        var previousSslStatus = monitor.SslCertificateStatus;
        var probe = await ProbeAsync(monitor, cancellationToken);
        var classification = _classifier.Classify(monitor, probe);
        var ssl = await InspectSslAsync(monitor, cancellationToken);

        var check = new MonitorCheck
        {
            MonitorId = monitor.Id,
            Status = classification.Status,
            StatusCode = probe.StatusCode,
            ResponseTimeMs = probe.ResponseTimeMs,
            ErrorMessage = classification.ErrorMessage,
            CheckedAt = checkedAt
        };

        monitor.CurrentStatus = classification.Status;
        monitor.LastCheckedAt = checkedAt;
        monitor.LastStatusCode = probe.StatusCode;
        monitor.LastResponseTimeMs = probe.ResponseTimeMs;
        monitor.LastErrorMessage = classification.ErrorMessage;
        monitor.SslCertificateStatus = ssl.Status;
        monitor.SslCertificateExpiresAt = ssl.ExpiresAt;
        monitor.SslCertificateDaysRemaining = ssl.DaysRemaining;
        monitor.LastSslErrorMessage = ssl.ErrorMessage;
        monitor.NextCheckAt = checkedAt.AddSeconds(monitor.CheckIntervalSeconds);
        monitor.UpdatedAt = checkedAt;

        _db.MonitorChecks.Add(check);
        await _incidentService.ApplyTransitionAsync(
            _db,
            monitor,
            previousStatus,
            classification.Status,
            checkedAt,
            classification.ErrorMessage,
            cancellationToken);

        await CreateSslNotificationAsync(monitor, previousSslStatus, ssl, checkedAt, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        await _hub.Clients.User(monitor.UserId.ToString()).SendAsync(
            "monitorUpdated",
            new
            {
                monitor.Id,
                Status = monitor.CurrentStatus.ToString(),
                monitor.LastCheckedAt,
                monitor.LastResponseTimeMs,
                SslCertificateStatus = monitor.SslCertificateStatus.ToString(),
                monitor.SslCertificateExpiresAt,
                monitor.SslCertificateDaysRemaining
            },
            cancellationToken);
    }

    private async Task<SslCertificateResult> InspectSslAsync(MonitorEntity monitor, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(monitor.Url, UriKind.Absolute, out var uri))
        {
            return new SslCertificateResult(SslCertificateStatus.Unavailable, null, null, "Monitor URL is invalid.");
        }

        return await _sslInspector.InspectAsync(uri, monitor.TimeoutSeconds, cancellationToken);
    }

    private async Task CreateSslNotificationAsync(
        MonitorEntity monitor,
        SslCertificateStatus previousStatus,
        SslCertificateResult ssl,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        if (ssl.Status == previousStatus)
        {
            return;
        }

        if (ssl.Status is not (SslCertificateStatus.ExpiringSoon or SslCertificateStatus.Critical or SslCertificateStatus.Expired))
        {
            return;
        }

        var type = ssl.Status == SslCertificateStatus.ExpiringSoon
            ? NotificationType.SslCertificateWarning
            : NotificationType.SslCertificateCritical;

        var remaining = ssl.DaysRemaining is null
            ? "soon"
            : ssl.DaysRemaining <= 0
                ? "now"
                : $"in {ssl.DaysRemaining} days";
        var title = ssl.Status == SslCertificateStatus.Expired
            ? $"{monitor.Name} SSL certificate expired"
            : $"{monitor.Name} SSL certificate expires {remaining}";
        var message = ssl.ExpiresAt is null
            ? $"{monitor.Name} has an SSL certificate warning."
            : $"{monitor.Name}'s SSL certificate expires on {ssl.ExpiresAt.Value.LocalDateTime}.";

        await _notifications.CreateAsync(
            _db,
            monitor.UserId,
            monitor.Id,
            null,
            type,
            title,
            message,
            $"ssl:{monitor.Id}:{ssl.Status}:{ssl.ExpiresAt?.ToString("yyyyMMdd") ?? "none"}",
            checkedAt,
            cancellationToken);
    }

    private async Task<HealthProbeResult> ProbeAsync(MonitorEntity monitor, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(monitor.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new HealthProbeResult(false, null, null, null, "Only absolute HTTP and HTTPS URLs can be monitored.");
        }

        var client = _httpClientFactory.CreateClient("monitor-checker");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(monitor.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("PulseCheck/1.0");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            string? body = null;

            if (!string.IsNullOrWhiteSpace(monitor.ExpectedKeyword))
            {
                body = await response.Content.ReadAsStringAsync(linked.Token);
            }

            stopwatch.Stop();
            return new HealthProbeResult(true, (int)response.StatusCode, (int)stopwatch.ElapsedMilliseconds, body, null);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new HealthProbeResult(false, null, (int)stopwatch.ElapsedMilliseconds, null, "Request timed out.");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return new HealthProbeResult(false, null, (int)stopwatch.ElapsedMilliseconds, null, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            return new HealthProbeResult(false, null, (int)stopwatch.ElapsedMilliseconds, null, ex.Message);
        }
    }
}
