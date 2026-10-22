using System.Globalization;

namespace SineUyum.Api.Localization
{
    // "Localization" section. The supported languages are listed in exactly one
    // place: appsettings.json "Localization:SupportedLanguages". Adding a
    // language = adding Resources/Locales/{code}.json and the code to that list.
    public class LocalizationSettings
    {
        public const string SectionName = "Localization";

        public string[] SupportedLanguages { get; set; } = Array.Empty<string>();
    }

    // Two-letter neutral language codes ("en", "tr"). The default (en) is
    // always supported: it is the fallback for everything else.
    public sealed class SupportedLanguages
    {
        public const string Default = LocaleCatalog.DefaultLanguage;

        public SupportedLanguages(IEnumerable<string> codes)
        {
            All = new[] { Default }
                .Concat(codes.Select(c => c.Trim().ToLowerInvariant()))
                .Where(c => c.Length > 0)
                .Distinct()
                .ToList();
        }

        public IReadOnlyList<string> All { get; }

        // "tr", "TR", "tr-TR" and "tr_TR" all match "tr"; null if unsupported.
        public string? Match(string? requested)
        {
            if (string.IsNullOrWhiteSpace(requested)) return null;
            var neutral = requested.Trim().Split('-', '_')[0].ToLowerInvariant();
            return All.Contains(neutral) ? neutral : null;
        }

        public string Resolve(string? requested) => Match(requested) ?? Default;

        // Language of the current request (set by the request localization
        // middleware; always a supported language there).
        public static string Current => FromCulture(CultureInfo.CurrentUICulture);

        public static string FromCulture(CultureInfo culture)
        {
            while (!culture.IsNeutralCulture && culture.Parent != CultureInfo.InvariantCulture)
            {
                culture = culture.Parent;
            }
            return culture.Name.Length == 0 ? Default : culture.Name.ToLowerInvariant();
        }

        // TMDb expects a region for the languages it localizes best.
        public static string TmdbLanguage(string language) => language switch
        {
            "tr" => "tr-TR",
            "en" => "en-US",
            _ => language
        };
    }
}
