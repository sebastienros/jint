using System.Buffers;
using Jint.Native.Temporal;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-availablecalendars — the calendar identifiers this engine answers for.
/// </summary>
/// <remarks>
/// <para>
/// ECMA-402 has one such list, not three, and defines it as the calendars "for which the implementation
/// provides the functionality of <c>Intl.DateTimeFormat</c> objects". Three places consume it, and each used
/// to carry its own copy: <c>Intl.supportedValuesOf('calendar')</c>
/// (https://tc39.es/ecma402/#sec-intl.supportedvaluesof step 2), the <c>calendar</c> option of
/// <see cref="DateTimeFormatConstructor"/>, and — through
/// https://tc39.es/proposal-temporal/#sec-temporal-canonicalizecalendar, whose first step is this very
/// operation — every <c>Temporal</c> entry point. The three agreed on the sixteen for a default engine and
/// on nothing else: they had different alias tables, and only one of them could be extended.
/// </para>
/// <para>
/// Extending it is <see cref="ICalendarProvider.GetSupportedCalendars"/>, which after #3405 is what makes a
/// non-ISO calendar exist. So a host adds a calendar once, and it is a calendar everywhere — including in
/// <c>Intl.supportedValuesOf</c> and as an <c>Intl.DateTimeFormat</c> <c>calendar</c> option, neither of
/// which could see it before. The sixteen are named here rather than asked of the provider, so an
/// unconfigured engine answers from a lookup and never constructs one.
/// </para>
/// <para>
/// Two identifiers are accepted and never available. <c>islamic</c> and <c>islamic-rgsa</c> name
/// observation-based calendars whose data Jint does not have:
/// https://tc39.es/ecma402/#sec-createdatetimeformat step 9 requires a formatter asking for one to resolve to
/// some other available calendar instead — which is what test262's
/// <c>DateTimeFormat/constructor-options-calendar-islamic-fallback.js</c> asserts, with a list of exactly
/// these sixteen — and <c>Temporal</c> refuses them outright in <c>RejectTemporalUnsupportedCalendar</c>.
/// That is the one place the two services differ, and it is about data rather than about membership: a host
/// provider that claims either has the observation tables, and then it is available like any other.
/// </para>
/// </remarks>
internal static class AvailableCalendars
{
    /// <summary>
    /// The identifiers a default engine answers for, in lexicographic code-unit order — the order
    /// <c>Intl.supportedValuesOf</c> reports and the order this array is read in.
    /// </summary>
    private static readonly string[] Builtin =
    [
        "buddhist", "chinese", "coptic", "dangi", "ethioaa", "ethiopic",
        "gregory", "hebrew", "indian", "islamic-civil", "islamic-tbla",
        "islamic-umalqura", "iso8601", "japanese", "persian", "roc"
    ];

    private static readonly StringSearchValues BuiltinLookup = new(Builtin, StringComparison.Ordinal);

    /// <summary>
    /// A legacy spelling of <c>gregory</c> that predates the Unicode <c>type</c> grammar and cannot appear in
    /// a <c>-u-ca-</c> extension, being nine characters where the grammar allows eight. It is here because
    /// <c>Temporal</c> has always accepted it as a <c>calendar</c> field value.
    /// </summary>
    private const string LegacyGregorian = "gregorian";

    /// <summary>The two deprecated identifiers of the remark above: legal to ask for, never resolved to.</summary>
    private static bool IsDeprecated(string canonical)
        => string.Equals(canonical, "islamic", StringComparison.Ordinal)
        || string.Equals(canonical, "islamic-rgsa", StringComparison.Ordinal);

    /// <summary>
    /// Whether <paramref name="canonical"/> — already through <see cref="Canonicalize"/> — is one of the
    /// calendars this engine can convert dates for.
    /// </summary>
    internal static bool Contains(Engine? engine, string canonical)
    {
        if (BuiltinLookup.Contains(canonical))
        {
            return true;
        }

        // Only a host provider can answer for anything else, and the shared singleton is skipped by identity
        // because the sixteen above are everything it claims. It is asked for a well-formed Unicode calendar
        // type only, so nothing it claims can produce an identifier that would not round-trip through a
        // [u-ca=…] annotation.
        var provider = engine?.Options.Temporal.CalendarProvider;
        return provider is not null
            && !ReferenceEquals(provider, DefaultCalendarProvider.Instance)
            && IntlUtilities.IsValidUnicodeExtensionValue(canonical)
            && provider.IsSupported(canonical);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-canonicalizeuvalue applied to a calendar identifier, then checked against
    /// the list. Returns null for an identifier this engine does not answer for, which is the caller's cue to
    /// throw a <c>RangeError</c> (<c>Temporal</c>) or to fall back to the locale's calendar (<c>Intl</c>).
    /// </summary>
    /// <remarks>
    /// The two deprecated identifiers come back as themselves rather than as null: they are legal option and
    /// annotation values, and each consumer decides what to do with one.
    /// </remarks>
    internal static string? Canonicalize(Engine? engine, string identifier)
    {
        var canonical = IntlUtilities.CanonicalizeUValue("ca", identifier);

        if (string.Equals(canonical, LegacyGregorian, StringComparison.Ordinal))
        {
            return "gregory";
        }

        return Contains(engine, canonical) || IsDeprecated(canonical) ? canonical : null;
    }

    /// <summary>
    /// The calendar an <c>Intl.DateTimeFormat</c> resolves to for a requested identifier, or null when the
    /// request is not one this engine answers for and the caller should fall back to the locale's own
    /// calendar. https://tc39.es/ecma402/#sec-createdatetimeformat step 9 is the deprecated-identifier
    /// substitution; the tabular civil calendar is the closest thing Jint has to either of them.
    /// </summary>
    internal static string? ResolveForDateTimeFormat(Engine engine, string identifier)
    {
        var canonical = Canonicalize(engine, identifier);
        if (canonical is null)
        {
            return null;
        }

        return IsDeprecated(canonical) && !Contains(engine, canonical) ? "islamic-civil" : canonical;
    }

    /// <summary>
    /// What <c>Intl.supportedValuesOf('calendar')</c> reports: the available list, with the deprecated
    /// identifiers left out because they are never a resolved calendar. The caller sorts.
    /// </summary>
    internal static string[] SupportedValues(Engine engine)
    {
        var provider = engine.Options.Temporal.CalendarProvider;
        if (ReferenceEquals(provider, DefaultCalendarProvider.Instance))
        {
            return (string[]) Builtin.Clone();
        }

        var result = new List<string>(Builtin.Length + 4);
        result.AddRange(Builtin);

        foreach (var calendar in provider.GetSupportedCalendars())
        {
            if (!IsDeprecated(calendar) && !result.Contains(calendar))
            {
                result.Add(calendar);
            }
        }

        return result.ToArray();
    }
}
