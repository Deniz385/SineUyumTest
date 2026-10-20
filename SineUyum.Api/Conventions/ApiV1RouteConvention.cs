using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace SineUyum.Api.Conventions
{
    // Tüm "api/..." ile başlayan controller rotalarını tek noktadan "api/v1/..."
    // altına taşır. Böylece her controller'ın [Route] özniteliğini tek tek
    // düzenlemeye gerek kalmaz ve sürüm bilgisi tek yerde durur. İleride v2
    // gerektiğinde controller bazında sürümleme (Asp.Versioning) eklenebilir;
    // şimdilik tüm API tek sürüm (v1) altında.
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
