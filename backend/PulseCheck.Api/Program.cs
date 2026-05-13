using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PulseCheck.Api.Contracts;
using PulseCheck.Api.Data;
using PulseCheck.Api.Extensions;
using PulseCheck.Api.Hubs;
using PulseCheck.Api.Models;
using PulseCheck.Api.Services;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var connectionString = ResolveConnectionString(builder.Configuration);

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PulseCheck";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PulseCheck";
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret is not configured.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/monitors"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var allowedOrigins = (builder.Configuration["PulseCheck:AllowedOrigins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHttpClient("monitor-checker", client =>
{
    client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
});

builder.Services.AddSingleton<AdminAccessService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddHttpClient<EmailDeliveryService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<MonitorStatusClassifier>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddScoped<SslCertificateInspector>();
builder.Services.AddScoped<UptimeCalculator>();
builder.Services.AddScoped<SloService>();
builder.Services.AddScoped<HealthCheckRunner>();
builder.Services.AddHostedService<HealthCheckWorker>();

var app = builder.Build();

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "PulseCheck.Api" }));
app.MapHub<MonitorHub>("/hubs/monitors");

var auth = app.MapGroup("/api/auth");

auth.MapPost("/register", async (
    RegisterRequest request,
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    JwtTokenService jwt,
    CancellationToken cancellationToken) =>
{
    request = request with
    {
        Email = request.Email?.Trim() ?? string.Empty,
        WorkspaceName = request.WorkspaceName?.Trim() ?? string.Empty
    };

    var validation = ValidateRequest(request);
    if (validation is not null)
    {
        return validation;
    }

    if (await userManager.FindByEmailAsync(request.Email) is not null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Email"] = new[] { "An account with this email already exists. Sign in instead." }
        });
    }

    var slug = await CreateUniqueSlugAsync(db, request.WorkspaceName, cancellationToken);
    var user = new ApplicationUser
    {
        UserName = request.Email,
        Email = request.Email,
        EmailConfirmed = false,
        WorkspaceName = request.WorkspaceName.Trim(),
        PublicStatusSlug = slug
    };

    var result = await userManager.CreateAsync(user, request.Password);
    if (!result.Succeeded)
    {
        return ToIdentityValidationProblem(result.Errors);
    }

    db.StatusPages.Add(new StatusPage
    {
        UserId = user.Id,
        Slug = slug,
        Title = $"{user.WorkspaceName} Status"
    });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(jwt.CreateToken(user));
});

auth.MapPost("/login", async (
    LoginRequest request,
    UserManager<ApplicationUser> userManager,
    JwtTokenService jwt) =>
{
    request = request with { Email = request.Email?.Trim() ?? string.Empty };

    var validation = ValidateRequest(request);
    if (validation is not null)
    {
        return validation;
    }

    var user = await userManager.FindByEmailAsync(request.Email);
    if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(jwt.CreateToken(user));
});

auth.MapGet("/me", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    AdminAccessService adminAccess) =>
{
    var user = await userManager.GetUserAsync(principal);
    return user is null
        ? Results.Unauthorized()
        : Results.Ok(ToUserDto(user, adminAccess));
}).RequireAuthorization();

var account = app.MapGroup("/api/account").RequireAuthorization();

account.MapGet("/notification-preferences", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    EmailDeliveryService emailDelivery) =>
{
    var user = await userManager.GetUserAsync(principal);
    return user is null
        ? Results.Unauthorized()
        : Results.Ok(new NotificationPreferencesDto(user.EmailAlertsEnabled, emailDelivery.IsConfigured()));
});

account.MapPut("/notification-preferences", async (
    UpdateNotificationPreferencesRequest request,
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    EmailDeliveryService emailDelivery) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    user.EmailAlertsEnabled = request.EmailAlertsEnabled;
    await userManager.UpdateAsync(user);
    return Results.Ok(new NotificationPreferencesDto(user.EmailAlertsEnabled, emailDelivery.IsConfigured()));
});

var monitors = app.MapGroup("/api/monitors").RequireAuthorization();

monitors.MapGet("/", async (
    ClaimsPrincipal principal,
    AppDbContext db,
    UptimeCalculator uptime,
    CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var userMonitors = await db.Monitors
        .AsNoTracking()
        .Where(monitor => monitor.UserId == userId)
        .OrderBy(monitor => monitor.Name)
        .ToListAsync(cancellationToken);

    var response = new List<MonitorSummaryDto>();
    foreach (var monitor in userMonitors)
    {
        response.Add(await ToSummaryDtoAsync(db, uptime, monitor, cancellationToken));
    }

    return Results.Ok(response);
});

