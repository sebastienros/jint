namespace Jint.Native.Intl.Data;

/// <summary>
/// Implements CLDR Add Likely Subtags and Remove Likely Subtags algorithms.
/// https://www.unicode.org/reports/tr35/#Likely_Subtags
/// </summary>
internal static class LikelySubtags
{
    /// <summary>
    /// Add Likely Subtags algorithm - maximizes a locale by adding likely script and region.
    /// </summary>
    public static string AddLikelySubtags(string locale)
    {
        var baseName = ExtractBaseName(locale);
        var extension = locale.Length > baseName.Length ? locale.Substring(baseName.Length) : string.Empty;
        ParseBaseName(baseName, out var language, out var script, out var region, out var variants);
        var variantSuffix = BuildVariantSuffix(variants);

        // If already has all parts and language is not "und", return as-is
        if (!RequiresReplacement(language) && !string.IsNullOrEmpty(script) && !string.IsNullOrEmpty(region))
        {
            return baseName + extension;
        }

        if (!TryResolve(language, script, region, out var resolved))
        {
            return baseName + extension;
        }

        var maximizedBase = resolved + variantSuffix;
        return maximizedBase + extension;
    }

    /// <summary>
    /// Remove Likely Subtags algorithm - minimizes a locale by removing default script and region.
    /// </summary>
    public static string RemoveLikelySubtags(string locale)
    {
        var baseName = ExtractBaseName(locale);
        var extension = locale.Length > baseName.Length ? locale.Substring(baseName.Length) : string.Empty;
        ParseBaseName(baseName, out var language, out var script, out var region, out var variants);
        var variantSuffix = BuildVariantSuffix(variants);

        // Build the base tag without variants for maximization comparison
        var baseTagWithoutVariants = BuildBaseTag(language, script, region);

        // First maximize the locale (without variants for comparison)
        var maximized = AddLikelySubtags(baseTagWithoutVariants);
        var maximizedBase = ExtractBaseName(maximized);
        ParseBaseName(maximizedBase, out var languageMax, out var scriptMax, out var regionMax, out _);

        // Build the maximized base without variants for comparison
        var maximizedBaseTag = BuildBaseTag(languageMax, scriptMax, regionMax);

        // Try removing parts and check if re-maximizing gives the same result
        foreach (var trial in EnumerateTrials(languageMax, scriptMax, regionMax))
        {
            var trialMaximized = AddLikelySubtags(trial);
            var trialBase = ExtractBaseName(trialMaximized);
            // Parse to get just the base tag (no variants from trial)
            ParseBaseName(trialBase, out var trialLang, out var trialScript, out var trialRegion, out _);
            var trialBaseTag = BuildBaseTag(trialLang, trialScript, trialRegion);

            if (string.Equals(trialBaseTag, maximizedBaseTag, StringComparison.Ordinal))
            {
                return trial + variantSuffix + extension;
            }
        }

        return maximizedBaseTag + variantSuffix + extension;
    }

    private static IEnumerable<string> EnumerateTrials(string? language, string? script, string? region)
    {
        // Try language only
        if (!string.IsNullOrEmpty(language))
        {
            yield return BuildBaseTag(language, null, null);
        }

        // Try language + region (without script)
        if (!string.IsNullOrEmpty(language) && !string.IsNullOrEmpty(region))
        {
            yield return BuildBaseTag(language, null, region);
        }

        // Try language + script (without region)
        if (!string.IsNullOrEmpty(language) && !string.IsNullOrEmpty(script))
        {
            yield return BuildBaseTag(language, script, null);
        }
    }

    private static bool TryResolve(string? language, string? script, string? region, out string resolved)
    {
        var lookupLanguage = NormalizeLanguage(language);
        string? match = null;

        // Try lookup keys in order of specificity
        foreach (var key in EnumerateLookupKeys(lookupLanguage, script, region))
        {
            if (key is not null && LikelySubtagsData.TryResolve(key, out var candidate))
            {
                match = candidate;
                break;
            }
        }

        if (match is null)
        {
            resolved = string.Empty;
            return false;
        }

        ParseBaseName(match, out var matchLanguage, out var matchScript, out var matchRegion, out _);
        var resolvedLanguage = RequiresReplacement(language) ? matchLanguage : lookupLanguage;
        var resolvedScript = string.IsNullOrEmpty(script) ? matchScript : script;
        var resolvedRegion = string.IsNullOrEmpty(region) ? matchRegion : region;

        resolved = BuildBaseTag(resolvedLanguage, resolvedScript, resolvedRegion);
        return true;
    }

