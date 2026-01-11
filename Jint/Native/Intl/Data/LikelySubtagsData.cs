using System.IO;

namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides access to CLDR likely subtags data for locale maximize/minimize operations.
/// Data is lazy-loaded from embedded text resource (key=value format).
/// </summary>
internal static class LikelySubtagsData
{
    private static Dictionary<string, string>? _likelySubtags;

    private static Dictionary<string, string> LikelySubtags => _likelySubtags ??= Load();

    public static bool TryResolve(string key, out string value)
    {
        return LikelySubtags.TryGetValue(key, out value!);
    }

    private static Dictionary<string, string> Load()
    {
        var assembly = typeof(LikelySubtagsData).Assembly;
        const string resourceName = "Jint.Native.Intl.Data.LikelySubtags.txt";
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               "Could not load embedded Intl likely-subtags data.");

        using var reader = new StreamReader(stream);
        var data = new Dictionary<string, string>(8000, StringComparer.Ordinal);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var eqIndex = line.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = line.Substring(0, eqIndex);
                var value = line.Substring(eqIndex + 1);
                data[key] = value;
            }
        }

        return data;
    }
}
