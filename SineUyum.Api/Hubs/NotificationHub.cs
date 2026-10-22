// SineUyum.Api/Hubs/NotificationHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SineUyum.Api.Hubs
{
    // Real-time events from the server to clients: "ReceiveNotification"
    // (notifications) and "ReceiveMessage" (live chat). Events are sent to the
    // user id (JWT NameIdentifier); all open connections of that user get them.
    // Notification texts are rendered in the recipient's stored language.
    //
    // Only signed-in users can connect. Anonymous connections used to be
    // accepted too: they never received events but held server resources for
    // nothing. WebSockets cannot carry headers, so the token comes in the
    // "access_token" query parameter (see Program.cs).
    [Authorize]
    public class NotificationHub : Hub
    {
    }
}
