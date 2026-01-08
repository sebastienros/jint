using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-relativetimeformat-constructor
/// </summary>
internal sealed class RelativeTimeFormatConstructor : Constructor
{
    private static readonly JsString _functionName = new("RelativeTimeFormat");
    private static readonly string[] LocaleMatcherValues = ["lookup", "best fit"];
    private static readonly string[] StyleValues = ["long", "short", "narrow"];
    private static readonly string[] NumericValues = ["always", "auto"];

    public RelativeTimeFormatConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new RelativeTimeFormatPrototype(engine, realm, this, objectPrototype);
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

    public RelativeTimeFormatPrototype PrototypeObject { get; }

    /// <summary>
    /// Called when Intl.RelativeTimeFormat is invoked without `new`.
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, this);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.relativetimeformat
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Get options object
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);

        // Validate localeMatcher option
        GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

        // Resolve locale
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolvedLocale = ResolveRelativeTimeFormatLocale(_engine, availableLocales, requestedLocales, optionsObj);

        // Get style option
        var style = GetStringOption(optionsObj, "style", StyleValues, "long");

        // Get numeric option
        var numeric = GetStringOption(optionsObj, "numeric", NumericValues, "always");

        // Get CultureInfo for the locale
        var culture = IntlUtilities.GetCultureInfo(resolvedLocale) ?? CultureInfo.InvariantCulture;

        return new JsRelativeTimeFormat(
            _engine,
            PrototypeObject,
            resolvedLocale,
            style,
            numeric,
            culture);
    }

    private string GetStringOption(ObjectInstance options, string property, string[]? values, string fallback)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        if (values != null && values.Length > 0)
        {
            var found = false;
            foreach (var allowed in values)
            {
                if (string.Equals(stringValue, allowed, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option '{property}'");
            }
        }

        return stringValue;
    }

    private static string ResolveRelativeTimeFormatLocale(Engine engine, HashSet<string> availableLocales, List<string> requestedLocales, ObjectInstance options)
    {
        var resolved = IntlUtilities.ResolveLocale(engine, availableLocales, requestedLocales, options, []);
        return resolved.Locale;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.relativetimeformat.supportedlocalesof
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
