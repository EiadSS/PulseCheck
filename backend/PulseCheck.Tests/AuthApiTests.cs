using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseCheck.Api.Data;
using PulseCheck.Api.Contracts;
using PulseCheck.Api.Models;
using PulseCheck.Api.Services;
using Xunit;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Tests;

public sealed class AuthApiTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;
    private readonly AuthApiFactory _factory;

    public AuthApiTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ReturnsFriendlyInvalidEmailMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            password = "Password1",
            workspaceName = "Acme Ops"
        });

        var errors = await ReadErrorsAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Enter a valid email address.", errors["Email"]);
    }

    [Fact]
    public async Task Register_ReturnsFriendlyDuplicateEmailMessage()
    {
        var payload = new
        {
            email = "ops@example.com",
            password = "Password1",
            workspaceName = "Acme Ops"
        };

        var first = await _client.PostAsJsonAsync("/api/auth/register", payload);
        var second = await _client.PostAsJsonAsync("/api/auth/register", payload);
        var errors = await ReadErrorsAsync(second);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("An account with this email already exists. Sign in instead.", errors["Email"]);
    }

    [Fact]
    public async Task DemoEndpoint_IsRemoved()
    {
        var response = await _client.PostAsync("/api/auth/demo", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NotificationPreferences_DefaultEnabledAndCanBeUpdated()
    {
        await RegisterAndAuthorizeAsync("prefs@example.com", "Prefs");

        var me = await _client.GetAsync("/api/auth/me");
        me.EnsureSuccessStatusCode();

        var initial = await _client.GetFromJsonAsync<NotificationPreferencesDto>("/api/account/notification-preferences");
        Assert.True(initial!.EmailAlertsEnabled);
        Assert.False(initial.EmailDeliveryConfigured);

        var update = await _client.PutAsJsonAsync("/api/account/notification-preferences", new UpdateNotificationPreferencesRequest(false));
        update.EnsureSuccessStatusCode();

        var updated = await update.Content.ReadFromJsonAsync<NotificationPreferencesDto>();
        Assert.False(updated!.EmailAlertsEnabled);
        Assert.False(updated.EmailDeliveryConfigured);
    }

    [Fact]
    public async Task RunCheckNow_RunsOneCheckAndReturnsUpdatedMonitor()
    {
        await RegisterAndAuthorizeAsync("manual-check@example.com", "Manual Checks");
        var create = await CreateMonitorAsync("Manual API");
        var monitorId = create.GetProperty("id").GetGuid();

        var response = await _client.PostAsync($"/api/monitors/{monitorId}/check", null);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Up", updated.GetProperty("currentStatus").GetString());
        Assert.Equal(200, updated.GetProperty("lastStatusCode").GetInt32());
        Assert.True(updated.GetProperty("lastResponseTimeMs").GetInt32() >= 0);

        var checks = await _client.GetFromJsonAsync<JsonElement>($"/api/monitors/{monitorId}/checks?range=24h");
        Assert.Single(checks.EnumerateArray());
    }

    [Fact]
    public async Task ResponseTimes_ReturnsSparsePointsInTimeOrder()
    {
        await RegisterAndAuthorizeAsync("sparse-response-times@example.com", "Sparse Response Times");
        var create = await CreateMonitorAsync("Sparse API");
        var monitorId = create.GetProperty("id").GetGuid();
        var now = DateTimeOffset.UtcNow;

        await SeedMonitorChecksAsync(
            monitorId,
            new[]
            {
                new MonitorCheck { MonitorId = monitorId, Status = MonitorStatus.Up, ResponseTimeMs = 180, CheckedAt = now.AddHours(-2) },
                new MonitorCheck { MonitorId = monitorId, Status = MonitorStatus.Up, ResponseTimeMs = 120, CheckedAt = now.AddHours(-1) }
            });

        var points = await _client.GetFromJsonAsync<List<MonitorResponseTimePointDto>>(
            $"/api/monitors/{monitorId}/response-times?range=24h");

        Assert.Equal(2, points!.Count);
        Assert.All(points, point => Assert.Equal(1, point.CheckCount));
        Assert.True(points[0].CheckedAt < points[1].CheckedAt);
        Assert.Equal(180, points[0].ResponseTimeMs);
        Assert.Equal(120, points[1].ResponseTimeMs);
    }

    [Fact]
    public async Task ResponseTimes_DownsamplesAcrossSelectedRange()
    {
        await RegisterAndAuthorizeAsync("busy-response-times@example.com", "Busy Response Times");
        var create = await CreateMonitorAsync("Busy API");
        var monitorId = create.GetProperty("id").GetGuid();
        var now = DateTimeOffset.UtcNow;
        var checks = new List<MonitorCheck>();

        for (var index = 0; index < 700; index++)
        {
            checks.Add(new MonitorCheck
            {
                MonitorId = monitorId,
                Status = MonitorStatus.Up,
                ResponseTimeMs = 50 + (index % 40),
                CheckedAt = now.AddDays(-6.9).AddTicks(TimeSpan.FromDays(6.9).Ticks * index / 699)
            });
        }

        for (var index = 0; index < 50; index++)
        {
            checks.Add(new MonitorCheck
            {
                MonitorId = monitorId,
                Status = MonitorStatus.Up,
                ResponseTimeMs = 100 + (index % 30),
                CheckedAt = now.AddDays(-25).AddHours(index * 2)
            });
        }

        await SeedMonitorChecksAsync(monitorId, checks);

        var dayPoints = await _client.GetFromJsonAsync<List<MonitorResponseTimePointDto>>(
            $"/api/monitors/{monitorId}/response-times?range=24h");
        var weekPoints = await _client.GetFromJsonAsync<List<MonitorResponseTimePointDto>>(
            $"/api/monitors/{monitorId}/response-times?range=7d");
        var monthPoints = await _client.GetFromJsonAsync<List<MonitorResponseTimePointDto>>(
            $"/api/monitors/{monitorId}/response-times?range=30d");

        Assert.True(dayPoints!.Count <= 300);
        Assert.True(weekPoints!.Count <= 300);
        Assert.True(monthPoints!.Count <= 300);
        Assert.All(weekPoints, point => Assert.True(point.CheckCount >= 1));
        Assert.True(dayPoints.Min(point => point.CheckedAt) > now.AddHours(-25));
        Assert.True(weekPoints.Min(point => point.CheckedAt) < now.AddDays(-6));
        Assert.True(monthPoints.Min(point => point.CheckedAt) < now.AddDays(-20));
    }

    [Fact]
    public async Task ResponseTimes_ReturnsNotFoundForOtherUserMonitor()
    {
        await RegisterAndAuthorizeAsync("response-owner@example.com", "Response Owner");
        var create = await CreateMonitorAsync("Owned API");
        var monitorId = create.GetProperty("id").GetGuid();

        await RegisterAndAuthorizeAsync("response-other@example.com", "Response Other");
        var response = await _client.GetAsync($"/api/monitors/{monitorId}/response-times?range=24h");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RunCheckNow_ReturnsValidationProblem_WhenMonitorIsPaused()
    {
        await RegisterAndAuthorizeAsync("paused-check@example.com", "Paused Checks");
        var create = await CreateMonitorAsync("Paused API");
        var monitorId = create.GetProperty("id").GetGuid();

        var pause = await _client.PostAsync($"/api/monitors/{monitorId}/pause", null);
        pause.EnsureSuccessStatusCode();

        var response = await _client.PostAsync($"/api/monitors/{monitorId}/check", null);
        var errors = await ReadErrorsAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Resume this monitor before running a check.", errors["Monitor"]);
    }

    [Fact]
    public async Task DeleteNotification_RemovesOwnedNotificationAndPreservesLinkedRecords()
    {
        var auth = await RegisterAndAuthorizeAsync("delete-alert@example.com", "Delete Alerts");
        var seeded = await SeedLinkedNotificationAsync(auth.User.Id, isRead: false);

        var initialCount = await _client.GetFromJsonAsync<NotificationUnreadCountDto>("/api/notifications/unread-count");
        Assert.Equal(1, initialCount!.UnreadCount);

        var response = await _client.DeleteAsync($"/api/notifications/{seeded.NotificationId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updatedCount = await _client.GetFromJsonAsync<NotificationUnreadCountDto>("/api/notifications/unread-count");
        Assert.Equal(0, updatedCount!.UnreadCount);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null(await db.Notifications.FindAsync(seeded.NotificationId));
        Assert.NotNull(await db.Monitors.FindAsync(seeded.MonitorId));
        Assert.NotNull(await db.Incidents.FindAsync(seeded.IncidentId));
    }

    [Fact]
    public async Task DeleteNotification_ReturnsNotFoundForMissingOrOtherUserNotification()
    {
        await RegisterAndAuthorizeAsync("delete-missing-alert@example.com", "Delete Missing Alerts");
        var otherUserNotification = await SeedLinkedNotificationAsync(Guid.NewGuid(), isRead: true);

        var otherUserResponse = await _client.DeleteAsync($"/api/notifications/{otherUserNotification.NotificationId}");
        var missingResponse = await _client.DeleteAsync($"/api/notifications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, otherUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    private async Task<AuthResponse> RegisterAndAuthorizeAsync(string email, string workspaceName)
    {
        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1",
            workspaceName
        });
        register.EnsureSuccessStatusCode();

        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return auth;
    }

    private async Task<JsonElement> CreateMonitorAsync(string name)
    {
        var create = await _client.PostAsJsonAsync("/api/monitors", new
        {
            name,
            url = "http://example.com/health",
            type = "Api",
            checkIntervalSeconds = 60,
            timeoutSeconds = 10,
            degradedThresholdMs = 800,
            expectedStatusCode = 200,
            expectedKeyword = (string?)null,
            isPublic = false
        });
        create.EnsureSuccessStatusCode();
        return await create.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task SeedMonitorChecksAsync(Guid monitorId, IEnumerable<MonitorCheck> checks)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.MonitorChecks.AddRange(checks);
        await db.SaveChangesAsync();
    }

    private async Task<(Guid NotificationId, Guid MonitorId, Guid IncidentId)> SeedLinkedNotificationAsync(Guid userId, bool isRead)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var monitor = new MonitorEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = $"Seeded API {Guid.NewGuid():N}",
            Url = "https://example.com",
            CurrentStatus = MonitorStatus.Down,
            CreatedAt = now,
            UpdatedAt = now
        };
        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            MonitorId = monitor.Id,
            Status = IncidentStatus.Open,
            StartedStatus = MonitorStatus.Down,
            StartedAt = now,
            Title = $"{monitor.Name} is Down"
        };
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MonitorId = monitor.Id,
            IncidentId = incident.Id,
            Type = NotificationType.MonitorFailed,
            Title = incident.Title,
            Message = "Failure detected.",
            DedupKey = $"test-notification:{Guid.NewGuid():N}",
            IsRead = isRead,
            CreatedAt = now
        };

        db.Monitors.Add(monitor);
        db.Incidents.Add(incident);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        return (notification.Id, monitor.Id, incident.Id);
    }

    private static async Task<Dictionary<string, string[]>> ReadErrorsAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = new Dictionary<string, string[]>();

        foreach (var property in problem.GetProperty("errors").EnumerateObject())
        {
            errors[property.Name] = property.Value.EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty)
                .ToArray();
        }

        return errors;
    }
}

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PulseCheck:AutoMigrate"] = "false",
                ["PulseCheck:AdminEmails"] = "owner@example.com",
                ["Jwt:Secret"] = "dev-only-secret-change-me-please-32-chars",
                ["Smtp:Password"] = "",
                ["Smtp:FromEmail"] = ""
            });
        });

        builder.ConfigureServices(services =>
        {
            var databaseName = Guid.NewGuid().ToString();

            services.RemoveAll<DbContextOptions<AppDbContext>>();

            foreach (var descriptor in services
                .Where(descriptor => descriptor.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") == true)
                .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));

            foreach (var descriptor in services
                .Where(descriptor => descriptor.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
                    descriptor.ImplementationType == typeof(HealthCheckWorker))
                .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddHttpClient("monitor-checker")
                .ConfigurePrimaryHttpMessageHandler(() => new HealthyHttpMessageHandler());
        });
    }
}

public sealed class HealthyHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Welcome")
        });
    }
}
