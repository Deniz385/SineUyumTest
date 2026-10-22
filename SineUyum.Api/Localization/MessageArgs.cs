using System.Text.Json;

namespace SineUyum.Api.Localization
{
    // Arguments of a stored message key (notifications, matching errors) as a
    // JSON array. Numbers stay numbers so they are re-formatted per culture
    // when the text is rendered in another language; movies are stored as
    // MovieRef so their titles follow the language too.
    public static class MessageArgs
    {
        public static string? Serialize(IReadOnlyCollection<object?> args) =>
            args.Count == 0 ? null : JsonSerializer.Serialize(args);

        public static object[] Deserialize(string? json)
        {
            if (string.IsNullOrEmpty(json)) return Array.Empty<object>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<object>();
                return doc.RootElement.EnumerateArray().Select(ToValue).ToArray();
            }
            catch (JsonException)
            {
                return Array.Empty<object>();
            }
        }

        private static object ToValue(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Number when e.TryGetInt64(out var whole) => whole,
            JsonValueKind.Number => e.GetDouble(),
            JsonValueKind.String => e.GetString() ?? string.Empty,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.Object when e.TryGetProperty(nameof(MovieRef.MovieId), out var id) && id.TryGetInt32(out var movieId) =>
                new MovieRef(movieId, e.TryGetProperty(nameof(MovieRef.Title), out var title) ? title.GetString() ?? string.Empty : string.Empty),
            _ => e.GetRawText()
        };
    }
}
