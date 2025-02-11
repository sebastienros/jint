using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-numberformat-constructor
/// </summary>
internal sealed class NumberFormatConstructor : Constructor
{
    private static readonly JsString _functionName = new("NumberFormat");

    public NumberFormatConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new NumberFormatPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public NumberFormatPrototype PrototypeObject { get; }

    public object LocaleData { get; private set; } = null!;
    public object AvailableLocales { get; private set; } = null!;
    public object RelevantExtensionKeys { get; private set; } = null!;


    protected override void Initialize()
    {
        LocaleData = new object();
        AvailableLocales = new object();
        RelevantExtensionKeys = new object();
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        if (newTarget.IsUndefined())
        {
            newTarget = this;
        }

        var numberFormat = OrdinaryCreateFromConstructor<JsObject, object>(
            newTarget,
            static intrinsics => intrinsics.NumberFormat.PrototypeObject,
            static (engine, _, _) => new JsObject(engine));

        InitializeNumberFormat(numberFormat, locales, options);
        return numberFormat;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-initializenumberformat
    /// </summary>
    private JsObject InitializeNumberFormat(JsObject numberFormat, JsValue locales, JsValue opts)
    {
        var requestedLocales = CanonicalizeLocaleList(locales);
        var options = CoerceOptionsToObject(opts);
        var opt = new JsObject(_engine);

        var matcher = GetOption(options, "localeMatcher", OptionType.String, new JsValue[] { "lookup", "best fit" }, "best fit");
        opt["localeMatcher"] = matcher;

        var numberingSystem = GetOption(options, "numberingSystem", OptionType.String, [], Undefined);
        if (!numberingSystem.IsUndefined())
        {
            // If numberingSystem does not match the Unicode Locale Identifier type nonterminal, throw a RangeError exception.
        }

        opt["nu"] = numberingSystem;
        var localeData = LocaleData;
        var r = ResolveLocale(_engine.Realm.Intrinsics.NumberFormat.AvailableLocales, requestedLocales, opt, _engine.Realm.Intrinsics.NumberFormat.RelevantExtensionKeys, localeData);
        numberFormat["Locale"] = r["locale"];
        numberFormat["DataLocale"] = r["dataLocale"];
        numberFormat["NumberingSystem"] = r["nu"];
        SetNumberFormatUnitOptions(numberFormat, options);

        int mnfdDefault;
        int mxfdDefault;
        var style = numberFormat["Style"];
        if (style == "currency")
        {
            var currency = numberFormat["Currency"];
            var cDigits = CurrencyDigits(currency);
            mnfdDefault = cDigits;
            mxfdDefault = cDigits;
        }
        else
        {
            mnfdDefault = 0;
            mxfdDefault = style == "percent" ? 0 : 3;
        }

        var notation = GetOption(options, "notation", OptionType.String, new JsValue[] { "standard", "scientific", "engineering", "compact" }, "standard");
        numberFormat["Notation"] = notation;
        SetNumberFormatDigitOptions(numberFormat, options, mnfdDefault, mxfdDefault, notation.ToString());

        var compactDisplay = GetOption(options, "compactDisplay", OptionType.String, new JsValue[] { "short", "long" }, "short");
        if (notation == "compact")
        {
            numberFormat["CompactDisplay"] = compactDisplay;
        }

        var useGrouping = GetOption(options, "useGrouping", OptionType.Boolean, System.Array.Empty<JsValue>(), JsBoolean.True);
        numberFormat["UseGrouping"] = useGrouping;
        var signDisplay = GetOption(options, "signDisplay", OptionType.String, new JsValue[] { "auto", "never", "always", "exceptZero" }, "auto");
        numberFormat["SignDisplay"] = signDisplay;

        return numberFormat;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-resolvelocale
    /// </summary>
    private JsObject ResolveLocale(object availableLocales, JsArray requestedLocales, JsObject options, object relevantExtensionKeys, object localeData)
    {
        // TODO
        var result = new JsObject(_engine);
        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-setnfdigitoptions
    /// </summary>
    private static void SetNumberFormatDigitOptions(JsObject numberFormat, ObjectInstance options, int mnfdDefault, int mxfdDefault, string notation)
    {
        // TODO
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-currencydigits
    /// </summary>
    private static int CurrencyDigits(JsValue currency)
    {
        // TODO
        return 2;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-setnumberformatunitoptions
    /// </summary>
    private void SetNumberFormatUnitOptions(JsObject intlObj, JsValue options)
    {
        var style = GetOption(options, "style", OptionType.String, new JsValue[] { "decimal", "percent", "currency", "unit" }, "decimal");
        intlObj["Style"] = style;
        var currency = GetOption(options, "currency", OptionType.String, [], Undefined);
        if (currency.IsUndefined())
        {
            if (style == "currency")
            {
                ExceptionHelper.ThrowTypeError(_realm, "No currency found when style currency requested");
            }
        }
        else if (!IsWellFormedCurrencyCode(currency))
        {
            ExceptionHelper.ThrowRangeError(_realm, "Invalid currency code");
        }

        var currencyDisplay = GetOption(options, "currencyDisplay", OptionType.String, new JsValue[] { "code", "symbol", "narrowSymbol", "name" }, "symbol");
        var currencySign = GetOption(options, "currencySign", OptionType.String, new JsValue[] { "standard", "accounting" }, "standard");
        var unit = GetOption(options, "unit", OptionType.String, [], Undefined);

        if (unit.IsUndefined())
        {
            if (style == "unit")
            {
                ExceptionHelper.ThrowTypeError(_realm, "No unit found when style unit requested");
            }
        }
        else if (!IsWellFormedUnitIdentifier(unit))
        {
            ExceptionHelper.ThrowRangeError(_realm, "Invalid unit");
        }

        var unitDisplay = GetOption(options, "unitDisplay", OptionType.String, new JsValue[] { "short", "narrow", "long" }, "short");
        if (style == "currency")
        {
            intlObj["Currency"] = currency.ToString().ToUpperInvariant();
            intlObj["CurrencyDisplay"] = currencyDisplay;
            intlObj["CurrencySign"] = currencySign;
        }

        if (style == "unit")
        {
            intlObj["Unit"] = unit;
            intlObj["UnitDisplay"] = unitDisplay;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-iswellformedunitidentifier
    /// </summary>
    private static bool IsWellFormedUnitIdentifier(JsValue unitIdentifier)
    {
        var value = unitIdentifier.ToString();
        if (IsSanctionedSingleUnitIdentifier(value))
        {
            return true;
        }

        var i = value.IndexOf("-per-", StringComparison.Ordinal);
        if (i == -1 || value.IndexOf("-per-", i + 1, StringComparison.Ordinal) != -1)
        {
            return false;
        }

        var numerator = value.Substring(0, i);
        var denominator = value.Substring(i + 5);
        if (IsSanctionedSingleUnitIdentifier(numerator) && IsSanctionedSingleUnitIdentifier(denominator))
        {
            return true;
        }

        return false;
    }

    private static readonly HashSet<string> _sanctionedSingleUnitIdentifiers = new(StringComparer.Ordinal)
    {
        "acre",
        "bit",
        "byte",
        "celsius",
        "centimeter",
        "day",
        "degree",
        "fahrenheit",
        "fluid-ounce",
        "foot",
        "gallon",
        "gigabit",
        "gigabyte",
        "gram",
        "hectare",
        "hour",
        "inch",
        "kilobit",
        "kilobyte",
        "kilogram",
        "kilometer",
        "liter",
        "megabit",
        "megabyte",
        "meter",
        "microsecond",
        "mile",
        "mile-scandinavian",
        "milliliter",
        "millimeter",
        "millisecond",
        "minute",
        "month",
        "nanosecond",
        "ounce",
        "percent",
        "petabyte",
        "pound",
        "second",
        "stone",
        "terabit",
        "terabyte",
        "week",
        "yard",
        "year",
    };

    /// <summary>
    /// https://tc39.es/ecma402/#sec-issanctionedsingleunitidentifier
    /// </summary>
    private static bool IsSanctionedSingleUnitIdentifier(string unitIdentifier) => _sanctionedSingleUnitIdentifiers.Contains(unitIdentifier);

    /// <summary>
    /// https://tc39.es/ecma402/#sec-iswellformedcurrencycode
    /// </summary>
    private static bool IsWellFormedCurrencyCode(JsValue currency)
    {
        var value = currency.ToString();
        return value.Length == 3 && char.IsLetter(value[0]) && char.IsLetter(value[1]) && char.IsLetter(value[2]);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-coerceoptionstoobject
    /// </summary>
    private ObjectInstance CoerceOptionsToObject(JsValue options)
    {
        if (options.IsUndefined())
        {
            return OrdinaryObjectCreate(_engine, null);
        }

        return TypeConverter.ToObject(_realm, options);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-canonicalizelocalelist
    /// </summary>
    private JsArray CanonicalizeLocaleList(JsValue locales)
    {
        return new JsArray(_engine);
        // TODO
    }

    private enum OptionType
    {
        Boolean,
        Number,
        String
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-getoption
    /// </summary>
    private JsValue GetOption<T>(JsValue options, string property, OptionType type, T[] values, T defaultValue) where T : JsValue
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            if (defaultValue == "required")
            {
                ExceptionHelper.ThrowRangeError(_realm, "Required value missing");
            }

            return defaultValue;
        }

        switch (type)
        {
            case OptionType.Boolean:
                value = TypeConverter.ToBoolean(value);
                break;
            case OptionType.Number:
                {
                    var number = TypeConverter.ToNumber(value);
                    if (double.IsNaN(number))
                    {
                        ExceptionHelper.ThrowRangeError(_realm, "Invalid number value");
                    }
                    value = number;
                    break;
                }
            case OptionType.String:
                value = TypeConverter.ToString(value);
                break;
            default:
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(type), "Unknown type");
                break;
        }

        if (values.Length > 0 && System.Array.IndexOf(values, value) == -1)
        {
            ExceptionHelper.ThrowRangeError(_realm, "Value not part of list");
        }

        return value;
    }
}
