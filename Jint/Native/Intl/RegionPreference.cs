using System.Runtime.InteropServices;
using Jint.Native.Intl.Data;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-regionpreference - the region whose data a locale-info operation reads, and
/// the <c>-u-rg-</c> region override that is tried ahead of it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Region"/> is the first of the region subtag, the region of a <c>-u-sd-</c> subdivision, the
/// region Add Likely Subtags supplies, and <c>001</c>; it is never null. <see cref="RegionOverride"/> is the
/// region of a <c>-u-rg-</c> keyword, and whether it wins is the caller's decision: each locale-info operation
/// uses it only when its own data covers that region, so it cannot be folded into <see cref="Region"/> here.
/// </para>
/// <para>
/// Both hold a region subtag as <c>GetLocaleRegion</c> reads it off a canonicalized tag - two uppercase
/// letters or three digits.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct RegionPreference(string Region, string? RegionOverride)
{
    /// <summary>CLDR's world region, the answer when nothing in the tag or the likely subtags names one.</summary>
    internal const string World = "001";

    /// <summary>
    /// https://tc39.es/ecma402/#sec-regionpreference for a Unicode canonicalized locale identifier.
    /// </summary>
    internal static RegionPreference Of(string locale)
    {
        // 1. Let region be GetLocaleRegion(locale).
        var region = GetLocaleRegion(locale);

        // 2. If region is undefined, then
        if (region is null)
        {
            // a. Set region to CanonicalUnicodeSubdivision(locale, "sd").
            region = CanonicalUnicodeSubdivision(locale, "sd");

            // b. If region is undefined, then
            if (region is null)
            {
                // i. Let maximal be the result of the Add Likely Subtags algorithm applied to locale. If an
                //    error is signaled, set maximal to locale.
                // ii. Set maximal to CanonicalizeUnicodeLocaleId(maximal).
                //     Skipped: every region the likely-subtags data supplies is already canonical, and the
                //     step cannot move a region the tag itself did not have.
                // iii. Set region to GetLocaleRegion(maximal).
                // iv. If region is undefined, set region to "001".
                region = GetLocaleRegion(LikelySubtags.AddLikelySubtags(locale)) ?? World;
            }
        }

        // 3. Let regionOverride be CanonicalUnicodeSubdivision(locale, "rg").
        // 4. Return { [[Region]]: region, [[RegionOverride]]: regionOverride }.
        return new RegionPreference(region, CanonicalUnicodeSubdivision(locale, "rg"));
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-canonicalunicodesubdivision - the region a <c>-u-rg-</c> or <c>-u-sd-</c>
    /// value names, or null when the key is absent, empty, or not a <c>unicode_subdivision_id</c>.
    /// </summary>
    /// <remarks>
    /// The region is not checked against any list: <c>zzzzzz</c> names region <c>ZZ</c>, and it is the
    /// caller's data that then has nothing for it.
    /// </remarks>
    internal static string? CanonicalUnicodeSubdivision(string locale, string key)
    {
        // 1. Let subdivision be UnicodeExtensionValue(locale, key).
        var subdivision = UnicodeExtension.GetKeywordValue(locale, key);

        // 2. If subdivision is empty, return undefined.
        // 3. If subdivision cannot be matched by the unicode_subdivision_id Unicode locale nonterminal,
        //    return undefined.
        if (string.IsNullOrEmpty(subdivision) || !TryGetSubdivisionRegionLength(subdivision!, out var regionLength))
        {
            return null;
        }

        // 4. Let region be the longest prefix of subdivision matched by the unicode_region_subtag Unicode
        //    locale nonterminal.
        // 5. Let regionLocale be the string-concatenation of "und-" and region.
        // 6. Set regionLocale to CanonicalizeUnicodeLocaleId(regionLocale).
        // 7. Return GetLocaleRegion(regionLocale).
        var regionLocale = IntlUtilities.CanonicalizeUnicodeLocaleId(string.Concat("und-".AsSpan(), subdivision!.AsSpan(0, regionLength)));
        return GetLocaleRegion(regionLocale);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-getlocaleregion - the region subtag of the tag's base name, or null.
    /// </summary>
    /// <remarks>
    /// The base name is every subtag ahead of the first singleton. After its leading language subtag, a
    /// script is four letters and a variant is four characters starting with a digit or five to eight, so the
    /// one subtag of two letters or three digits there is the region.
    /// </remarks>
    internal static string? GetLocaleRegion(string locale)
    {
        var start = locale.IndexOf('-');
        while ((uint) start < (uint) locale.Length)
        {
            start++;
            var end = locale.IndexOf('-', start);
            if (end < 0)
            {
                end = locale.Length;
            }

            var length = end - start;
            if (length == 1)
            {
                // an extension singleton: the base name ends here
                return null;
            }

            if ((length == 2 && char.IsAsciiLetter(locale[start]) && char.IsAsciiLetter(locale[start + 1]))
                || (length == 3 && char.IsAsciiDigit(locale[start]) && char.IsAsciiDigit(locale[start + 1]) && char.IsAsciiDigit(locale[start + 2])))
            {
                return locale.Substring(start, length).ToUpperInvariant();
            }

            start = end;
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="value"/> is a <c>unicode_subdivision_id</c> - a
    /// <c>unicode_region_subtag</c> followed by one to four alphanumerics - and how long its region prefix is.
    /// </summary>
    /// <remarks>
    /// https://unicode.org/reports/tr35/#unicode_subdivision_id. The region is two letters or three digits,
    /// and those two shapes differ in their first character, so the longest region prefix is decided there.
    /// </remarks>
    private static bool TryGetSubdivisionRegionLength(string value, out int regionLength)
    {
        if (value.Length >= 2 && char.IsAsciiLetter(value[0]) && char.IsAsciiLetter(value[1]))
        {
            regionLength = 2;
        }
        else if (value.Length >= 3 && char.IsAsciiDigit(value[0]) && char.IsAsciiDigit(value[1]) && char.IsAsciiDigit(value[2]))
        {
            regionLength = 3;
        }
        else
        {
            regionLength = 0;
            return false;
        }

        var suffixLength = value.Length - regionLength;
        if (suffixLength is < 1 or > 4)
        {
            return false;
        }

        for (var i = regionLength; i < value.Length; i++)
        {
            if (!char.IsAsciiLetterOrDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }
}
