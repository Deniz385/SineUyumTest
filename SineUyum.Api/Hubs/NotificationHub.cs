// SineUyum.Api/Hubs/NotificationHub.cs
using Microsoft.AspNetCore.SignalR;

namespace SineUyum.Api.Hubs
{
    // Bu Hub, sunucu ile istemciler (tarayıcılar) arasında anlık bir bağlantı kurar.
    public class NotificationHub : Hub
    {
        // İstemcilerin bu huba bağlanıp ayrıldığında özel bir işlem yapmamıza gerek yok,
        // bu yüzden içi şimdilik boş kalabilir. SignalR bağlantıyı kendi yönetir.
    }
}

