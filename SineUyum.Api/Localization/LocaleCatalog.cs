using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace SineUyum.Api.Localization
{
    // Every user-facing string, loaded once from the embedded
    // Resources/Locales/{language}.json files. Nested JSON objects are flattened
    // into dotted keys ("errors.userNotFound"). Templates use positional
    // placeholders ({0}, {1:0.0}) formatted with the language's culture.
    // Lookup falls back to the default language (en), then to the key itself.
    public sealed class LocaleCatalog
    {
        public const string DefaultLanguage = "en";

        // Matches the LogicalName given to the locale files in the csproj.
        internal const string ResourcePrefix = "SineUyum.Api.Locales.";

        private static readonly Lazy<LocaleCatalog> LazyShared = new(() => FromAssembly(typeof(LocaleCatalog).Assembly));

        // The catalog is immutable, so one instance serves the whole process.
        public static LocaleCatalog Shared => LazyShared.Value;

        private readonly Dictionary<string, Dictionary<string, string>> _strings;

        public LocaleCatalog(IReadOnlyDictionary<string, string> jsonByLanguage)
        {
            _strings = jsonByLanguage.ToDictionary(
                pair => pair.Key.ToLowerInvariant(),
                pair => Flatten(pair.Value));
            if (!_strings.ContainsKey(DefaultLanguage))
            {
                throw new InvalidOperationException($"The default locale file ({DefaultLanguage}.json) is missing.");
            }
        }

        public IReadOnlyCollection<string> Languages => _strings.Keys;

        public IReadOnlyDictionary<string, string> Entries(string language) =>
            _strings.TryGetValue(language, out var entries) ? entries : _strings[DefaultLanguage];

        public bool HasLanguage(string language) => _strings.ContainsKey(language);

        // Template in the given language, else in the default language.
        public bool TryGetTemplate(string? language, string key, out string template)
        {
            if (language != null && _strings.TryGetValue(language, out var entries) && entries.TryGetValue(key, out template!))
            {
                return true;
            }
            return _strings[DefaultLanguage].TryGetValue(key, out template!);
        }

        public string Translate(string? language, string key, params object?[] args)
        {
            if (!TryGetTemplate(language, key, out var template)) return key;
            if (args.Length == 0) return template;
            try
            {
                return string.Format(CultureFor(language), template, args);
            }
            catch (FormatException)
            {
                // A template expecting more arguments must not turn into a 500.
                return template;
            }
        }

        public static CultureInfo CultureFor(string? language)
        {
            try
            {
                return CultureInfo.GetCultureInfo(language ?? DefaultLanguage);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo(DefaultLanguage);
            }
        }

        public static LocaleCatalog FromAssembly(Assembly assembly)
        {
            var files = new Dictionary<string, string>();
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
                    !name.EndsWith(".json", StringComparison.Ordinal))
                {
                    continue;
                }
                var language = name[ResourcePrefix.Length..^".json".Length];
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                files[language] = reader.ReadToEnd();
            }
            return new LocaleCatalog(files);
        }

        private static Dictionary<string, string> Flatten(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            Walk(doc.RootElement, prefix: null, result);
            return result;
        }

        private static void Walk(JsonElement element, string? prefix, Dictionary<string, string> result)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, prefix == null ? property.Name : $"{prefix}.{property.Name}", result);
                }
                return;
            }
            if (element.ValueKind != JsonValueKind.String || prefix == null)
            {
                throw new InvalidOperationException($"Locale entry '{prefix}' must be a string or an object.");
            }
            result[prefix] = element.GetString()!;
        }
    }
}
