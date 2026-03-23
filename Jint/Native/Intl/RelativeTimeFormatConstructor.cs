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
        Throw.TypeError(_realm, "Constructor Intl.RelativeTimeFormat requires 'new'");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.relativetimeformat
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Get options object (lenient - converts to object)
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);

        // Per spec: Get options in the correct order
        // Step 5: localeMatcher
        var localeMatcher = GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

        // Step 7: numberingSystem (read and validate)
        // Per spec, the value must be syntactically valid as a Unicode numbering system identifier
        // If not supported, we fall back to "latn" - we don't throw for valid-but-unsupported values
        var numberingSystemValue = optionsObj.Get("numberingSystem");
        string? numberingSystem = null;
        if (!numberingSystemValue.IsUndefined())
        {
            numberingSystem = TypeConverter.ToString(numberingSystemValue);
            if (string.IsNullOrEmpty(numberingSystem) || !IsWellFormedNumberingSystem(numberingSystem))
            {
                Throw.RangeError(_realm, $"Invalid numberingSystem: {numberingSystem}");
            }
        }

        // Step 16: style
        var style = GetStringOption(optionsObj, "style", StyleValues, "long");

        // Step 18: numeric
        var numeric = GetStringOption(optionsObj, "numeric", NumericValues, "always");

        // Resolve locale (don't re-read localeMatcher from options)
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolvedLocale = ResolveRelativeTimeFormatLocale(_engine, availableLocales, requestedLocales, localeMatcher);

        // Resolve numbering system with proper fallback logic
        string? localeNumberingSystem = null;
        foreach (var loc in requestedLocales)
        {
            localeNumberingSystem = ExtractNumberingSystemFromLocale(loc);
            if (localeNumberingSystem != null)
            {
                break;
            }
        }

        string resolvedNumberingSystem;
        if (numberingSystem != null && IsSupportedNumberingSystem(numberingSystem))
        {
            // Options value is valid and supported - use it
            resolvedNumberingSystem = numberingSystem;
        }
        else if (localeNumberingSystem != null && IsSupportedNumberingSystem(localeNumberingSystem))
        {
            // Fall back to locale extension value
            resolvedNumberingSystem = localeNumberingSystem;
        }
        else
        {
            // Default to "latn"
            resolvedNumberingSystem = "latn";
        }

        // Adjust the resolved locale based on numbering system source
        // Per spec:
        // - If options.numberingSystem overrides locale extension with different value, remove nu from locale
        // - If options.numberingSystem matches locale extension, keep the extension
        // - If locale extension is used (no valid options value), keep the extension
        var finalResolvedLocale = resolvedLocale;
        var numberingSystemFromOptions = numberingSystem != null && IsSupportedNumberingSystem(numberingSystem);

        if (numberingSystemFromOptions)
        {
            // Check if the options value matches the locale extension
            if (localeNumberingSystem != null &&
                string.Equals(numberingSystem, localeNumberingSystem, StringComparison.OrdinalIgnoreCase))
            {
                // Options matches locale extension - keep the extension
                finalResolvedLocale = EnsureNumberingSystemInLocale(resolvedLocale, resolvedNumberingSystem);
            }
            else
            {
                // Options overrode locale extension with different value - remove nu from resolved locale
                finalResolvedLocale = RemoveNumberingSystemFromLocale(resolvedLocale);
            }
        }
        else if (localeNumberingSystem != null && IsSupportedNumberingSystem(localeNumberingSystem))
        {
            // Locale extension is used - ensure it's in the resolved locale
            finalResolvedLocale = EnsureNumberingSystemInLocale(resolvedLocale, resolvedNumberingSystem);
        }
        else
        {
            // Default is used - remove any unsupported nu extension
            finalResolvedLocale = RemoveNumberingSystemFromLocale(resolvedLocale);
        }

        // Get CultureInfo for the locale
        var culture = IntlUtilities.GetCultureInfo(finalResolvedLocale) ?? CultureInfo.InvariantCulture;

        // Get prototype from newTarget (for cross-realm construction)
        var proto = GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.RelativeTimeFormat.PrototypeObject);

        // Per ECMA-402 17.1.1 step 24: Create NumberFormat for number formatting
        var numberFormatConstructor = (NumberFormatConstructor) _realm.Intrinsics.NumberFormat;
        var numberFormat = (JsNumberFormat) numberFormatConstructor.Construct([new JsString(finalResolvedLocale), Undefined], numberFormatConstructor);

        return new JsRelativeTimeFormat(
            _engine,
            proto,
            finalResolvedLocale,
            resolvedNumberingSystem,
            style,
            numeric,
            culture,
            numberFormat);
    }

    private static string? ExtractNumberingSystemFromLocale(string locale)
    {
        // Look for -u-nu-xxx pattern
        const string marker = "-u-";
        var uIndex = locale.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (uIndex == -1) return null;

        // Look for nu- after -u-
        var extensionPart = locale.Substring(uIndex + marker.Length);
        var nuIndex = extensionPart.IndexOf("nu-", StringComparison.OrdinalIgnoreCase);
        if (nuIndex == -1) return null;

        // Extract the value after nu-
        var valueStart = nuIndex + 3;
        var valueEnd = valueStart;
        while (valueEnd < extensionPart.Length && extensionPart[valueEnd] != '-')
        {
            valueEnd++;
        }

        if (valueEnd > valueStart)
        {
            return extensionPart.Substring(valueStart, valueEnd - valueStart).ToLowerInvariant();
        }

        return null;
    }

    private static bool IsWellFormedNumberingSystem(string numberingSystem)
    {
        // Unicode numbering system identifier can be a sequence of subtags separated by '-'
        // Each subtag must be 3-8 alphanumeric characters
        if (string.IsNullOrEmpty(numberingSystem))
        {
            return false;
        }

        var subtags = numberingSystem.Split('-');
        foreach (var subtag in subtags)
        {
            if (subtag.Length < 3 || subtag.Length > 8)
            {
                return false;
            }

            foreach (var c in subtag)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsSupportedNumberingSystem(string numberingSystem)
    {
        // Check if the numbering system is actually supported (has digit mappings)
        return Data.NumberingSystemData.Digits.ContainsKey(numberingSystem);
    }

    private static string RemoveNumberingSystemFromLocale(string locale)
    {
        // Remove the -u-nu-xxx or -nu-xxx part from the locale
        var uIndex = locale.IndexOf("-u-", StringComparison.OrdinalIgnoreCase);
        if (uIndex == -1) return locale;

        var nuIndex = locale.IndexOf("-nu-", StringComparison.OrdinalIgnoreCase);
        if (nuIndex == -1) return locale;

        // Find the end of the nu value
        var valueStart = nuIndex + 4;
        var valueEnd = valueStart;
        while (valueEnd < locale.Length && locale[valueEnd] != '-')
        {
            valueEnd++;
        }

        // Check if there are other extensions after nu
        var hasOtherExtensions = valueEnd < locale.Length;

        // Check if there are other extensions before nu (after -u-)
        var extensionPart = locale.Substring(uIndex + 3);
        var hasExtensionsBefore = !extensionPart.StartsWith("nu-", StringComparison.OrdinalIgnoreCase);

        if (!hasOtherExtensions && !hasExtensionsBefore)
        {
            // nu is the only extension - remove entire -u- section
            return locale.Substring(0, uIndex);
        }

        // Remove just the nu-xxx part
#pragma warning disable CA1845
        return locale.Substring(0, nuIndex) + locale.Substring(valueEnd);
#pragma warning restore CA1845
    }

    private static string EnsureNumberingSystemInLocale(string locale, string numberingSystem)
    {
        // Check if locale already has the numbering system extension
        var uIndex = locale.IndexOf("-u-", StringComparison.OrdinalIgnoreCase);
        if (uIndex == -1)
        {
            // No Unicode extension - add it
            return locale + "-u-nu-" + numberingSystem;
        }

        // Check if nu is already present with the correct value
        var nuPattern = "-nu-" + numberingSystem;
        if (locale.Contains(nuPattern, StringComparison.OrdinalIgnoreCase))
        {
            return locale;
        }

        // Check if nu is present at all
        var nuIndex = locale.IndexOf("-nu-", StringComparison.OrdinalIgnoreCase);
        if (nuIndex == -1)
        {
            // Insert nu after -u-
            return locale.Insert(uIndex + 3, "nu-" + numberingSystem + "-");
        }

        // nu exists with different value - replace it
        var valueStart = nuIndex + 4;
        var valueEnd = valueStart;
        while (valueEnd < locale.Length && locale[valueEnd] != '-')
        {
            valueEnd++;
        }
#pragma warning disable CA1845
        return locale.Substring(0, valueStart) + numberingSystem + locale.Substring(valueEnd);
#pragma warning restore CA1845
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

    private static string ResolveRelativeTimeFormatLocale(Engine engine, HashSet<string> availableLocales, List<string> requestedLocales, string localeMatcher)
    {
        var resolved = IntlUtilities.ResolveLocale(engine, availableLocales, requestedLocales, localeMatcher, []);
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
