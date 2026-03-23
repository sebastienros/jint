using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-displaynames-prototype-object
/// </summary>
internal sealed class DisplayNamesPrototype : Prototype
{
    private readonly DisplayNamesConstructor _constructor;

    public DisplayNamesPrototype(Engine engine,
        Realm realm,
        DisplayNamesConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;

        var properties = new PropertyDictionary(3, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["of"] = new PropertyDescriptor(new ClrFunction(Engine, "of", Of, 1, LengthFlags), PropertyFlags),
            ["resolvedOptions"] = new PropertyDescriptor(new ClrFunction(Engine, "resolvedOptions", ResolvedOptions, 0, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.DisplayNames", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsDisplayNames ValidateDisplayNames(JsValue thisObject)
    {
        if (thisObject is JsDisplayNames displayNames)
        {
            return displayNames;
        }

        Throw.TypeError(_realm, "Value is not an Intl.DisplayNames");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.displaynames.prototype.of
    /// </summary>
    private JsValue Of(JsValue thisObject, JsCallArguments arguments)
    {
        var displayNames = ValidateDisplayNames(thisObject);
        var code = arguments.At(0);

        // code argument is required
        if (code.IsUndefined())
        {
            Throw.TypeError(_realm, "Code argument is required");
        }

        var codeString = TypeConverter.ToString(code);

        // Validate code based on type
        if (!ValidateCode(displayNames.DisplayType, codeString))
        {
            Throw.RangeError(_realm, $"Invalid code '{codeString}' for type '{displayNames.DisplayType}'");
        }

        var result = displayNames.Of(codeString);

        return result != null ? new JsString(result) : JsValue.Undefined;
    }

    private static bool ValidateCode(string type, string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        return type switch
        {
            "language" => IsValidLanguageCode(code),
            "region" => IsValidRegionCode(code),
            "script" => IsValidScriptCode(code),
            "currency" => IsValidCurrencyCode(code),
            "calendar" => IsValidCalendarCode(code),
            "dateTimeField" => IsValidDateTimeFieldCode(code),
            _ => false
        };
    }

    private static bool IsValidLanguageCode(string code)
    {
        // Language codes must be valid unicode_language_id per ECMA-402
        // unicode_language_id = unicode_language_subtag (("-" unicode_script_subtag)? ("-" unicode_region_subtag)? | ("-" unicode_region_subtag)) ("-" unicode_variant_subtag)*
        // https://tc39.es/ecma402/#sec-isstructurallyvalidlanguagetag

        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        // Reject CLDR-specific syntax not valid in BCP 47
        if (string.Equals(code, "root", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Reject underscores (BCP 47 uses hyphens only)
        if (code.Contains('_'))
        {
            return false;
        }

        // Cannot start or end with separator
        if (code[0] == '-' || code[code.Length - 1] == '-')
        {
            return false;
        }

        // Check for empty subtags (consecutive hyphens)
        if (code.Contains("--"))
        {
            return false;
        }

        // Must contain only valid characters
        foreach (var c in code)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-'))
            {
                return false;
            }
        }

        var parts = code.Split('-');
        if (parts.Length == 0)
        {
            return false;
        }

        var firstPart = parts[0];

        // First part must be language subtag: 2-3 or 5-8 letters (4 letters is script, not valid as first)
        if (firstPart.Length == 1 || firstPart.Length == 4 || firstPart.Length > 8)
        {
            return false;
        }

        // First part must start with letter and be all letters
        foreach (var c in firstPart)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        // No extensions or private use allowed for unicode_language_id
        var hasScript = false;
        var hasRegion = false;
        var seenVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];

            if (part.Length == 0)
            {
                return false; // Empty subtag
            }

            // Check for singleton (extension marker) - not allowed
            if (part.Length == 1)
            {
                return false;
            }

            // 2-character subtag starting with letter: region
            if (part.Length == 2 && char.IsLetter(part[0]))
            {
                if (hasRegion || seenVariants.Count > 0)
                {
                    return false; // Duplicate region or region after variant
                }
                if (!char.IsLetter(part[1]))
                {
                    return false;
                }
                hasRegion = true;
            }
            // 3-digit subtag: UN M.49 region code
            else if (part.Length == 3 && char.IsDigit(part[0]) && char.IsDigit(part[1]) && char.IsDigit(part[2]))
            {
                if (hasRegion || seenVariants.Count > 0)
                {
                    return false;
                }
                hasRegion = true;
            }
            // 3-character subtag starting with digit: invalid for region (must be all digits)
            else if (part.Length == 3 && char.IsDigit(part[0]))
            {
                // Must be all digits for region, otherwise invalid
                if (!char.IsDigit(part[1]) || !char.IsDigit(part[2]))
                {
                    return false;
                }
            }
            // 4-character subtag all letters: script
            else if (part.Length == 4)
            {
                var allLetters = true;
                foreach (var c in part)
                {
                    if (!char.IsLetter(c))
                    {
                        allLetters = false;
                        break;
                    }
                }

                if (allLetters)
                {
                    if (hasScript || hasRegion || seenVariants.Count > 0)
                    {
                        return false; // Duplicate script or script after region/variant
                    }
                    hasScript = true;
                }
                else if (char.IsDigit(part[0]))
                {
                    // 4-char variant starting with digit
                    if (seenVariants.Contains(part))
                    {
                        return false; // Duplicate variant
                    }
                    seenVariants.Add(part);
                }
                else
                {
                    return false; // Invalid 4-char subtag
                }
            }
            // 5-8 character subtag: variant
            else if (part.Length >= 5 && part.Length <= 8)
            {
                // Variant subtags are alphanumeric
                foreach (var c in part)
                {
                    if (!char.IsLetterOrDigit(c))
                    {
                        return false;
                    }
                }
                if (seenVariants.Contains(part))
                {
                    return false; // Duplicate variant
                }
                seenVariants.Add(part);
            }
            else
            {
                return false; // Invalid subtag length
            }
        }

        return true;
    }

    private static bool IsValidRegionCode(string code)
    {
        // Region codes: 2 letter codes (US) or 3 digit codes (001)
        if (code.Length == 2)
        {
            return char.IsLetter(code[0]) && char.IsLetter(code[1]);
        }

        if (code.Length == 3)
        {
            return char.IsDigit(code[0]) && char.IsDigit(code[1]) && char.IsDigit(code[2]);
        }

        return false;
    }

    private static bool IsValidScriptCode(string code)
    {
        // Script codes: 4 letter codes (Latn, Cyrl)
        if (code.Length != 4)
        {
            return false;
        }

        foreach (var c in code)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidCurrencyCode(string code)
    {
        // Currency codes: 3 letter codes (USD, EUR)
        if (code.Length != 3)
        {
            return false;
        }

        foreach (var c in code)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidCalendarCode(string code)
    {
        // Calendar codes must follow Unicode calendar identifier format:
        // unicode_calendar_identifier = unicode_alpha (3*8alphanum) *("-" (3*8alphanum))
        // Each segment must be 3-8 alphanumeric characters (ASCII only)
        // Segments are separated by hyphens (not underscores)
        // https://unicode.org/reports/tr35/#Unicode_calendar_identifier

        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        // Cannot start or end with separator
        if (code[0] == '-' || code[0] == '_' || code[code.Length - 1] == '-' || code[code.Length - 1] == '_')
        {
            return false;
        }

        // Cannot contain spaces or underscores
        foreach (var c in code)
        {
            if (char.IsWhiteSpace(c) || c == '_')
            {
                return false;
            }
        }

        var segments = code.Split('-');

        foreach (var segment in segments)
        {
            // Each segment must be 3-8 characters
            if (segment.Length < 3 || segment.Length > 8)
            {
                return false;
            }

            // Each segment must be ASCII alphanumeric only
            foreach (var c in segment)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsValidDateTimeFieldCode(string code)
    {
        // DateTime field codes
        return code switch
        {
            "era" or "year" or "quarter" or "month" or "weekOfYear" or
            "weekday" or "day" or "dayPeriod" or "hour" or "minute" or
            "second" or "timeZoneName" => true,
            _ => false
        };
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.displaynames.prototype.resolvedoptions
    /// </summary>
    private JsObject ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
    {
        var displayNames = ValidateDisplayNames(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        result.CreateDataPropertyOrThrow("locale", displayNames.Locale);
        result.CreateDataPropertyOrThrow("style", displayNames.Style);
        result.CreateDataPropertyOrThrow("type", displayNames.DisplayType);
        result.CreateDataPropertyOrThrow("fallback", displayNames.Fallback);

        if (displayNames.LanguageDisplay != null)
        {
            result.CreateDataPropertyOrThrow("languageDisplay", displayNames.LanguageDisplay);
        }

        return result;
    }
}
