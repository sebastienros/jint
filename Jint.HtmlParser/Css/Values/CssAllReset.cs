using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Values;

internal static class CssAllReset
{
    // CSS Cascading and Inheritance Level 5, § 3.1; Editor's Draft, 2026-09-23.
    internal static bool IsExcluded(string decodedPropertyName)
    {
        ArgumentNullException.ThrowIfNull(decodedPropertyName);
        return decodedPropertyName.StartsWith("--", StringComparison.Ordinal) ||
            decodedPropertyName.Equals("direction", StringComparison.OrdinalIgnoreCase) ||
            decodedPropertyName.Equals("unicode-bidi", StringComparison.OrdinalIgnoreCase);
    }

    internal static string[] Longhands(IReadOnlyDictionary<string, CssPropertyMetadata> completed)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, family) in CssPropertyCatalog.Obligations)
            if (family != "V9") names.Add(CssPropertyEffects.Canonical(name));
        foreach (var name in completed.Keys) names.Add(name);
        foreach (var (name, longhands) in CssPropertyEffects.ShorthandEffects)
        {
            names.Add(name);
            foreach (var longhand in longhands) names.Add(longhand);
        }
        foreach (var name in CssPropertyEffects.ShorthandEffects.Keys) names.Remove(name);
        foreach (var entry in completed.Values)
            if (entry.Longhands.Count != 0) names.Remove(entry.Name);
        names.RemoveWhere(IsExcluded);
        return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }
}
