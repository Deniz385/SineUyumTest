using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Services;

namespace SineUyum.Api.Filters
{
    // Paid feature: returns 402 without an active subscription. The check runs
    // ON THE SERVER, against the database. The events feature used to be hidden
    // only in the web client (not at all on mobile); anyone calling the API
    // directly could join events without paying.
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

                // Admins can test events.
                if (userId != null && !user.IsInRole("Admin") && !await _subscriptions.IsActiveAsync(userId))
                {
                    throw new DomainException(
                        "subscriptions.required",
                        StatusCodes.Status402PaymentRequired);
                }

                await next();
            }
        }
    }
}