monitors.MapPost("/", async (
    MonitorRequest request,
    ClaimsPrincipal principal,
    AppDbContext db,
    UptimeCalculator uptime,
    CancellationToken cancellationToken) =>
{
    var validation = ValidateMonitorRequest(request);
    if (validation is not null)
    {
        return validation;
    }

    var now = DateTimeOffset.UtcNow;
    var monitor = new MonitorEntity
    {
        UserId = principal.GetUserId(),
        Name = request.Name.Trim(),
        Url = request.Url.Trim(),
        Type = request.Type,
        CheckIntervalSeconds = request.CheckIntervalSeconds,
        TimeoutSeconds = request.TimeoutSeconds,
        DegradedThresholdMs = request.DegradedThresholdMs,
        ExpectedStatusCode = request.ExpectedStatusCode,
        ExpectedKeyword = string.IsNullOrWhiteSpace(request.ExpectedKeyword) ? null : request.ExpectedKeyword.Trim(),
        IsPublic = request.IsPublic,
        CurrentStatus = MonitorStatus.Up,
        NextCheckAt = now,
        CreatedAt = now,
        UpdatedAt = now
    };

    db.Monitors.Add(monitor);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/monitors/{monitor.Id}", await ToSummaryDtoAsync(db, uptime, monitor, cancellationToken));
});

monitors.MapGet("/{id:guid}", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext db,
    UptimeCalculator uptime,
    CancellationToken cancellationToken) =>
{
    var monitor = await FindOwnedMonitorAsync(db, principal.GetUserId(), id, cancellationToken);
    return monitor is null ? Results.NotFound() : Results.Ok(await ToDetailDtoAsync(db, uptime, monitor, cancellationToken));
});

monitors.MapPost("/{id:guid}/check", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext db,
    UptimeCalculator uptime,
    HealthCheckRunner runner,
    CancellationToken cancellationToken) =>
{
    var monitor = await FindOwnedMonitorAsync(db, principal.GetUserId(), id, cancellationToken);
    if (monitor is null)
    {
        return Results.NotFound();
    }

    if (monitor.IsPaused)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Monitor"] = new[] { "Resume this monitor before running a check." }
        });
    }

    await runner.CheckAsync(monitor, cancellationToken);
    return Results.Ok(await ToDetailDtoAsync(db, uptime, monitor, cancellationToken));
});

monitors.MapPut("/{id:guid}", async (
    Guid id,
    MonitorRequest request,
    ClaimsPrincipal principal,
    AppDbContext db,
    UptimeCalculator uptime,
    CancellationToken cancellationToken) =>
{
    var validation = ValidateMonitorRequest(request);
    if (validation is not null)
    {
        return validation;
    }

    var monitor = await FindOwnedMonitorAsync(db, principal.GetUserId(), id, cancellationToken);
    if (monitor is null)
    {
        return Results.NotFound();
    }

    monitor.Name = request.Name.Trim();
    monitor.Url = request.Url.Trim();
    monitor.Type = request.Type;
    monitor.CheckIntervalSeconds = request.CheckIntervalSeconds;
    monitor.TimeoutSeconds = request.TimeoutSeconds;
    monitor.DegradedThresholdMs = request.DegradedThresholdMs;
    monitor.ExpectedStatusCode = request.ExpectedStatusCode;
    monitor.ExpectedKeyword = string.IsNullOrWhiteSpace(request.ExpectedKeyword) ? null : request.ExpectedKeyword.Trim();
    monitor.IsPublic = request.IsPublic;
    monitor.UpdatedAt = DateTimeOffset.UtcNow;

    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(await ToDetailDtoAsync(db, uptime, monitor, cancellationToken));
});

monitors.MapPost("/{id:guid}/pause", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext db,
    UptimeCalculator uptime,
    CancellationToken cancellationToken) =>
{
    var monitor = await FindOwnedMonitorAsync(db, principal.GetUserId(), id, cancellationToken);
    if (monitor is null)
    {
        return Results.NotFound();
    }

    monitor.IsPaused = true;
    monitor.CurrentStatus = MonitorStatus.Paused;
    monitor.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(await ToSummaryDtoAsync(db, uptime, monitor, cancellationToken));
});

monitors.MapPost("/{id:guid}/resume", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext db,
    UptimeCalculator uptime,
    CancellationToken cancellationToken) =>
{
    var monitor = await FindOwnedMonitorAsync(db, principal.GetUserId(), id, cancellationToken);
    if (monitor is null)
    {
        return Results.NotFound();
    }

    monitor.IsPaused = false;
    monitor.CurrentStatus = MonitorStatus.Up;
    monitor.NextCheckAt = DateTimeOffset.UtcNow;
    monitor.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(await ToSummaryDtoAsync(db, uptime, monitor, cancellationToken));
});

