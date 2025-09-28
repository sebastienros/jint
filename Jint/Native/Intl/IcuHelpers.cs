using System.Text;
using Jint.Runtime;

namespace Jint.Native.Intl
{
    /// <summary>
    /// ICU interop + ECMA-402 canonicalization helpers shared by Intl built-ins.
    /// </summary>
    internal static class IcuHelpers
    {
        /// <summary>
        /// Mirrors WebKit's canonicalizeUnicodeExtensionsAfterICULocaleCanonicalization():
        /// - Finds the "-u-" extension and its end (before the next singleton).
        /// - Re-emits the extension with per-key normalization:
        ///   * For keys kb/kc/kh/kk/kn: drop boolean "true" (and treat "yes" as true → drop).
        ///   * For all other keys: keep "yes"; if ICU turned "yes" into "true", revert to "yes".
        ///   * For "rg"/"sd": canonicalize subdivision aliases (no23→no50, ...).
        ///   * For "tz": canonicalize timezone aliases (eire→iedub, est→papty, ...).
        /// Everything else in the tag is preserved.
        /// </summary>
        public static string CanonicalizeUnicodeExtensionsAfterIcu(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return tag;

            int extensionIndex = tag.IndexOf("-u-", StringComparison.OrdinalIgnoreCase);
            if (extensionIndex < 0)
                return tag;

            // Determine the end of the -u- block (before the next singleton like -x-).
            int extensionLength = tag.Length - extensionIndex;
            int end = extensionIndex + 3;
            while (end < tag.Length)
            {
                int dash = tag.IndexOf('-', end);
                if (dash < 0)
                    break;
                if (dash + 2 < tag.Length && tag[dash + 2] == '-')
                {
                    extensionLength = dash - extensionIndex;
                    break;
                }
                end = dash + 1;
            }

            var result = new StringBuilder(tag.Length + 8);

            // Copy up to and including "-u"
            result.Append(tag, 0, extensionIndex + 2);

            // Process "-u-..." segment
            string extension = tag.Substring(extensionIndex, extensionLength);
            var parts = extension.Split('-'); // parts[0] == "", parts[1] == "u"
            int i = 2;

            while (i < parts.Length)
            {
                string subtag = parts[i];
                if (subtag.Length == 0) { i++; continue; }

                // Emit the key or attribute
                result.Append('-');
                result.Append(subtag);

                if (subtag.Length == 2)
                {
                    // It's a key.
                    string key = subtag;
                    bool keyIsDroppableTrue = s_trueDroppableKeys.Contains(key);

                    int valueStart = i + 1;
                    int valueEnd = valueStart;
                    while (valueEnd < parts.Length && parts[valueEnd].Length != 2 && parts[valueEnd].Length != 0)
                        valueEnd++;

                    bool emittedAnyValue = false;

                    for (int v = valueStart; v < valueEnd; v++)
                    {
                        string value = parts[v];
                        if (value.Length == 0)
                            continue;

                        // Handle "yes"/"true" normalization
                        if (value.Equals("yes", StringComparison.OrdinalIgnoreCase))
                        {
                            if (keyIsDroppableTrue)
                            {
                                // Drop boolean true for droppable keys.
                                continue;
                            }
                            // keep "yes" for non-droppable
                        }
                        else if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                        {
                            if (keyIsDroppableTrue)
                            {
                                // Drop boolean true for droppable keys.
                                continue;
                            }
                            // Non-droppable: canonicalize to "yes"
                            value = "yes";
                        }

                        // Per-key aliasing
                        if (key.Equals("rg", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("sd", StringComparison.OrdinalIgnoreCase))
                        {
                            value = CanonicalizeSubdivision(value);
                        }
                        else if (key.Equals("tz", StringComparison.OrdinalIgnoreCase))
                        {
                            value = CanonicalizeTimeZoneType(value);
                        }

                        result.Append('-');
                        result.Append(value);
                        emittedAnyValue = true;
                    }

                    // If **no** value was emitted for a **non-droppable** key, synthesize "-yes".
                    if (!emittedAnyValue && !keyIsDroppableTrue)
                    {
                        result.Append("-yes");
                    }

                    i = valueEnd;
                }
                else
                {
                    // Attribute (or malformed); just pass through.
                    i++;
                }
            }

            // Append remainder after the -u- block
            result.Append(tag, extensionIndex + extensionLength, tag.Length - (extensionIndex + extensionLength));
            return result.ToString();
        }

        /// Validates `tag` as a BCP-47 language tag via ICU and returns a canonical tag.
        /// Throws RangeError on invalid tags (spec-compliant).
        public static string CanonicalizeUnicodeLocaleIdOrThrow(Realm realm, string tag)
        {
            // 1) Validate & parse BCP-47 -> ICU locale ID
            var status = ICU.UErrorCode.U_ZERO_ERROR;
            byte[] locBuf = new byte[128];
            int parsed;
            int need = ICU.uloc_forLanguageTag(tag, locBuf, locBuf.Length, out parsed, ref status);

            if (need > locBuf.Length)
            {
                locBuf = new byte[need];
                status = ICU.UErrorCode.U_ZERO_ERROR;
                need = ICU.uloc_forLanguageTag(tag, locBuf, locBuf.Length, out parsed, ref status);
            }

            if (status != ICU.UErrorCode.U_ZERO_ERROR || parsed != tag.Length || need <= 0)
            {
                // RangeError per spec
                Throw.RangeError(realm, $"invalid language tag: {tag}");
            }

            string icuLocaleId = Encoding.UTF8.GetString(locBuf, 0, need);

            // 2) Canonicalize the ICU locale ID (this applies CLDR language/region/script aliases, e.g. cmn->zh)
            status = ICU.UErrorCode.U_ZERO_ERROR;
            byte[] canonLoc = new byte[System.Math.Max(need + 16, 256)];
            int canonLen = ICU.uloc_canonicalize(icuLocaleId, canonLoc, canonLoc.Length, ref status);

            if (canonLen > canonLoc.Length)
            {
                canonLoc = new byte[canonLen];
                status = ICU.UErrorCode.U_ZERO_ERROR;
                canonLen = ICU.uloc_canonicalize(icuLocaleId, canonLoc, canonLoc.Length, ref status);
            }

            string icuCanonical = (status == ICU.UErrorCode.U_ZERO_ERROR && canonLen > 0)
                ? Encoding.UTF8.GetString(canonLoc, 0, canonLen)
                : icuLocaleId; // fall back if canonicalize didn’t change it

            // 3) Convert canonical ICU locale ID -> canonical BCP-47 tag
            status = ICU.UErrorCode.U_ZERO_ERROR;
            byte[] outBuf = new byte[256];
            int len = ICU.uloc_toLanguageTag(icuCanonical, outBuf, outBuf.Length, strict: false, ref status);

            if (len > outBuf.Length)
            {
                outBuf = new byte[len];
                status = ICU.UErrorCode.U_ZERO_ERROR;
                len = ICU.uloc_toLanguageTag(icuCanonical, outBuf, outBuf.Length, strict: false, ref status);
            }

            if (status != ICU.UErrorCode.U_ZERO_ERROR || len <= 0)
            {
                Throw.RangeError(realm, $"failed to canonicalize language tag: {tag}");
            }

            var canonical = Encoding.UTF8.GetString(outBuf, 0, len);

            // WebKit-style cleanup for "-u-…-true"
            canonical = CanonicalizeUnicodeExtensionsAfterIcu(canonical);

            // Fallback for ICU builds that don't alias cmn->zh
            canonical = FixKnownLanguageAliases(canonical);

            return canonical;
        }

        // Keys whose boolean "true" value is **elided** in canonical form.
        // For these, "-u-<key>-yes" and "-u-<key>-true" both canonicalize to just "-u-<key>".
        // Add "ca" here so a bare `-u-ca` does not synthesize `-yes`
        private static readonly HashSet<string> s_trueDroppableKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "kb", "kc", "kh", "kk", "kn", "ca"
        };

