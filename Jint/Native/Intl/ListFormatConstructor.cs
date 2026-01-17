using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-listformat-constructor
/// </summary>
internal sealed class ListFormatConstructor : Constructor
{
    private static readonly JsString _functionName = new("ListFormat");
    private static readonly HashSet<string> LocaleMatcherValues = ["lookup", "best fit"];
    private static readonly HashSet<string> TypeValues = ["conjunction", "disjunction", "unit"];
    private static readonly HashSet<string> StyleValues = ["long", "short", "narrow"];

    public ListFormatConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ListFormatPrototype(engine, realm, this, objectPrototype);
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

    public ListFormatPrototype PrototypeObject { get; }

    /// <summary>
    /// Called when Intl.ListFormat is invoked without `new`.
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Constructor Intl.ListFormat requires 'new'");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.listformat
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Get options object (strict - throws TypeError for non-object)
        var optionsObj = IntlUtilities.GetOptionsObject(_engine, options);

        // Per ECMA-402 13.1.1: Get options in the correct order
        // Step 8: localeMatcher
        var localeMatcher = GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

        // Step 10: type
        var type = GetStringOption(optionsObj, "type", TypeValues, "conjunction");

        // Step 12: style
        var style = GetStringOption(optionsObj, "style", StyleValues, "long");

        // Resolve locale (don't re-read localeMatcher from options)
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolvedLocale = ResolveListFormatLocale(_engine, availableLocales, requestedLocales, localeMatcher);

        // Get CultureInfo for the locale
        var culture = IntlUtilities.GetCultureInfo(resolvedLocale) ?? CultureInfo.InvariantCulture;

        // Get prototype from newTarget (for cross-realm construction)
        var proto = GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.ListFormat.PrototypeObject);

        return new JsListFormat(
            _engine,
            proto,
            resolvedLocale,
            type,
            style,
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

    private static string ResolveListFormatLocale(Engine engine, HashSet<string> availableLocales, List<string> requestedLocales, string localeMatcher)
    {
        var resolved = IntlUtilities.ResolveLocale(engine, availableLocales, requestedLocales, localeMatcher, []);
        return resolved.Locale;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.listformat.supportedlocalesof
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