monitors.MapDelete("/{id:guid}", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var monitor = await FindOwnedMonitorAsync(db, principal.GetUserId(), id, cancellationToken);
    if (monitor is null)
    {
        return Results.NotFound();
    }

    db.Monitors.Remove(monitor);
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

monitors.MapGet("/{id:guid}/checks", async (
    Guid id,
    string? range,
    ClaimsPrincipal principal,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var monitor = await FindOwnedMonitorAsync(db, principal.GetUserId(), id, cancellationToken);
    if (monitor is null)
    {
        return Results.NotFound();
    }

    var since = ParseRange(range);
    var checks = await db.MonitorChecks
        .AsNoTracking()
        .Where(check => check.MonitorId == id && check.CheckedAt >= since)
        .OrderByDescending(check => check.CheckedAt)
        .Take(300)
        .Select(check => new MonitorCheckDto(
            check.Id,
            check.Status,
            check.StatusCode,
            check.ResponseTimeMs,
            check.ErrorMessage,
            check.CheckedAt))
        .ToListAsync(cancellationToken);

    return Results.Ok(checks);
});

monitors.MapGet("/{id:guid}/incidents", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var monitor = await FindOwnedMonitorAsync(db, principal.GetUserId(), id, cancellationToken);
    if (monitor is null)
    {
        return Results.NotFound();
    }

    var incidents = await db.Incidents
        .AsNoTracking()
        .Where(incident => incident.MonitorId == id)
        .OrderByDescending(incident => incident.StartedAt)
        .Select(incident => new IncidentDto(
            incident.Id,
            incident.Status,
            incident.StartedStatus,
            incident.ResolvedStatus,
            incident.Title,
            incident.Summary,
            incident.StartedAt,
            incident.ResolvedAt))
        .ToListAsync(cancellationToken);

    return Results.Ok(incidents);
});

var dashboard = app.MapGroup("/api/dashboard").RequireAuthorization();

dashboard.MapGet("/summary", async (
    ClaimsPrincipal principal,
    AppDbContext db,
    UptimeCalculator uptime,
    CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var userMonitors = await db.Monitors
        .AsNoTracking()
        .Where(monitor => monitor.UserId == userId)
        .ToListAsync(cancellationToken);

    var openIncidents = await db.Incidents
        .AsNoTracking()
        .CountAsync(incident =>
            incident.Status == IncidentStatus.Open &&
            incident.Monitor != null &&
            incident.Monitor.UserId == userId,
            cancellationToken);

    var uptimes = new List<double>();
    foreach (var monitor in userMonitors)
    {
        uptimes.Add(await uptime.CalculateAsync(db, monitor.Id, DateTimeOffset.UtcNow.AddHours(-24), cancellationToken));
    }

    return Results.Ok(new DashboardSummaryDto(
        userMonitors.Count,
        userMonitors.Count(monitor => monitor.CurrentStatus == MonitorStatus.Up),
        userMonitors.Count(monitor => monitor.CurrentStatus == MonitorStatus.Degraded),
        userMonitors.Count(monitor => monitor.CurrentStatus == MonitorStatus.Error),
        userMonitors.Count(monitor => monitor.CurrentStatus == MonitorStatus.Down),
        userMonitors.Count(monitor => monitor.CurrentStatus == MonitorStatus.Paused),
        openIncidents,
        uptimes.Count == 0 ? 100 : Math.Round(uptimes.Average(), 2)));
});

dashboard.MapGet("/slo", async (
    ClaimsPrincipal principal,
    AppDbContext db,
    SloService slo,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await slo.BuildSummaryAsync(db, principal.GetUserId(), DateTimeOffset.UtcNow, cancellationToken));
});

var analytics = app.MapGroup("/api/analytics");

analytics.MapPost("/events", async (
    AnalyticsEventRequest request,
    ClaimsPrincipal principal,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    request = request with
    {
        EventType = request.EventType?.Trim() ?? string.Empty,
        Path = NormalizeAnalyticsPath(request.Path ?? string.Empty)
    };

    var validation = ValidateRequest(request);
    if (validation is not null)
    {
        return validation;
    }

    if (!string.Equals(request.EventType, "PageView", StringComparison.OrdinalIgnoreCase))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["EventType"] = new[] { "Unsupported analytics event type." }
        });
    }

    db.AnalyticsEvents.Add(new AnalyticsEvent
    {
        UserId = principal.Identity?.IsAuthenticated == true ? principal.GetUserId() : null,
        EventType = "PageView",
        Path = request.Path,
        CreatedAt = DateTimeOffset.UtcNow
    });
    await db.SaveChangesAsync(cancellationToken);

    return Results.NoContent();
});

var admin = app.MapGroup("/api/admin").RequireAuthorization();

admin.MapGet("/analytics/summary", async (
    string? range,
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    AdminAccessService adminAccess,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    if (!adminAccess.IsAdmin(user))
    {
        return Results.Forbid();
    }

    var (normalizedRange, since, now) = ParseAnalyticsRange(range);
    return Results.Ok(await BuildAnalyticsSummaryAsync(db, normalizedRange, since, now, cancellationToken));
});

var notifications = app.MapGroup("/api/notifications").RequireAuthorization();

notifications.MapGet("/", async (
    ClaimsPrincipal principal,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var notifications = await db.Notifications
        .AsNoTracking()
        .Where(notification => notification.UserId == userId)
        .OrderByDescending(notification => notification.CreatedAt)
        .Take(50)
        .ToListAsync(cancellationToken);

    return Results.Ok(notifications.Select(ToNotificationDto));
});

