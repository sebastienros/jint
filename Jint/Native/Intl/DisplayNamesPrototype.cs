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
        // Language codes: 2-3 letter codes (en, eng) or BCP 47 tags (en-US)
        if (code.Length < 2)
        {
            return false;
        }

        // Simple validation - starts with letters
        return char.IsLetter(code[0]) && char.IsLetter(code[1]);
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
        // Calendar codes: alphanumeric with hyphens
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        foreach (var c in code)
        {
            if (!char.IsLetterOrDigit(c) && c != '-')
            {
                return false;
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
        result.CreateDataPropertyOrThrow("type", displayNames.DisplayType);
        result.CreateDataPropertyOrThrow("style", displayNames.Style);
        result.CreateDataPropertyOrThrow("fallback", displayNames.Fallback);

        if (displayNames.LanguageDisplay != null)
        {
            result.CreateDataPropertyOrThrow("languageDisplay", displayNames.LanguageDisplay);
        }

        return result;
    }
}
