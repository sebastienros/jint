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

Jint keeps the package small with English-oriented CLDR defaults and BCL-backed timezone/calendar data. Hosts
that need broader locale data or full historical IANA timezone behavior can replace:

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
use. A CLDR provider can replace each of them, one member per `Intl.Locale` method:

| Member | Read by | Asked about |
| --- | --- | --- |
| `GetWeekInfo` | `getWeekInfo` | the whole tag |
| `GetHourCycles` | `getHourCycles`, and `Intl.DateTimeFormat`'s default hour cycle and the ones `hour12` picks | the whole tag; the locale `Intl.DateTimeFormat` matched |
| `GetCalendars` | `getCalendars` | the whole tag |
| `GetDefaultCalendar` | `Intl.DateTimeFormat`'s default calendar | the locale `Intl.DateTimeFormat` matched |

"The whole tag" keeps its `-u-rg-` and `-u-sd-` keywords, which can move the region, so the provider picks the
region itself; the locale a formatter matched carries no Unicode extension. A provider that returns `null`
leaves the answer to the embedded table, read for the region the specification picks, and `DefaultCldrProvider`
answers every member from those tables. A `-u-fw-`, `-u-hc-` or `-u-ca-` keyword, or the matching option, still
wins over the provider, and what a provider answers is held to what the algorithm allows: hour cycles are kept
only if they are `h11`, `h12`, `h23` or `h24`, calendars only if the engine can format in them.

One hour-cycle member means `Intl.DateTimeFormat`'s default is always `getHourCycles()[0]` of the same locale.
The calendar is two members, because the specification keys the formatter's default by the matched locale
alone while `getCalendars` honours `-u-rg-` and `-u-sd-`. So a provider overriding only `GetDefaultCalendar`
can see `getCalendars()[0]` disagree with the formatter's default calendar; one that wants them to agree
overrides both:

```csharp
sealed class HebrewByDefault : DefaultCldrProvider
{
    public override string? GetDefaultCalendar(string locale) => "hebrew";

    public override string[]? GetCalendars(string locale) => ["hebrew", "gregory"];
}
```

Choose providers before engine construction; the `Options` instance is frozen when an engine consumes it.