notifications.MapGet("/unread-count", async (
    ClaimsPrincipal principal,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var count = await db.Notifications
        .AsNoTracking()
        .CountAsync(notification => notification.UserId == userId && !notification.IsRead, cancellationToken);

    return Results.Ok(new NotificationUnreadCountDto(count));
});

notifications.MapPost("/{id:guid}/read", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var notification = await db.Notifications
        .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);

    if (notification is null)
    {
        return Results.NotFound();
    }

    notification.IsRead = true;
    notification.ReadAt ??= DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToNotificationDto(notification));
});

notifications.MapPost("/read-all", async (
    ClaimsPrincipal principal,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var unread = await db.Notifications
        .Where(notification => notification.UserId == userId && !notification.IsRead)
        .ToListAsync(cancellationToken);

    var now = DateTimeOffset.UtcNow;
    foreach (var notification in unread)
    {
        notification.IsRead = true;
        notification.ReadAt = now;
    }

    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(new NotificationUnreadCountDto(0));
});

notifications.MapDelete("/{id:guid}", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var notification = await db.Notifications
        .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);

    if (notification is null)
    {
        return Results.NotFound();
    }

    db.Notifications.Remove(notification);
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

var publicApi = app.MapGroup("/api/public");

publicApi.MapGet("/status/{slug}", async (
    string slug,
    AppDbContext db,
    UptimeCalculator uptime,
    CancellationToken cancellationToken) =>
{
    var page = await db.StatusPages
        .AsNoTracking()
        .FirstOrDefaultAsync(statusPage => statusPage.Slug == slug && statusPage.IsEnabled, cancellationToken);

    if (page is null)
    {
        return Results.NotFound();
    }

    var monitors = await db.Monitors
        .AsNoTracking()
        .Where(monitor => monitor.UserId == page.UserId && monitor.IsPublic)
        .OrderBy(monitor => monitor.Name)
        .ToListAsync(cancellationToken);

    var publicMonitors = new List<PublicMonitorDto>();
    foreach (var monitor in monitors)
    {
        publicMonitors.Add(new PublicMonitorDto(
            monitor.Id,
            monitor.Name,
            monitor.Type,
            monitor.CurrentStatus,
            monitor.LastCheckedAt,
            monitor.LastResponseTimeMs,
            await uptime.CalculateAsync(db, monitor.Id, DateTimeOffset.UtcNow.AddHours(-24), cancellationToken),
            await uptime.CalculateAsync(db, monitor.Id, DateTimeOffset.UtcNow.AddDays(-7), cancellationToken),
            await uptime.CalculateAsync(db, monitor.Id, DateTimeOffset.UtcNow.AddDays(-30), cancellationToken)));
    }

    var monitorIds = monitors.Select(monitor => monitor.Id).ToArray();
    var incidents = await db.Incidents
        .AsNoTracking()
        .Where(incident => monitorIds.Contains(incident.MonitorId))
        .OrderByDescending(incident => incident.StartedAt)
        .Take(10)
        .Select(incident => new PublicIncidentDto(
            incident.Id,
            incident.Monitor == null ? "Monitor" : incident.Monitor.Name,
            incident.Status,
            incident.Title,
            incident.StartedAt,
            incident.ResolvedAt))
        .ToListAsync(cancellationToken);

    return Results.Ok(new PublicStatusPageDto(
        page.Slug,
        page.Title,
        OverallStatus(monitors),
        publicMonitors.Count == 0 ? 100 : Math.Round(publicMonitors.Average(monitor => monitor.Uptime24Hours), 2),
        publicMonitors.Count == 0 ? 100 : Math.Round(publicMonitors.Average(monitor => monitor.Uptime7Days), 2),
        publicMonitors.Count == 0 ? 100 : Math.Round(publicMonitors.Average(monitor => monitor.Uptime30Days), 2),
        publicMonitors,
        incidents));
});

if (app.Configuration.GetValue("PulseCheck:AutoMigrate", app.Environment.IsDevelopment()))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await ApplySchemaPatchesAsync(db);
    await DeleteLegacyDemoAccountAsync(scope.ServiceProvider);
}

await app.RunAsync();

static string ResolveConnectionString(IConfiguration configuration)
{
    var databaseUrl = configuration["DATABASE_URL"];
    if (!string.IsNullOrWhiteSpace(databaseUrl) &&
        Uri.TryCreate(databaseUrl, UriKind.Absolute, out var databaseUri))
    {
        return BuildPostgresConnectionString(databaseUri);
    }

    var pgHost = configuration["PGHOST"];
    var pgDatabase = configuration["PGDATABASE"];
    var pgUser = configuration["PGUSER"];
    var pgPassword = configuration["PGPASSWORD"];
    if (!string.IsNullOrWhiteSpace(pgHost) &&
        !string.IsNullOrWhiteSpace(pgDatabase) &&
        !string.IsNullOrWhiteSpace(pgUser) &&
        !string.IsNullOrWhiteSpace(pgPassword))
    {
        return new NpgsqlConnectionStringBuilder
        {
            Host = pgHost,
            Port = configuration.GetValue("PGPORT", 5432),
            Database = pgDatabase,
            Username = pgUser,
            Password = pgPassword,
            SslMode = SslMode.Require
        }.ConnectionString;
    }

    return configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is not configured.");
}

