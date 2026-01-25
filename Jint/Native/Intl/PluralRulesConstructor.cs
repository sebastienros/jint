using System.Buffers;
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
    private static readonly StringSearchValues LocaleMatcherValues = new(["lookup", "best fit"], StringComparison.Ordinal);
    private static readonly StringSearchValues TypeValues = new(["cardinal", "ordinal"], StringComparison.Ordinal);
    private static readonly StringSearchValues NotationValues = new(["standard", "scientific", "engineering", "compact"], StringComparison.Ordinal);
    private static readonly StringSearchValues RoundingModeValues = new(["ceil", "floor", "expand", "trunc", "halfCeil", "halfFloor", "halfExpand", "halfTrunc", "halfEven"], StringComparison.Ordinal);
    private static readonly StringSearchValues RoundingPriorityValues = new(["auto", "morePrecision", "lessPrecision"], StringComparison.Ordinal);
    private static readonly StringSearchValues TrailingZeroDisplayValues = new(["auto", "stripIfInteger"], StringComparison.Ordinal);

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

        // Digit options - must be read in spec order (SetNumberFormatDigitOptions)
        var minimumIntegerDigits = GetNumberOption(optionsObj, "minimumIntegerDigits", 1, 21, 1);

        // Read fraction digits first (per spec order)
        var minFracValue = optionsObj.Get("minimumFractionDigits");
        var maxFracValue = optionsObj.Get("maximumFractionDigits");
        var hasMinFrac = !minFracValue.IsUndefined();
        var hasMaxFrac = !maxFracValue.IsUndefined();

        // Then read significant digits (per spec order)
        var minSigValue = optionsObj.Get("minimumSignificantDigits");
        var maxSigValue = optionsObj.Get("maximumSignificantDigits");
        var hasMinSig = !minSigValue.IsUndefined();
        var hasMaxSig = !maxSigValue.IsUndefined();

        int? minimumFractionDigits = null;
        int? maximumFractionDigits = null;
        int? minimumSignificantDigits = null;
        int? maximumSignificantDigits = null;

        if (hasMinSig || hasMaxSig)
        {
            // Use significant digits - validate them now
            minimumSignificantDigits = hasMinSig ? GetNumberOptionFromValue(minSigValue, "minimumSignificantDigits", 1, 21, 1) : 1;
            maximumSignificantDigits = hasMaxSig ? GetNumberOptionFromValue(maxSigValue, "maximumSignificantDigits", minimumSignificantDigits.Value, 21, 21) : 21;
        }
        else
        {
            // Use fraction digits - validate them now
            var minFrac = hasMinFrac ? GetNumberOptionFromValue(minFracValue, "minimumFractionDigits", 0, 20, 0) : 0;
            minimumFractionDigits = minFrac;
            var maxFracDefault = System.Math.Max(minFrac, 3);
            maximumFractionDigits = hasMaxFrac ? GetNumberOptionFromValue(maxFracValue, "maximumFractionDigits", minFrac, 20, maxFracDefault) : maxFracDefault;
        }

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
            minimumSignificantDigits,
            maximumSignificantDigits,
            roundingMode,
            roundingPriority,
            roundingIncrement,
            trailingZeroDisplay,
            culture);
    }

    private string GetStringOption(ObjectInstance options, string property, in StringSearchValues values, string fallback)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        if (!values.Contains(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option '{property}'");
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

        return GetNumberOptionFromValue(value, property, minimum, maximum, fallback);
    }

    private int GetNumberOptionFromValue(JsValue value, string property, int minimum, int maximum, int fallback)
    {
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