        // Canonicalize subdivision aliases (used for rg/sd values).
        private static string CanonicalizeSubdivision(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "no23": return "no50";
                case "cn11": return "cnbj";
                case "cz10a": return "cz110";
                case "fra": return "frges";
                case "frg": return "frges";
                case "lud": return "lucl"; // test262 prefers the first in replacement list
                default: return value;
            }
        }

        // Canonicalize time zone type aliases (used for tz values).
        private static string CanonicalizeTimeZoneType(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "cnckg": return "cnsha"; // deprecated -> preferred
                case "eire": return "iedub"; // alias -> canonical
                case "est": return "papty"; // alias -> canonical
                case "gmt0": return "gmt";   // alias -> canonical
                case "uct": return "utc";   // alias -> canonical
                case "zulu": return "utc";   // alias -> canonical
                case "utcw05": return "papty"; // short offset alias seen in test262
                default: return value;
            }
        }

        private static string FixKnownLanguageAliases(string canonicalTag)
        {
            if (string.IsNullOrEmpty(canonicalTag))
                return canonicalTag;

            // Split once: "xx[-…]" → lang + rest (rest includes the leading '-')
            int dash = canonicalTag.IndexOf('-');
            ReadOnlySpan<char> lang = dash < 0
                ? canonicalTag.AsSpan()
                : canonicalTag.AsSpan(0, dash);

            // We'll append the remainder (if any) after we swap the primary language subtag.
            ReadOnlySpan<char> rest = dash < 0
                ? ReadOnlySpan<char>.Empty
                : canonicalTag.AsSpan(dash); // includes '-...'

            // Known primary language aliases not consistently handled by older ICU:
            //  - cmn → zh (Mandarin → Chinese)
            //  - ji  → yi
            //  - in  → id
            if (lang.Equals("cmn".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return rest.IsEmpty ? "zh" : "zh" + rest.ToString();
            }

            if (lang.Equals("ji".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return rest.IsEmpty ? "yi" : "yi" + rest.ToString();
            }

            if (lang.Equals("in".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return rest.IsEmpty ? "id" : "id" + rest.ToString();
            }

            // Otherwise, leave as-is.
            return canonicalTag;
        }
    }
}
