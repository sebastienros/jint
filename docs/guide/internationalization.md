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
use. A CLDR provider can replace the week data (`GetWeekInfo`) and the calendar `Intl.DateTimeFormat` defaults
to (`GetDefaultCalendar`). It cannot yet replace what `Intl.Locale.prototype.getHourCycles` and `getCalendars`
report. So a provider that answers `GetDefaultCalendar` differently from CLDR can see `getCalendars()[0]`
disagree with the formatter's default calendar.

Choose providers before engine construction; the `Options` instance is frozen when an engine consumes it.
