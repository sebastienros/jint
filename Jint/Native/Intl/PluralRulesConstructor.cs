using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-pluralrules-constructor
/// </summary>
internal sealed class PluralRulesConstructor : Constructor
{
    private static readonly JsString _functionName = new("PluralRules");
    private static readonly HashSet<string> LocaleMatcherValues = ["lookup", "best fit"];
    private static readonly HashSet<string> TypeValues = ["cardinal", "ordinal"];
    private static readonly HashSet<string> NotationValues = ["standard", "scientific", "engineering", "compact"];
    private static readonly HashSet<string> RoundingModeValues = ["ceil", "floor", "expand", "trunc", "halfCeil", "halfFloor", "halfExpand", "halfTrunc", "halfEven"];
    private static readonly HashSet<string> RoundingPriorityValues = ["auto", "morePrecision", "lessPrecision"];
    private static readonly HashSet<string> TrailingZeroDisplayValues = ["auto", "stripIfInteger"];

    public PluralRulesConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PluralRulesPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            ["supportedLocalesOf"] = new(new ClrFunction(Engine, "supportedLocalesOf", SupportedLocalesOf, 1, PropertyFlag.Configurable), PropertyFlags)
        };
        SetProperties(properties);
    }

    public PluralRulesPrototype PrototypeObject { get; }

    /// <summary>
    /// Called when Intl.PluralRules is invoked without `new`.
    /// PluralRules must throw TypeError when called without new.
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Intl.PluralRules must be called with 'new'");
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.pluralrules
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Get options object
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);

        // Note: localeMatcher is read by ResolveLocale, so we don't read it here

        // Resolve locale
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolvedLocale = ResolvePluralRulesLocale(_engine, availableLocales, requestedLocales, optionsObj);

        // Get type option
        var type = GetStringOption(optionsObj, "type", TypeValues, "cardinal");

        // Read notation option
        var notation = GetStringOption(optionsObj, "notation", NotationValues, "standard");

        // Digit options (in spec order)
        var minimumIntegerDigits = GetNumberOption(optionsObj, "minimumIntegerDigits", 1, 21, 1);
        var minimumFractionDigits = GetNumberOption(optionsObj, "minimumFractionDigits", 0, 20, 0);
        var maximumFractionDigits = GetNumberOption(optionsObj, "maximumFractionDigits", minimumFractionDigits, 20, System.Math.Max(minimumFractionDigits, 3));

        // Read significant digits options (for spec compliance)
        _ = optionsObj.Get("minimumSignificantDigits");
        _ = optionsObj.Get("maximumSignificantDigits");

        // Rounding options (in spec order)
        var roundingIncrement = GetNumberOption(optionsObj, "roundingIncrement", 1, 5000, 1);
        var roundingMode = GetStringOption(optionsObj, "roundingMode", RoundingModeValues, "halfExpand");
        var roundingPriority = GetStringOption(optionsObj, "roundingPriority", RoundingPriorityValues, "auto");
        var trailingZeroDisplay = GetStringOption(optionsObj, "trailingZeroDisplay", TrailingZeroDisplayValues, "auto");

        // Get CultureInfo for the locale
        var culture = IntlUtilities.GetCultureInfo(resolvedLocale) ?? CultureInfo.InvariantCulture;

        // Get prototype from newTarget (for cross-realm construction)
        var proto = GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.PluralRules.PrototypeObject);

        return new JsPluralRules(
            _engine,
            proto,
            resolvedLocale,
            type,
            notation,
            minimumIntegerDigits,
            minimumFractionDigits,
            maximumFractionDigits,
            roundingMode,
            roundingPriority,
            roundingIncrement,
            trailingZeroDisplay,
            culture);
    }

    private string GetStringOption(ObjectInstance options, string property, HashSet<string> values, string fallback)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        if (values != null && values.Count > 0)
        {
            if (!values.Contains(stringValue))
            {
                Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option '{property}'");
            }
        }

        return stringValue;
    }

    private int GetNumberOption(ObjectInstance options, string property, int minimum, int maximum, int fallback)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            return fallback;
        }

        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number))
        {
            Throw.RangeError(_realm, $"Invalid number for option '{property}'");
        }

        var intValue = (int) System.Math.Floor(number);
        if (intValue < minimum || intValue > maximum)
        {
            Throw.RangeError(_realm, $"Value {intValue} for option '{property}' is out of range [{minimum}, {maximum}]");
        }

        return intValue;
    }

    private static string ResolvePluralRulesLocale(Engine engine, HashSet<string> availableLocales, List<string> requestedLocales, ObjectInstance options)
    {
        var resolved = IntlUtilities.ResolveLocale(engine, availableLocales, requestedLocales, options, []);
        return resolved.Locale;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.pluralrules.supportedlocalesof
    /// </summary>
    private JsArray SupportedLocalesOf(JsValue thisObject, JsCallArguments arguments)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();

        // Validate localeMatcher option
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);
        GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

        List<JsValue> supported = [];
        foreach (var locale in requestedLocales)
        {
            var bestAvailable = IntlUtilities.BestAvailableLocale(availableLocales, locale);
            if (bestAvailable != null)
            {
                supported.Add(locale);
            }
        }

        return new JsArray(_engine, supported.ToArray());
    }
}
