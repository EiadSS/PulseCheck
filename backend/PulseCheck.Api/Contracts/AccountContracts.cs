namespace PulseCheck.Api.Contracts;

public sealed record NotificationPreferencesDto(bool EmailAlertsEnabled, bool EmailDeliveryConfigured);

public sealed record UpdateNotificationPreferencesRequest(bool EmailAlertsEnabled);