static string BuildPostgresConnectionString(Uri databaseUri)
{
    var credentials = databaseUri.UserInfo.Split(':', 2);
    var database = databaseUri.AbsolutePath.TrimStart('/');

    return new NpgsqlConnectionStringBuilder
    {
        Host = databaseUri.Host,
        Port = databaseUri.IsDefaultPort ? 5432 : databaseUri.Port,
        Database = Uri.UnescapeDataString(database),
        Username = credentials.Length > 0 ? Uri.UnescapeDataString(credentials[0]) : string.Empty,
        Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty,
        SslMode = SslMode.Require
    }.ConnectionString;
}

static string NormalizeAnalyticsPath(string path)
{
    path = path.Trim();
    var queryIndex = path.IndexOfAny(new[] { '?', '#' });
    if (queryIndex >= 0)
    {
        path = path[..queryIndex];
    }

    if (string.IsNullOrWhiteSpace(path))
    {
        return "/";
    }

    if (!path.StartsWith('/'))
    {
        path = $"/{path.TrimStart('/')}";
    }

    return path.Length > 300 ? path[..300] : path;
}

static (string Range, DateTimeOffset Since, DateTimeOffset Now) ParseAnalyticsRange(string? range)
{
    var now = DateTimeOffset.UtcNow;
    return range?.ToLowerInvariant() switch
    {
        "24h" => ("24h", now.AddHours(-24), now),
        "30d" => ("30d", now.AddDays(-30), now),
        _ => ("7d", now.AddDays(-7), now)
    };
}

static async Task<AnalyticsSummaryDto> BuildAnalyticsSummaryAsync(
    AppDbContext db,
    string range,
    DateTimeOffset since,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    var pageViewEvents = db.AnalyticsEvents
        .AsNoTracking()
        .Where(analyticsEvent => analyticsEvent.EventType == "PageView" && analyticsEvent.CreatedAt >= since);

    var totalUsers = await db.Users.AsNoTracking().CountAsync(cancellationToken);
    var newUsers = await db.Users.AsNoTracking().CountAsync(user => user.CreatedAt >= since, cancellationToken);
    var activeUsers = await pageViewEvents
        .Where(analyticsEvent => analyticsEvent.UserId != null)
        .Select(analyticsEvent => analyticsEvent.UserId)
        .Distinct()
        .CountAsync(cancellationToken);
    var totalMonitors = await db.Monitors.AsNoTracking().CountAsync(cancellationToken);
    var monitorsCreated = await db.Monitors.AsNoTracking().CountAsync(monitor => monitor.CreatedAt >= since, cancellationToken);
    var pageViews = await pageViewEvents.CountAsync(cancellationToken);
    var publicStatusPageViews = await pageViewEvents.CountAsync(
        analyticsEvent => analyticsEvent.Path.StartsWith("/status/"),
        cancellationToken);

    var topPageRows = await pageViewEvents
        .GroupBy(analyticsEvent => analyticsEvent.Path)
        .Select(group => new { Path = group.Key, Views = group.Count() })
        .OrderByDescending(page => page.Views)
        .ThenBy(page => page.Path)
        .Take(8)
        .ToListAsync(cancellationToken);
    var topPages = topPageRows
        .Select(page => new AnalyticsTopPageDto(page.Path, page.Views))
        .ToList();

    var checks = db.MonitorChecks.AsNoTracking().Where(check => check.CheckedAt >= since);
    var monitorChecks = await checks.CountAsync(cancellationToken);
    var responseTimes = checks
        .Where(check => check.ResponseTimeMs != null)
        .Select(check => check.ResponseTimeMs!.Value);
    var averageResponseTime = await responseTimes.AnyAsync(cancellationToken)
        ? Math.Round(await responseTimes.AverageAsync(cancellationToken), 0)
        : (double?)null;
    var checkStatusRows = await checks
        .GroupBy(check => check.Status)
        .Select(group => new { Status = group.Key, Count = group.Count() })
        .OrderBy(item => item.Status)
        .ToListAsync(cancellationToken);
    var checkStatusCounts = checkStatusRows
        .Select(item => new AnalyticsMonitorStatusCountDto(item.Status, item.Count))
        .ToList();

    var incidents = db.Incidents.AsNoTracking();
    var notifications = db.Notifications.AsNoTracking().Where(notification => notification.CreatedAt >= since);
    var emailStatusRows = await notifications
        .GroupBy(notification => notification.EmailStatus)
        .Select(group => new { Status = group.Key, Count = group.Count() })
        .OrderBy(item => item.Status)
        .ToListAsync(cancellationToken);
    var emailStatusCounts = emailStatusRows
        .Select(item => new AnalyticsEmailStatusCountDto(item.Status, item.Count))
        .ToList();
    var monitorActivityRows = await db.Monitors
        .AsNoTracking()
        .Select(monitor => new
        {
            monitor.Id,
            monitor.Name,
            monitor.Url,
            monitor.CurrentStatus,
            CheckCount = monitor.Checks.Count(check => check.CheckedAt >= since),
            LastCheckedAt = monitor.Checks
                .Where(check => check.CheckedAt >= since)
                .Select(check => (DateTimeOffset?)check.CheckedAt)
                .Max()
        })
        .OrderByDescending(monitor => monitor.CheckCount)
        .ThenBy(monitor => monitor.Name)
        .Take(8)
        .ToListAsync(cancellationToken);
    var monitorActivity = monitorActivityRows
        .Select(monitor => new AnalyticsMonitorActivityDto(
            monitor.Id,
            monitor.Name,
            monitor.Url,
            monitor.CurrentStatus,
            monitor.CheckCount,
            monitor.LastCheckedAt))
        .ToList();
    var signupTimes = await db.Users
        .AsNoTracking()
        .Where(user => user.CreatedAt >= since)
        .Select(user => user.CreatedAt)
        .ToListAsync(cancellationToken);
    var recentSignups = await db.Users
        .AsNoTracking()
        .OrderByDescending(user => user.CreatedAt)
        .Take(8)
        .Select(user => new AnalyticsRecentSignupDto(
            user.Id,
            user.Email ?? string.Empty,
            user.WorkspaceName,
            user.CreatedAt))
        .ToListAsync(cancellationToken);

    return new AnalyticsSummaryDto(
        range,
        since,
        now,
        totalUsers,
        newUsers,
        activeUsers,
        totalMonitors,
        monitorsCreated,
        totalUsers == 0 ? 0 : Math.Round((double)totalMonitors / totalUsers, 2),
        pageViews,
        publicStatusPageViews,
        monitorChecks,
        averageResponseTime,
        await incidents.CountAsync(incident => incident.StartedAt >= since, cancellationToken),
        await incidents.CountAsync(incident => incident.ResolvedAt != null && incident.ResolvedAt >= since, cancellationToken),
        await notifications.CountAsync(cancellationToken),
        topPages,
        checkStatusCounts,
        emailStatusCounts,
        monitorActivity,
        BuildSignupSeries(signupTimes, range, since, now),
        recentSignups);
}

