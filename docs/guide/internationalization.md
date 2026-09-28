# Internationalization

Set the engine culture and time zone when JavaScript locale operations must not inherit the machine defaults:

```csharp
var engine = new Engine(options =>
{
    options.Culture = CultureInfo.GetCultureInfo("fr-FR");
    options.TimeZone = TimeZoneInfo.Utc;
});

var text = engine.Evaluate("(1234.5).toLocaleString()").AsString();
```

`Culture` affects locale-sensitive formatting. `TimeZone` affects date operations that use the engine's default
zone. Prefer explicit values in deterministic services and tests.

## `Intl` and `Temporal` data

Jint embeds the CLDR tables below and falls back to English-oriented defaults and BCL-backed timezone/calendar data
for the rest. Hosts that need broader locale data or full historical IANA timezone behavior can replace:

- `Options.Intl.CldrProvider` with an `ICldrProvider`
- `Options.Temporal.TimeZoneProvider` with an `ITimeZoneProvider`
- `Options.Temporal.CalendarProvider` with an `ICalendarProvider`

```csharp
var engine = new Engine(options =>
{
    options.Intl.CldrProvider = myCldrProvider;
    options.Temporal.TimeZoneProvider = myTimeZoneProvider;
    options.Temporal.CalendarProvider = myCalendarProvider;
});
```

The calendar provider controls non-ISO arithmetic and the set of recognized calendar identifiers. The CLDR
provider supplies localized names and patterns for those calendars.

The per-region tables are embedded, from CLDR 48.2: week data, the hour cycles in use, and the calendars in
use. A CLDR provider can replace the week data (`GetWeekInfo`) and the calendar `Intl.DateTimeFormat` defaults
to (`GetDefaultCalendar`). It cannot yet replace the hour cycles — what `Intl.Locale.prototype.getHourCycles`
reports and the one `Intl.DateTimeFormat` defaults to, which is always `getHourCycles()[0]` — or what
`getCalendars` reports. So a provider that answers `GetDefaultCalendar` differently from CLDR can see
`getCalendars()[0]` disagree with the formatter's default calendar.

`Intl.DateTimeFormat` resolves a bag of component options — `{ weekday: 'short', day: 'numeric', month: 'long' }`
and the like — through CLDR 48.2's Gregorian patterns, embedded for all 766 locales cldr-json carries, the way ICU
does: the locale's `availableFormats` pattern nearest the request, fields it lacks appended through `appendItems`, a
date and a time joined with `dateTimeFormats` ("at", "um", "à"), and CLDR's month, weekday, era and am/pm names in the
context the pattern asks for. So `de` writes `Sa., 24. Dezember` and `ja` writes `12月24日(土)`, as V8 does, on every
platform, and `resolvedOptions()` reports the fields of the pattern chosen. `formatMatcher: 'basic'` is answered the
same way. The data is one deflated block per language (about 150 KB in the assembly), and only the languages a
process formats in are inflated.

`dateStyle` and `timeStyle` write the locale's CLDR `dateFormats` and `timeFormats` the same way, a date and a time
joined with the same `atTime` connector (`Saturday, December 24, 2022 at 3:07 PM`, `24. Dezember 2022 um 15:07`).
Where the hour cycle the formatter resolved — from `hourCycle`, `hour12`, `-u-hc-` or the region — is not the one the
locale's time pattern is written in, the pattern is matched again for the requested cycle, as V8 does: `en` with
`hourCycle: 'h23'` writes `15:07`, `de` with `hour12: true` writes `03:07 PM`. A Temporal value without all of a
style's fields — a `PlainYearMonth` under a `dateStyle`, a `PlainTime` under a `long` `timeStyle`, which has no zone —
is written with the format the matcher chooses for the fields it has, as Temporal's AdjustDateTimeStyleFormat says.
`resolvedOptions()` reports the styles, not the fields.

The patterns are not replaceable. The names are: a CLDR provider's `GetMonthNames`, `GetWeekdayNames`, `GetEraNames`
or `GetDayPeriods` answer takes the place of CLDR's, in both the format and stand-alone contexts, where it differs
from `DefaultCldrProvider.Instance`'s answer for the same arguments — so a provider that derives from
`DefaultCldrProvider`, or delegates to it, without touching the names leaves CLDR's in place. Time-zone names are not
CLDR's: a `long` zone is written in English in every locale (`Coordinated Universal Time`). `formatRange`'s collapsing
and the Chinese and Dangi calendars still write .NET's patterns and names, and the other non-Gregorian calendars are
written in the Gregorian patterns; those move onto CLDR in later steps of
[#4158](https://github.com/sebastienros/jint/issues/4158).

Choose providers before engine construction; the `Options` instance is frozen when an engine consumes it.
