using PulseCheck.Api.Models;

namespace PulseCheck.Api.Contracts;

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    Guid? MonitorId,
    Guid? IncidentId,
    string Title,
    string Message,
    bool IsRead,
    NotificationEmailStatus EmailStatus,
    string? EmailErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record NotificationUnreadCountDto(int UnreadCount);
