namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR locale data for canonicalization.
/// Data is initialized inline in LocaleData.Data.cs.
/// </summary>
internal static partial class LocaleData
{
    /// <summary>
    /// Grandfathered tag mappings (e.g., "art-lojban" → "jbo").
    /// </summary>
    public static Dictionary<string, string> TagMappings => _tagMappings;

    /// <summary>
    /// Simple language code mappings (e.g., "cmn" → "zh", "iw" → "he").
    /// </summary>
    public static Dictionary<string, string> LanguageMappings => _languageMappings;

    /// <summary>
    /// Complex language mappings that may include script/region.
    /// </summary>
    public static Dictionary<string, ComplexLanguageMapping> ComplexLanguageMappings => _complexLanguageMappings;

    /// <summary>
    /// Region code mappings (e.g., "DD" → "DE", numeric to alpha).
    /// </summary>
    public static Dictionary<string, string> RegionMappings => _regionMappings;

    /// <summary>
    /// Variant subtag mappings.
    /// </summary>
    public static Dictionary<string, VariantMapping> VariantMappings => _variantMappings;

    /// <summary>
    /// Language + variant mappings for grandfathered variants.
    /// Key format: "language+variant" (e.g., "art+lojban"), Value: new language (e.g., "jbo").
    /// </summary>
    public static Dictionary<string, string> LanguageVariantMappings => _languageVariantMappings;

    /// <summary>
    /// Script + region mappings for deprecated regions (like SU).
    /// Key format: "Script+Region" (e.g., "Armn+SU"), Value: new region (e.g., "AM").
    /// </summary>
    public static Dictionary<string, string> ScriptRegionMappings => _scriptRegionMappings;

    /// <summary>
    /// Unicode extension value mappings, keyed by extension key (e.g., "ca", "tz").
    /// </summary>
    public static Dictionary<string, Dictionary<string, string>> UnicodeMappings => _unicodeMappings;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    internal readonly record struct ComplexLanguageMapping(string Language, string? Script, string? Region);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    internal readonly record struct VariantMapping(string Type, string Replacement, string? Prefix = null);
}
