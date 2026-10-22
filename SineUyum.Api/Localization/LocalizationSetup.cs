using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace SineUyum.Api.Localization
{
    // Request culture: "lang" query string (e.g. SignalR negotiate, which cannot
    // always set headers), then Accept-Language. Unsupported or missing → en.
    // Regional cultures fall back to their neutral parent (tr-TR → tr).
    public static class LocalizationSetup
    {
        public static IServiceCollection AddSineUyumLocalization(this IServiceCollection services, IConfiguration configuration)
        {
            var settings = configuration.GetSection(LocalizationSettings.SectionName).Get<LocalizationSettings>() ?? new();
            var supported = new SupportedLanguages(settings.SupportedLanguages);

            services.AddSingleton(supported);
            services.AddSingleton(LocaleCatalog.Shared);
            services.AddLocalization();
            services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory, JsonStringLocalizerFactory>());
            services.AddSingleton<IStringLocalizer>(sp => new JsonStringLocalizer(sp.GetRequiredService<LocaleCatalog>()));

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var cultures = supported.All.Select(CultureInfo.GetCultureInfo).ToList();
                options.DefaultRequestCulture = new RequestCulture(SupportedLanguages.Default);
                options.SupportedCultures = cultures;
                options.SupportedUICultures = cultures;
                options.FallBackToParentCultures = true;
                options.FallBackToParentUICultures = true;
                options.RequestCultureProviders = new List<IRequestCultureProvider>
                {
                    new QueryStringRequestCultureProvider { QueryStringKey = "lang", UIQueryStringKey = "lang" },
                    new AcceptLanguageHeaderRequestCultureProvider(),
                };
            });

            // MVC's automatic validation response ("One or more validation
            // errors occurred.") gets a localized title too.
            services.Configure<ProblemDetailsOptions>(options =>
            {
                var previous = options.CustomizeProblemDetails;
                options.CustomizeProblemDetails = context =>
                {
                    previous?.Invoke(context);
                    if (context.ProblemDetails is ValidationProblemDetails validation)
                    {
                        validation.Title = context.HttpContext.RequestServices
                            .GetRequiredService<IStringLocalizer>()["errors.validationFailed"];
                    }
                };
            });

            return services;
        }

        // True if the client chose the language (query or header) rather than
        // getting the default; only then is the user's stored language synced.
        public static string? ExplicitRequestLanguage(this HttpContext context, SupportedLanguages supported)
        {
            var feature = context.Features.Get<IRequestCultureFeature>();
            return feature?.Provider == null ? null : supported.Match(feature.RequestCulture.UICulture.Name);
        }
    }
}