static IReadOnlyCollection<AnalyticsSeriesPointDto> BuildSignupSeries(
    IReadOnlyCollection<DateTimeOffset> signupTimes,
    string range,
    DateTimeOffset since,
    DateTimeOffset now)
{
    var bucketCount = range == "24h" ? 24 : range == "30d" ? 30 : 7;
    var bucketSize = range == "24h" ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
    var firstBucket = range == "24h" ? TruncateToHour(now).AddHours(-(bucketCount - 1)) : TruncateToDay(now).AddDays(-(bucketCount - 1));
    var points = new List<AnalyticsSeriesPointDto>();

    for (var index = 0; index < bucketCount; index++)
    {
        var start = firstBucket.AddTicks(bucketSize.Ticks * index);
        var end = start.Add(bucketSize);
        points.Add(new AnalyticsSeriesPointDto(
            start,
            signupTimes.Count(timestamp => timestamp >= start && timestamp < end)));
    }

    return points;
}

static DateTimeOffset TruncateToHour(DateTimeOffset value)
{
    return new DateTimeOffset(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Offset);
}

static DateTimeOffset TruncateToDay(DateTimeOffset value)
{
    return new DateTimeOffset(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);
}

static IResult? ValidateRequest<T>(T request)
{
    var context = new ValidationContext(request!);
    var results = new List<ValidationResult>();

    if (Validator.TryValidateObject(request!, context, results, true))
    {
        return null;
    }

    return Results.ValidationProblem(results
        .Where(result => result.MemberNames.Any())
        .ToDictionary(result => result.MemberNames.First(), result => new[] { result.ErrorMessage ?? "Invalid value." }));
}

static IResult? ValidateMonitorRequest(MonitorRequest request)
{
    var validation = ValidateRequest(request);
    if (validation is not null)
    {
        return validation;
    }

    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Url"] = new[] { "Monitor URL must be an absolute HTTP or HTTPS URL." }
        });
    }

    if (request.TimeoutSeconds * 1000 >= request.CheckIntervalSeconds * 1000)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["TimeoutSeconds"] = new[] { "Timeout must be shorter than the check interval." }
        });
    }

    return null;
}

static async Task<string> CreateUniqueSlugAsync(AppDbContext db, string workspaceName, CancellationToken cancellationToken)
{
    var baseSlug = ToSlug(workspaceName);
    var slug = baseSlug;
    var suffix = 2;

    while (await db.StatusPages.AnyAsync(page => page.Slug == slug, cancellationToken))
    {
        slug = $"{baseSlug}-{suffix++}";
    }

    return slug;
}

