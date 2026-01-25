using System.Buffers;
using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-displaynames-constructor
/// </summary>
internal sealed class DisplayNamesConstructor : Constructor
{
    private static readonly JsString _functionName = new("DisplayNames");
    private static readonly StringSearchValues LocaleMatcherValues = new(["lookup", "best fit"], StringComparison.Ordinal);
    private static readonly StringSearchValues TypeValues = new(["language", "region", "script", "currency", "calendar", "dateTimeField"], StringComparison.Ordinal);
    private static readonly StringSearchValues StyleValues = new(["long", "short", "narrow"], StringComparison.Ordinal);
    private static readonly StringSearchValues FallbackValues = new(["code", "none"], StringComparison.Ordinal);
    private static readonly StringSearchValues LanguageDisplayValues = new(["dialect", "standard"], StringComparison.Ordinal);

    public DisplayNamesConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new DisplayNamesPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(2, PropertyFlag.Configurable);
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

    public DisplayNamesPrototype PrototypeObject { get; }

    /// <summary>
    /// Called when Intl.DisplayNames is invoked without `new`.
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Constructor Intl.DisplayNames requires 'new'");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.displaynames
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Step 2: Get prototype from newTarget FIRST (per spec order)
        var proto = GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.DisplayNames.PrototypeObject);

        // Step 3: Resolve locales
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);

        // Step 4: options is required (GetOptionsObject throws for undefined)
        if (options.IsUndefined())
        {
            Throw.TypeError(_realm, "Options argument is required");
        }

        // Get options object
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);

        // Per spec: Get options in the correct order
        // Step 8: localeMatcher
        var localeMatcher = GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

        // Step 11: style (comes before type per spec)
        var style = GetStringOption(optionsObj, "style", StyleValues, "long");

        // Step 13: type (required)
        var typeValue = optionsObj.Get("type");
        if (typeValue.IsUndefined())
        {
            Throw.TypeError(_realm, "Required option 'type' is undefined");
        }
        var type = GetStringOption(optionsObj, "type", TypeValues, "");

        // Step 15: fallback
        var fallback = GetStringOption(optionsObj, "fallback", FallbackValues, "code");

        // Step 17: languageDisplay (only valid for type: "language")
        string? languageDisplay = null;
        if (string.Equals(type, "language", StringComparison.Ordinal))
        {
            languageDisplay = GetStringOption(optionsObj, "languageDisplay", LanguageDisplayValues, "dialect");
        }

        // Resolve locale
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolvedLocale = ResolveDisplayNamesLocale(_engine, availableLocales, requestedLocales, localeMatcher);

        // Get CultureInfo for the locale
        var culture = IntlUtilities.GetCultureInfo(resolvedLocale) ?? CultureInfo.InvariantCulture;

        return new JsDisplayNames(
            _engine,
            proto,
            resolvedLocale,
            type,
            style,
            fallback,
            languageDisplay,
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

    private static string ResolveDisplayNamesLocale(Engine engine, HashSet<string> availableLocales, List<string> requestedLocales, string localeMatcher)
    {
        var resolved = IntlUtilities.ResolveLocale(engine, availableLocales, requestedLocales, localeMatcher, []);
        return resolved.Locale;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.displaynames.supportedlocalesof
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
