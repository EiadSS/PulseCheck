using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PulseCheck.Api.Data;
using PulseCheck.Api.Models;
using PulseCheck.Api.Services;
using Xunit;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Tests;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesInAppNotification_WhenEmailDeliveryIsNotConfigured()
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        var monitor = CreateMonitor(user.Id);
        db.Users.Add(user);
        db.Monitors.Add(monitor);
        await db.SaveChangesAsync();

        var service = CreateService();
        var notification = await service.CreateAsync(
            db,
            user.Id,
            monitor.Id,
            null,
            NotificationType.MonitorFailed,
            "API is Down",
            "Request timed out.",
            $"test:{Guid.NewGuid()}",
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.NotNull(notification);
        var saved = await db.Notifications.SingleAsync();
        Assert.False(saved.IsRead);
        Assert.Equal(NotificationEmailStatus.NotConfigured, saved.EmailStatus);
    }

    [Fact]
    public async Task SendAsync_UsesResendApi_WhenConfigured()
    {
        var handler = new RecordingEmailHandler();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resend:ApiKey"] = "re_test_key",
                ["Resend:FromEmail"] = "alerts@example.com",
                ["Resend:FromName"] = "PulseCheck",
                ["Resend:ApiUrl"] = "https://api.resend.test/emails"
            })
            .Build();
        var email = new EmailDeliveryService(configuration, NullLogger<EmailDeliveryService>.Instance, new HttpClient(handler));

        var result = await email.SendAsync(
            "ops@example.com",
            "PulseCheck: API is Error",
            "Expected HTTP 200, received HTTP 404.",
            "<p>Expected HTTP 200, received HTTP 404.</p>",
            CancellationToken.None);

        Assert.Equal(NotificationEmailStatus.Sent, result.Status);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://api.resend.test/emails", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("re_test_key", handler.Authorization?.Parameter);
        Assert.Contains("\"from\":\"PulseCheck \\u003Calerts@example.com\\u003E\"", handler.Body);
        Assert.Contains("\"to\":\"ops@example.com\"", handler.Body);
        Assert.Contains("\"html\":\"\\u003Cp\\u003EExpected HTTP 200, received HTTP 404.\\u003C/p\\u003E\"", handler.Body);
    }

    [Fact]
    public async Task CreateAsync_SkipsDuplicateDedupKey()
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService();
        var key = $"incident-opened:{Guid.NewGuid()}";

        await service.CreateAsync(db, user.Id, null, null, NotificationType.MonitorFailed, "First", "First", key, DateTimeOffset.UtcNow, CancellationToken.None);
        await service.CreateAsync(db, user.Id, null, null, NotificationType.MonitorFailed, "Second", "Second", key, DateTimeOffset.UtcNow, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_CreatesInAppNotificationAndSkipsEmail_WhenUserDisabledEmailAlerts()
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        user.EmailAlertsEnabled = false;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var notification = await CreateService().CreateAsync(
            db,
            user.Id,
            null,
            null,
            NotificationType.MonitorFailed,
            "API is Down",
            "Request timed out.",
            $"disabled:{Guid.NewGuid()}",
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.NotNull(notification);
        Assert.Equal(NotificationEmailStatus.Skipped, notification.EmailStatus);
        Assert.Equal("Email alerts are disabled.", notification.EmailErrorMessage);
        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task IncidentService_CreatesFailureAndRecoveryNotifications()
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        var monitor = CreateMonitor(user.Id);
        db.Users.Add(user);
        db.Monitors.Add(monitor);
        await db.SaveChangesAsync();

        var incidentService = new IncidentService(CreateService());
        await incidentService.ApplyTransitionAsync(
            db,
            monitor,
            MonitorStatus.Up,
            MonitorStatus.Down,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            "Request timed out.",
            CancellationToken.None);
        await db.SaveChangesAsync();

        var incident = await db.Incidents.SingleAsync();
        await incidentService.ApplyTransitionAsync(
            db,
            monitor,
            MonitorStatus.Down,
            MonitorStatus.Up,
            DateTimeOffset.UtcNow,
            null,
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Contains(await db.Notifications.ToListAsync(), notification => notification.IncidentId == incident.Id && notification.Type == NotificationType.MonitorFailed);
        Assert.Contains(await db.Notifications.ToListAsync(), notification => notification.IncidentId == incident.Id && notification.Type == NotificationType.MonitorRecovered);
    }

    [Fact]
    public void NotificationEmailTemplate_IncludesMonitorDetailsAndActionLink()
    {
        var monitorId = Guid.NewGuid();
        var notification = new Notification
        {
            Type = NotificationType.MonitorFailed,
            Title = "API is Error",
            Message = "Expected HTTP 200, received HTTP 404.",
            CreatedAt = new DateTimeOffset(2026, 5, 7, 22, 15, 0, TimeSpan.Zero)
        };
        var user = CreateUser();
        var monitor = new MonitorEntity
        {
            Id = monitorId,
            UserId = user.Id,
            Name = "API",
            Url = "https://example.com/health",
            CurrentStatus = MonitorStatus.Error,
            LastStatusCode = 404,
            LastResponseTimeMs = 23,
            LastCheckedAt = notification.CreatedAt
        };
        var incident = new Incident
        {
            Status = IncidentStatus.Open,
            StartedStatus = MonitorStatus.Error,
            StartedAt = notification.CreatedAt,
            Title = "API is Error"
        };

        var content = NotificationEmailTemplate.Build(notification, user, monitor, incident, "https://pulsecheck.example");

        Assert.Equal("PulseCheck: API is Error", content.Subject);
        Assert.Contains("https://example.com/health", content.TextBody);
        Assert.Contains("HTTP status: 404", content.TextBody);
        Assert.Contains("Response time: 23 ms", content.TextBody);
        Assert.Contains($"https://pulsecheck.example/monitors/{monitorId}", content.TextBody);
        Assert.Contains("View in PulseCheck", content.HtmlBody);
        Assert.Contains("Expected HTTP 200, received HTTP 404.", content.HtmlBody);
    }

    private static NotificationService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PulseCheck:FrontendBaseUrl"] = "https://pulsecheck.example"
            })
            .Build();
        var email = new EmailDeliveryService(configuration, NullLogger<EmailDeliveryService>.Instance, new HttpClient());
        return new NotificationService(email, configuration);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static ApplicationUser CreateUser()
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "ops@example.com",
            UserName = "ops@example.com",
            WorkspaceName = "Ops",
            PublicStatusSlug = "ops",
            EmailAlertsEnabled = true
        };
    }

    private static MonitorEntity CreateMonitor(Guid userId)
    {
        return new MonitorEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "API",
            Url = "https://example.com",
            CurrentStatus = MonitorStatus.Up
        };
    }

    private sealed class RecordingEmailHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public System.Net.Http.Headers.AuthenticationHeaderValue? Authorization { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"email-id"}""")
            };
        }
    }
}