static string ToSlug(string value)
{
    var builder = new StringBuilder();
    var previousWasDash = false;

    foreach (var character in value.Trim().ToLowerInvariant())
    {
        if (char.IsLetterOrDigit(character))
        {
            builder.Append(character);
            previousWasDash = false;
        }
        else if (!previousWasDash)
        {
            builder.Append('-');
            previousWasDash = true;
        }
    }

    var slug = builder.ToString().Trim('-');
    return string.IsNullOrWhiteSpace(slug) ? $"status-{Guid.NewGuid():N}"[..18] : slug[..Math.Min(slug.Length, 60)];
}

static string ToFriendlyIdentityError(IdentityError error)
{
    return error.Code switch
    {
        "PasswordTooShort" => "Use at least 8 characters.",
        "PasswordRequiresUpper" => "Add at least one uppercase letter.",
        "PasswordRequiresLower" => "Add at least one lowercase letter.",
        "PasswordRequiresDigit" => "Add at least one number.",
        "PasswordRequiresNonAlphanumeric" => "Add at least one symbol.",
        "DuplicateUserName" or "DuplicateEmail" => "An account with this email already exists. Sign in instead.",
        "InvalidEmail" => "Enter a valid email address.",
        _ => error.Description
    };
}

static IResult ToIdentityValidationProblem(IEnumerable<IdentityError> errors)
{
    var grouped = new Dictionary<string, List<string>>();

    foreach (var error in errors)
    {
        var field = error.Code switch
        {
            "DuplicateUserName" or "DuplicateEmail" or "InvalidEmail" => "Email",
            _ when error.Code.StartsWith("Password", StringComparison.OrdinalIgnoreCase) => "Password",
            _ => "Account"
        };

        if (!grouped.TryGetValue(field, out var messages))
        {
            messages = new List<string>();
            grouped[field] = messages;
        }

        messages.Add(ToFriendlyIdentityError(error));
    }

    return Results.ValidationProblem(grouped.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.Distinct().ToArray()));
}

static async Task DeleteLegacyDemoAccountAsync(IServiceProvider services)
{
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var demoUser = await userManager.FindByEmailAsync("demo@pulsecheck.local");

    if (demoUser is not null)
    {
        await userManager.DeleteAsync(demoUser);
    }
}

