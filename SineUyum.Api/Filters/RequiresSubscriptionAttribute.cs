using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Services;

namespace SineUyum.Api.Filters
{
    // Ücretli özellik: aktif abonelik yoksa 402 döner. Kontrol SUNUCUDA ve
    // veritabanından yapılır. Önceden etkinlik özelliği yalnızca web
    // istemcisinde gizleniyordu (mobilde hiç gizlenmiyordu); API'yi doğrudan
    // çağıran herkes ödemeden etkinliğe katılabiliyordu.
    public sealed class RequiresSubscriptionAttribute : TypeFilterAttribute
    {
        public RequiresSubscriptionAttribute() : base(typeof(RequiresSubscriptionFilter)) { }

        private sealed class RequiresSubscriptionFilter : IAsyncActionFilter
        {
            private readonly SubscriptionService _subscriptions;

            public RequiresSubscriptionFilter(SubscriptionService subscriptions) => _subscriptions = subscriptions;

            public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
            {
                var user = context.HttpContext.User;
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

                // Yöneticiler etkinlikleri test edebilsin.
                if (userId != null && !user.IsInRole("Admin") && !await _subscriptions.IsActiveAsync(userId))
                {
                    throw new DomainException(
                        "Bu özellik için aktif abonelik gerekli.",
                        StatusCodes.Status402PaymentRequired);
                }

                await next();
            }
        }
    }
}
