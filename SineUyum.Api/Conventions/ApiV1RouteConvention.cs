using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace SineUyum.Api.Conventions
{
    // Moves every controller route starting with "api/..." under "api/v1/..."
    // in one place, so no controller's [Route] attribute needs editing and the
    // version lives in one spot. When a v2 is needed, per-controller
    // versioning (Asp.Versioning) can be added; for now the whole API is v1.
    public class ApiV1RouteConvention : IApplicationModelConvention
    {
        private const string OldPrefix = "api/";
        private const string NewPrefix = "api/v1/";

        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                foreach (var selector in controller.Selectors)
                {
                    var template = selector.AttributeRouteModel?.Template;
                    if (template == null) continue;

                    if (template.StartsWith(OldPrefix, StringComparison.OrdinalIgnoreCase) &&
                        !template.StartsWith(NewPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        selector.AttributeRouteModel!.Template =
                            NewPrefix + template.Substring(OldPrefix.Length);
                    }
                }
            }
        }
    }
}
