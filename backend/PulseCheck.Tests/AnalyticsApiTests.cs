using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using PulseCheck.Api.Contracts;
using PulseCheck.Api.Data;
using PulseCheck.Api.Models;
using Xunit;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Tests;

public sealed class AnalyticsApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task AdminEmail_CanAccessAnalyticsSummary()
    {
        await using var factory = new AuthApiFactory();
        var client = factory.CreateClient();

        var auth = await RegisterAndAuthorizeAsync(client, "owner@example.com", "Owner");

        Assert.True(auth.User.IsAdmin);

        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me");
        Assert.True(me!.IsAdmin);

        var response = await client.GetAsync("/api/admin/analytics/summary?range=7d");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonAdmin_CannotAccessAnalyticsSummary()
    {
        await using var factory = new AuthApiFactory();
        var client = factory.CreateClient();

        var auth = await RegisterAndAuthorizeAsync(client, "member@example.com", "Member");

        Assert.False(auth.User.IsAdmin);

        var response = await client.GetAsync("/api/admin/analytics/summary?range=7d");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnalyticsEvents_CanBeCreatedAnonymouslyAndWithSignedInUser()
    {
        await using var factory = new AuthApiFactory();
        var client = factory.CreateClient();

        var anonymous = await client.PostAsJsonAsync("/api/analytics/events", new AnalyticsEventRequest("PageView", "/"));
        Assert.Equal(HttpStatusCode.NoContent, anonymous.StatusCode);

        var auth = await RegisterAndAuthorizeAsync(client, "event-user@example.com", "Event User");
        var authenticated = await client.PostAsJsonAsync(
            "/api/analytics/events",
            new AnalyticsEventRequest("PageView", "/dashboard?token=secret#fragment"));
        Assert.Equal(HttpStatusCode.NoContent, authenticated.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var events = db.AnalyticsEvents.ToList();

        Assert.Equal(2, events.Count);
        Assert.Contains(events, analyticsEvent => analyticsEvent.Path == "/" && analyticsEvent.UserId is null);
        Assert.Contains(events, analyticsEvent => analyticsEvent.Path == "/dashboard" && analyticsEvent.UserId == auth.User.Id);
    }

    [Fact]
    public async Task AnalyticsSummary_ReturnsOwnerMetrics()
    {
        await using var factory = new AuthApiFactory();
        var client = factory.CreateClient();

        var owner = await RegisterAndAuthorizeAsync(client, "owner@example.com", "Owner");
        var member = await RegisterAndAuthorizeAsync(client, "analytics-member@example.com", "Analytics Member");
        await SeedAnalyticsDataAsync(factory, member.User.Id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);

        var summary = await client.GetFromJsonAsync<AnalyticsSummaryDto>("/api/admin/analytics/summary?range=7d", JsonOptions);

        Assert.NotNull(summary);
        Assert.Equal("7d", summary!.Range);
        Assert.Equal(2, summary.TotalUsers);
        Assert.Equal(2, summary.NewUsers);
        Assert.Equal(1, summary.ActiveUsers);
        Assert.Equal(1, summary.TotalMonitors);
        Assert.Equal(1, summary.MonitorsCreated);
        Assert.Equal(0.5, summary.AverageMonitorsPerUser);
        Assert.Equal(4, summary.PageViews);
        Assert.Equal(1, summary.PublicStatusPageViews);
        Assert.Equal(2, summary.MonitorChecks);
        Assert.Equal(200, summary.AverageResponseTimeMs);
        Assert.Equal(1, summary.IncidentsOpened);
        Assert.Equal(1, summary.IncidentsResolved);
        Assert.Equal(2, summary.NotificationsCreated);
        Assert.Contains(summary.TopPages, page => page.Path == "/dashboard" && page.Views == 2);
        Assert.Contains(summary.CheckStatusCounts, item => item.Status == MonitorStatus.Up && item.Count == 1);
        Assert.Contains(summary.CheckStatusCounts, item => item.Status == MonitorStatus.Error && item.Count == 1);
        Assert.Contains(summary.EmailStatusCounts, item => item.Status == NotificationEmailStatus.Sent && item.Count == 1);
        Assert.Contains(summary.EmailStatusCounts, item => item.Status == NotificationEmailStatus.Failed && item.Count == 1);
        Assert.Contains(summary.MonitorActivity, monitor =>
            monitor.Name == "Analytics API" &&
            monitor.Url == "https://example.com/health" &&
            monitor.CurrentStatus == MonitorStatus.Error &&
            monitor.CheckCount == 2 &&
            monitor.LastCheckedAt is not null);
        Assert.Contains(summary.RecentSignups, signup => signup.Email == "owner@example.com");
        Assert.Contains(summary.NewUsersOverTime, point => point.Count > 0);
    }

    [Fact]
    public async Task AnalyticsSummary_InvalidRangeFallsBackToSevenDays()
    {
        await using var factory = new AuthApiFactory();
        var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "owner@example.com", "Owner");

        var summary = await client.GetFromJsonAsync<JsonElement>("/api/admin/analytics/summary?range=forever");

        Assert.Equal("7d", summary.GetProperty("range").GetString());
    }

    private static async Task<AuthResponse> RegisterAndAuthorizeAsync(HttpClient client, string email, string workspaceName)
    {
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1",
            workspaceName
        });
        register.EnsureSuccessStatusCode();

        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return auth;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static async Task SeedAnalyticsDataAsync(AuthApiFactory factory, Guid memberUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var monitor = new MonitorEntity
        {
            Id = Guid.NewGuid(),
            UserId = memberUserId,
            Name = "Analytics API",
            Url = "https://example.com/health",
            CurrentStatus = MonitorStatus.Error,
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-2)
        };

        db.Monitors.Add(monitor);
        db.MonitorChecks.AddRange(
            new MonitorCheck
            {
                MonitorId = monitor.Id,
                Status = MonitorStatus.Up,
                StatusCode = 200,
                ResponseTimeMs = 100,
                CheckedAt = now.AddMinutes(-20)
            },
            new MonitorCheck
            {
                MonitorId = monitor.Id,
                Status = MonitorStatus.Error,
                StatusCode = 500,
                ResponseTimeMs = 300,
                CheckedAt = now.AddMinutes(-10)
            });
        db.Incidents.Add(new Incident
        {
            MonitorId = monitor.Id,
            Status = IncidentStatus.Resolved,
            StartedStatus = MonitorStatus.Error,
            ResolvedStatus = MonitorStatus.Up,
            Title = "Analytics API is Error",
            StartedAt = now.AddMinutes(-15),
            ResolvedAt = now.AddMinutes(-5)
        });
        db.Notifications.AddRange(
            new Notification
            {
                UserId = memberUserId,
                MonitorId = monitor.Id,
                Type = NotificationType.MonitorFailed,
                Title = "Analytics API is Error",
                Message = "Expected HTTP 200, received HTTP 500.",
                DedupKey = $"analytics-sent:{Guid.NewGuid():N}",
                EmailStatus = NotificationEmailStatus.Sent,
                CreatedAt = now.AddMinutes(-15)
            },
            new Notification
            {
                UserId = memberUserId,
                MonitorId = monitor.Id,
                Type = NotificationType.MonitorRecovered,
                Title = "Analytics API recovered",
                Message = "Analytics API recovered.",
                DedupKey = $"analytics-failed:{Guid.NewGuid():N}",
                EmailStatus = NotificationEmailStatus.Failed,
                CreatedAt = now.AddMinutes(-5)
            });
        db.AnalyticsEvents.AddRange(
            new AnalyticsEvent { EventType = "PageView", Path = "/", CreatedAt = now.AddMinutes(-30) },
            new AnalyticsEvent { EventType = "PageView", Path = "/dashboard", UserId = memberUserId, CreatedAt = now.AddMinutes(-25) },
            new AnalyticsEvent { EventType = "PageView", Path = "/dashboard", UserId = memberUserId, CreatedAt = now.AddMinutes(-20) },
            new AnalyticsEvent { EventType = "PageView", Path = "/status/analytics-member", CreatedAt = now.AddMinutes(-15) });
        await db.SaveChangesAsync();
    }
}