    private static IEnumerable<string?> EnumerateLookupKeys(string? language, string? script, string? region)
    {
        // Most specific to least specific
        yield return BuildBaseTag(language, script, region);

        if (!string.IsNullOrEmpty(script))
        {
            yield return BuildBaseTag(language, script, null);
        }

        if (!string.IsNullOrEmpty(region))
        {
            yield return BuildBaseTag(language, null, region);
        }

        yield return BuildBaseTag(language, null, null);

        // Also try "und" variants for script/region lookup
        if (!string.Equals(language, "und", StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(script) && !string.IsNullOrEmpty(region))
            {
                yield return BuildBaseTag("und", script, region);
            }

            if (!string.IsNullOrEmpty(script))
            {
                yield return BuildBaseTag("und", script, null);
            }

            if (!string.IsNullOrEmpty(region))
            {
                yield return BuildBaseTag("und", null, region);
            }
        }
    }

    private static bool RequiresReplacement(string? language)
    {
        return string.IsNullOrEmpty(language) || string.Equals(language, "und", StringComparison.Ordinal);
    }

    private static string NormalizeLanguage(string? language)
    {
        return RequiresReplacement(language) ? "und" : language!;
    }

    /// <summary>
    /// Extracts the base name from a locale (everything before extensions).
    /// </summary>
    internal static string ExtractBaseName(string locale)
    {
        // Find the start of extensions (-u-, -t-, -x-, etc.)
        var parts = locale.Split('-');
        var baseEndIndex = 0;
        var currentPos = 0;

        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 1 && i > 0)
            {
                // This is an extension singleton
                break;
            }
            baseEndIndex = currentPos + parts[i].Length;
            currentPos = baseEndIndex + 1; // +1 for the hyphen
        }

        return locale.Substring(0, baseEndIndex);
    }

    /// <summary>
    /// Parses the base name into language, script, region, and variants.
    /// </summary>
    internal static void ParseBaseName(string baseName, out string? language, out string? script, out string? region, out List<string> variants)
    {
        language = null;
        script = null;
        region = null;
        variants = new List<string>();

        var parts = baseName.Split('-');
        if (parts.Length == 0)
        {
            return;
        }

        var index = 0;

        // Language (2-3 or 4-8 letters)
        if (index < parts.Length && parts[index].Length >= 2 && parts[index].Length <= 8 && IsAllLetters(parts[index]))
        {
            language = parts[index].ToLowerInvariant();
            index++;
        }

        // Script (4 letters)
        if (index < parts.Length && parts[index].Length == 4 && IsAllLetters(parts[index]))
        {
            script = char.ToUpperInvariant(parts[index][0]) + parts[index].Substring(1).ToLowerInvariant();
            index++;
        }

        // Region (2 letters or 3 digits)
        if (index < parts.Length &&
            ((parts[index].Length == 2 && IsAllLetters(parts[index])) ||
             (parts[index].Length == 3 && IsAllDigits(parts[index]))))
        {
            region = parts[index].ToUpperInvariant();
            index++;
        }

        // Variants (5-8 alphanum or 4 chars starting with digit)
        while (index < parts.Length && IsVariantSubtag(parts[index]))
        {
            variants.Add(parts[index].ToLowerInvariant());
            index++;
        }
    }

    /// <summary>
    /// Builds a locale tag from components.
    /// </summary>
    internal static string BuildBaseTag(string? language, string? script, string? region)
    {
        if (string.IsNullOrEmpty(language))
        {
            return string.Empty;
        }

        var result = language!;

        if (!string.IsNullOrEmpty(script))
        {
            result += "-" + script;
        }

        if (!string.IsNullOrEmpty(region))
        {
            result += "-" + region;
        }

        return result;
    }

    private static string BuildVariantSuffix(List<string> variants)
    {
        if (variants.Count == 0)
        {
            return string.Empty;
        }

        return "-" + string.Join("-", variants);
    }

    private static bool IsAllLetters(string s)
    {
        foreach (var c in s)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsAllDigits(string s)
    {
        foreach (var c in s)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsVariantSubtag(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length == 1)
        {
            return false;
        }

        // 4 characters starting with digit
        if (s.Length == 4 && s[0] >= '0' && s[0] <= '9')
        {
            return IsAllAlphanumeric(s);
        }

        // 5-8 alphanumeric characters
        if (s.Length >= 5 && s.Length <= 8)
        {
            return IsAllAlphanumeric(s);
        }

        return false;
    }

    private static bool IsAllAlphanumeric(string s)
    {
        foreach (var c in s)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
            {
                return false;
            }
        }
        return true;
    }
}
