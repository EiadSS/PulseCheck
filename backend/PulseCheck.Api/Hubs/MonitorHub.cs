using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PulseCheck.Api.Hubs;

[Authorize]
public sealed class MonitorHub : Hub
{
}