static async Task ApplySchemaPatchesAsync(AppDbContext db)
{
    if (!db.Database.IsRelational())
    {
        return;
    }

    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Monitors" ADD COLUMN IF NOT EXISTS "SslCertificateStatus" integer NOT NULL DEFAULT 0;
        ALTER TABLE "Monitors" ADD COLUMN IF NOT EXISTS "SslCertificateExpiresAt" timestamp with time zone NULL;
        ALTER TABLE "Monitors" ADD COLUMN IF NOT EXISTS "SslCertificateDaysRemaining" integer NULL;
        ALTER TABLE "Monitors" ADD COLUMN IF NOT EXISTS "LastSslErrorMessage" character varying(500) NULL;
        ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "EmailAlertsEnabled" boolean NOT NULL DEFAULT TRUE;
        ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW();

        CREATE TABLE IF NOT EXISTS "Notifications" (
            "Id" uuid NOT NULL,
            "UserId" uuid NOT NULL,
            "MonitorId" uuid NULL,
            "IncidentId" uuid NULL,
            "Type" integer NOT NULL,
            "Title" character varying(160) NOT NULL,
            "Message" character varying(800) NOT NULL,
            "DedupKey" character varying(160) NOT NULL,
            "IsRead" boolean NOT NULL,
            "EmailStatus" integer NOT NULL,
            "EmailErrorMessage" character varying(500) NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "ReadAt" timestamp with time zone NULL,
            CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_Notifications_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_Notifications_Monitors_MonitorId" FOREIGN KEY ("MonitorId") REFERENCES "Monitors" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_Notifications_Incidents_IncidentId" FOREIGN KEY ("IncidentId") REFERENCES "Incidents" ("Id") ON DELETE SET NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Notifications_DedupKey" ON "Notifications" ("DedupKey");
        CREATE INDEX IF NOT EXISTS "IX_Notifications_UserId_IsRead_CreatedAt" ON "Notifications" ("UserId", "IsRead", "CreatedAt");
        CREATE INDEX IF NOT EXISTS "IX_Notifications_MonitorId" ON "Notifications" ("MonitorId");
        CREATE INDEX IF NOT EXISTS "IX_Notifications_IncidentId" ON "Notifications" ("IncidentId");

        CREATE TABLE IF NOT EXISTS "AnalyticsEvents" (
            "Id" uuid NOT NULL,
            "UserId" uuid NULL,
            "EventType" character varying(80) NOT NULL,
            "Path" character varying(300) NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_AnalyticsEvents" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_AnalyticsEvents_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE SET NULL
        );

        CREATE INDEX IF NOT EXISTS "IX_AnalyticsEvents_EventType_CreatedAt" ON "AnalyticsEvents" ("EventType", "CreatedAt");
        CREATE INDEX IF NOT EXISTS "IX_AnalyticsEvents_Path_CreatedAt" ON "AnalyticsEvents" ("Path", "CreatedAt");
        CREATE INDEX IF NOT EXISTS "IX_AnalyticsEvents_UserId_CreatedAt" ON "AnalyticsEvents" ("UserId", "CreatedAt");
        """);
}

static async Task<MonitorEntity?> FindOwnedMonitorAsync(AppDbContext db, Guid userId, Guid monitorId, CancellationToken cancellationToken)
{
    return await db.Monitors.FirstOrDefaultAsync(
        monitor => monitor.Id == monitorId && monitor.UserId == userId,
        cancellationToken);
}

static async Task<MonitorSummaryDto> ToSummaryDtoAsync(
    AppDbContext db,
    UptimeCalculator uptime,
    MonitorEntity monitor,
    CancellationToken cancellationToken)
{
    var since = DateTimeOffset.UtcNow.AddHours(-24);
    var uptimePercentage = await uptime.CalculateAsync(db, monitor.Id, since, cancellationToken);
    var openIncidentCount = await db.Incidents
        .AsNoTracking()
        .CountAsync(incident => incident.MonitorId == monitor.Id && incident.Status == IncidentStatus.Open, cancellationToken);

    return new MonitorSummaryDto(
        monitor.Id,
        monitor.Name,
        monitor.Url,
        monitor.Type,
        monitor.CurrentStatus,
        monitor.IsPaused,
        monitor.IsPublic,
        monitor.LastCheckedAt,
        monitor.LastStatusCode,
        monitor.LastResponseTimeMs,
        monitor.LastErrorMessage,
        monitor.SslCertificateStatus,
        monitor.SslCertificateExpiresAt,
        monitor.SslCertificateDaysRemaining,
        monitor.LastSslErrorMessage,
        uptimePercentage,
        openIncidentCount);
}

static async Task<MonitorDetailDto> ToDetailDtoAsync(
    AppDbContext db,
    UptimeCalculator uptime,
    MonitorEntity monitor,
    CancellationToken cancellationToken)
{
    return new MonitorDetailDto(
        monitor.Id,
        monitor.Name,
        monitor.Url,
        monitor.Type,
        monitor.CurrentStatus,
        monitor.IsPaused,
        monitor.IsPublic,
        monitor.CheckIntervalSeconds,
        monitor.TimeoutSeconds,
        monitor.DegradedThresholdMs,
        monitor.ExpectedStatusCode,
        monitor.ExpectedKeyword,
        monitor.LastCheckedAt,
        monitor.LastStatusCode,
        monitor.LastResponseTimeMs,
        monitor.LastErrorMessage,
        monitor.SslCertificateStatus,
        monitor.SslCertificateExpiresAt,
        monitor.SslCertificateDaysRemaining,
        monitor.LastSslErrorMessage,
        await uptime.CalculateAsync(db, monitor.Id, DateTimeOffset.UtcNow.AddHours(-24), cancellationToken),
        await uptime.CalculateAsync(db, monitor.Id, DateTimeOffset.UtcNow.AddDays(-7), cancellationToken),
        await uptime.CalculateAsync(db, monitor.Id, DateTimeOffset.UtcNow.AddDays(-30), cancellationToken));
}

static NotificationDto ToNotificationDto(Notification notification)
{
    return new NotificationDto(
        notification.Id,
        notification.Type,
        notification.MonitorId,
        notification.IncidentId,
        notification.Title,
        notification.Message,
        notification.IsRead,
        notification.EmailStatus,
        notification.EmailErrorMessage,
        notification.CreatedAt,
        notification.ReadAt);
}

static UserDto ToUserDto(ApplicationUser user, AdminAccessService adminAccess)
{
    return new UserDto(
        user.Id,
        user.Email ?? string.Empty,
        user.WorkspaceName,
        user.PublicStatusSlug,
        user.EmailAlertsEnabled,
        adminAccess.IsAdmin(user));
}

static DateTimeOffset ParseRange(string? range)
{
    return range?.ToLowerInvariant() switch
    {
        "7d" => DateTimeOffset.UtcNow.AddDays(-7),
        "30d" => DateTimeOffset.UtcNow.AddDays(-30),
        _ => DateTimeOffset.UtcNow.AddHours(-24)
    };
}

static MonitorStatus OverallStatus(IReadOnlyCollection<MonitorEntity> monitors)
{
    if (monitors.Any(monitor => monitor.CurrentStatus == MonitorStatus.Down))
    {
        return MonitorStatus.Down;
    }

    if (monitors.Any(monitor => monitor.CurrentStatus == MonitorStatus.Error))
    {
        return MonitorStatus.Error;
    }

    if (monitors.Any(monitor => monitor.CurrentStatus == MonitorStatus.Degraded))
    {
        return MonitorStatus.Degraded;
    }

    if (monitors.Any(monitor => monitor.CurrentStatus == MonitorStatus.Paused))
    {
        return MonitorStatus.Paused;
    }

    return MonitorStatus.Up;
}

public partial class Program
{
}
