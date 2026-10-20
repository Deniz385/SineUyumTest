// SineUyum.Api/Hubs/NotificationHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SineUyum.Api.Hubs
{
    // Sunucudan istemcilere anlık olaylar: "ReceiveNotification" (bildirim) ve
    // "ReceiveMessage" (canlı sohbet). Olaylar kullanıcı kimliğine (JWT
    // NameIdentifier) gönderilir; aynı kullanıcının tüm açık bağlantıları alır.
    //
    // Yalnızca giriş yapmış kullanıcılar bağlanabilir. Önceden kimliksiz
    // bağlantılar da kabul ediliyordu: hiçbir olay almasalar da sunucuda boşuna
    // kaynak tutuyorlardı. Token, WebSocket başlık taşıyamadığı için
    // "access_token" sorgu parametresiyle gelir (bkz. Program.cs).
    [Authorize]
    public class NotificationHub : Hub
    {
    }
}
