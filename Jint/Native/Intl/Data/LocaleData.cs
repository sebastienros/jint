namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR locale data for canonicalization.
/// Data is loaded from embedded LocaleData.txt resource.
/// </summary>
internal static class LocaleData
{
    private static readonly object _lock = new object();
    private static Dictionary<string, string>? _tagMappings;
    private static Dictionary<string, string>? _languageMappings;
    private static Dictionary<string, ComplexLanguageMapping>? _complexLanguageMappings;
    private static Dictionary<string, string>? _regionMappings;
    private static Dictionary<string, VariantMapping>? _variantMappings;
    private static Dictionary<string, string>? _languageVariantMappings;
    private static Dictionary<string, string>? _scriptRegionMappings;
    private static Dictionary<string, Dictionary<string, string>>? _unicodeMappings;
    private static volatile bool _loaded;

    /// <summary>
    /// Grandfathered tag mappings (e.g., "art-lojban" → "jbo").
    /// </summary>
    public static Dictionary<string, string> TagMappings
    {
        get
        {
            EnsureLoaded();
            return _tagMappings!;
        }
    }

    /// <summary>
    /// Simple language code mappings (e.g., "cmn" → "zh", "iw" → "he").
    /// </summary>
    public static Dictionary<string, string> LanguageMappings
    {
        get
        {
            EnsureLoaded();
            return _languageMappings!;
        }
    }

    /// <summary>
    /// Complex language mappings that may include script/region.
    /// </summary>
    public static Dictionary<string, ComplexLanguageMapping> ComplexLanguageMappings
    {
        get
        {
            EnsureLoaded();
            return _complexLanguageMappings!;
        }
    }

    /// <summary>
    /// Region code mappings (e.g., "DD" → "DE", numeric to alpha).
    /// </summary>
    public static Dictionary<string, string> RegionMappings
    {
        get
        {
            EnsureLoaded();
            return _regionMappings!;
        }
    }

    /// <summary>
    /// Variant subtag mappings.
    /// </summary>
    public static Dictionary<string, VariantMapping> VariantMappings
    {
        get
        {
            EnsureLoaded();
            return _variantMappings!;
        }
    }

    /// <summary>
    /// Language + variant mappings for grandfathered variants.
    /// Key format: "language+variant" (e.g., "art+lojban"), Value: new language (e.g., "jbo").
    /// </summary>
    public static Dictionary<string, string> LanguageVariantMappings
    {
        get
        {
            EnsureLoaded();
            return _languageVariantMappings!;
        }
    }

    /// <summary>
    /// Script + region mappings for deprecated regions (like SU).
    /// Key format: "Script+Region" (e.g., "Armn+SU"), Value: new region (e.g., "AM").
    /// </summary>
    public static Dictionary<string, string> ScriptRegionMappings
    {
        get
        {
            EnsureLoaded();
            return _scriptRegionMappings!;
        }
    }

    /// <summary>
    /// Unicode extension value mappings, keyed by extension key (e.g., "ca", "tz").
    /// </summary>
    public static Dictionary<string, Dictionary<string, string>> UnicodeMappings
    {
        get
        {
            EnsureLoaded();
            return _unicodeMappings!;
        }
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        lock (_lock)
        {
            if (_loaded)
            {
                return;
            }

            _tagMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _languageMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _complexLanguageMappings = new Dictionary<string, ComplexLanguageMapping>(StringComparer.OrdinalIgnoreCase);
            _regionMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _variantMappings = new Dictionary<string, VariantMapping>(StringComparer.OrdinalIgnoreCase);
            _languageVariantMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _scriptRegionMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _unicodeMappings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            var assembly = typeof(LocaleData).Assembly;
            using var stream = assembly.GetManifestResourceStream("Jint.Native.Intl.Data.LocaleData.txt");
            if (stream is null)
            {
                _loaded = true;
                return;
            }

            using var reader = new StreamReader(stream);
            string? currentSection = null;

            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                var eqIndex = line.IndexOf('=');
                if (eqIndex < 0)
                {
                    continue;
                }

                var key = line.Substring(0, eqIndex);
                var value = line.Substring(eqIndex + 1);

                switch (currentSection)
                {
                    case "TAG_MAPPINGS":
                        _tagMappings[key] = value;
                        break;

                    case "LANGUAGE_MAPPINGS":
                        _languageMappings[key] = value;
                        break;

                    case "COMPLEX_LANGUAGE_MAPPINGS":
                        ParseComplexLanguageMapping(key, value);
                        break;

                    case "REGION_MAPPINGS":
                        _regionMappings[key] = value;
                        break;

                    case "VARIANT_MAPPINGS":
                        ParseVariantMapping(key, value);
                        break;

                    case "LANGUAGE_VARIANT_MAPPINGS":
                        _languageVariantMappings![key] = value;
                        break;

                    case "SCRIPT_REGION_MAPPINGS":
                        _scriptRegionMappings![key] = value;
                        break;

                    case "UNICODE_MAPPINGS":
                        ParseUnicodeMapping(key, value);
                        break;
                }
            }

            _loaded = true;
        }
    }

    private static void ParseComplexLanguageMapping(string key, string value)
    {
        // Format: language,script:Latn,region:ME
        var parts = value.Split(',');
        string? script = null;
        string? region = null;

        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.StartsWith("script:", StringComparison.Ordinal))
            {
                script = part.Substring(7);
            }
            else if (part.StartsWith("region:", StringComparison.Ordinal))
            {
                region = part.Substring(7);
            }
        }

        _complexLanguageMappings![key] = new ComplexLanguageMapping(parts[0], script, region);
    }

    private static void ParseVariantMapping(string key, string value)
    {
        // Format: type,replacement
        var commaIndex = value.IndexOf(',');
        if (commaIndex > 0)
        {
            _variantMappings![key] = new VariantMapping(
                value.Substring(0, commaIndex),
                value.Substring(commaIndex + 1)
            );
        }
    }

    private static void ParseUnicodeMapping(string key, string value)
    {
        // Format key: keyType:oldValue (e.g., "ca:islamicc")
        var colonIndex = key.IndexOf(':');
        if (colonIndex > 0)
        {
            var keyType = key.Substring(0, colonIndex);
            var oldValue = key.Substring(colonIndex + 1);

            if (!_unicodeMappings!.TryGetValue(keyType, out var typeDict))
            {
                typeDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _unicodeMappings[keyType] = typeDict;
            }

            typeDict[oldValue] = value;
        }
    }

    internal readonly struct ComplexLanguageMapping
    {
        public ComplexLanguageMapping(string language, string? script, string? region)
        {
            Language = language;
            Script = script;
            Region = region;
        }

        public string Language { get; }
        public string? Script { get; }
        public string? Region { get; }
    }

    internal readonly struct VariantMapping
    {
        public VariantMapping(string type, string replacement)
        {
            Type = type;
            Replacement = replacement;
        }

        public string Type { get; }
        public string Replacement { get; }
    }
}
