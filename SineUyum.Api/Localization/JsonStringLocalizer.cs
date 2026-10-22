using System.Globalization;
using Microsoft.Extensions.Localization;

namespace SineUyum.Api.Localization
{
    // IStringLocalizer over the JSON locale files. Without a fixed language it
    // follows the request culture (CurrentUICulture); with one it always renders
    // that language (e.g. a notification in the recipient's language).
    public sealed class JsonStringLocalizer : IStringLocalizer
    {
        private readonly LocaleCatalog _catalog;
        private readonly string? _language;

        public JsonStringLocalizer(LocaleCatalog catalog, string? language = null)
        {
            _catalog = catalog;
            _language = language;
        }

        public string Language
        {
            get
            {
                var language = _language ?? SupportedLanguages.Current;
                return _catalog.HasLanguage(language) ? language : LocaleCatalog.DefaultLanguage;
            }
        }

        public CultureInfo Culture => LocaleCatalog.CultureFor(Language);

        public LocalizedString this[string name] => Get(name, Array.Empty<object>());

        public LocalizedString this[string name, params object[] arguments] => Get(name, arguments);

        private LocalizedString Get(string name, object[] arguments)
        {
            var language = Language;
            var found = _catalog.TryGetTemplate(language, name, out _);
            return new LocalizedString(name, _catalog.Translate(language, name, arguments), resourceNotFound: !found);
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            _catalog.Entries(Language).Select(e => new LocalizedString(e.Key, e.Value));

        public JsonStringLocalizer ForLanguage(string language) => new(_catalog, language);
    }

    // Every resource source shares the same catalog; this also makes
    // DataAnnotations localization read the locale files.
    public sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
    {
        private readonly LocaleCatalog _catalog;

        public JsonStringLocalizerFactory(LocaleCatalog catalog) => _catalog = catalog;

        public IStringLocalizer Create(Type resourceSource) => new JsonStringLocalizer(_catalog);

        public IStringLocalizer Create(string baseName, string location) => new JsonStringLocalizer(_catalog);
    }

    public static class StringLocalizerExtensions
    {
        // TMDb genre ids are a fixed set; unknown ids keep the stored name.
        public static string GenreName(this IStringLocalizer localizer, int genreId, string fallback)
        {
            var name = localizer[$"genres.{genreId}"];
            return name.ResourceNotFound ? fallback : name.Value;
        }
    }
}
