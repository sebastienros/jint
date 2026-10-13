#nullable enable

using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// The flexible day period — the <c>dayPeriod</c> option, and the <c>B</c> field CLDR writes in some locales' own time
/// patterns — written from CLDR 48.2's day period rules and each locale's names, as ICU writes it
/// (sebastienros/jint#4205). Until then only English had them, and every other locale wrote .NET's am/pm.
/// </summary>
/// <remarks>
/// The expected values are what Node 24.19 (ICU 78.3) writes. ICU picks the period by the hour from the rules of the
/// locale or, failing that, of its truncations (<c>zh-Hant</c> has <c>zh</c>'s); writes <c>noon</c> only where the
/// rules have it and the pattern shows the time as exactly 12:00; never writes <c>midnight</c>; and writes am/pm in the
/// field's width where the locale has no rules or no name for the period.
/// </remarks>
public class IntlDateTimeFormatDayPeriodTests
{
    private const string Base = "timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn'";

    /// <summary>
    /// <c>{ dayPeriod }</c> on its own, at each hour of a day: the pattern has no minute, so 12:00 is noon wherever the
    /// rules have one. English's morning starts at midnight in CLDR 48.2.
    /// </summary>
    [TestCase("en", "short", "in the morning|in the morning|in the morning|in the morning|in the morning|in the morning|in the morning|in the morning|in the morning|in the morning|in the morning|in the morning|noon|in the afternoon|in the afternoon|in the afternoon|in the afternoon|in the afternoon|in the evening|in the evening|in the evening|at night|at night|at night")]
    [TestCase("de", "long", "nachts|nachts|nachts|nachts|nachts|morgens|morgens|morgens|morgens|morgens|vormittags|vormittags|mittags|nachmittags|nachmittags|nachmittags|nachmittags|nachmittags|abends|abends|abends|abends|abends|abends")]
    [TestCase("fr", "long", "du matin|du matin|du matin|du matin|du matin|du matin|du matin|du matin|du matin|du matin|du matin|du matin|midi|de l’après-midi|de l’après-midi|de l’après-midi|de l’après-midi|de l’après-midi|du soir|du soir|du soir|du soir|du soir|du soir")]
    [TestCase("ja", "short", "夜中|夜中|夜中|夜中|朝|朝|朝|朝|朝|朝|朝|朝|正午|昼|昼|昼|夕方|夕方|夕方|夜|夜|夜|夜|夜中")]
    [TestCase("zh", "narrow", "凌晨|凌晨|凌晨|凌晨|凌晨|早上|早上|早上|上午|上午|上午|上午|中午|下午|下午|下午|下午|下午|下午|晚上|晚上|晚上|晚上|晚上")]
    [TestCase("zh-Hant", "short", "凌晨|凌晨|凌晨|凌晨|凌晨|清晨|清晨|清晨|上午|上午|上午|上午|中午|下午|下午|下午|下午|下午|下午|晚上|晚上|晚上|晚上|晚上")]
    [TestCase("zh-TW", "long", "凌晨|凌晨|凌晨|凌晨|凌晨|清晨|清晨|清晨|上午|上午|上午|上午|中午|下午|下午|下午|下午|下午|下午|晚上|晚上|晚上|晚上|晚上")]
    [TestCase("es", "long", "de la madrugada|de la madrugada|de la madrugada|de la madrugada|de la madrugada|de la madrugada|de la mañana|de la mañana|de la mañana|de la mañana|de la mañana|de la mañana|del mediodía|de la tarde|de la tarde|de la tarde|de la tarde|de la tarde|de la tarde|de la tarde|de la noche|de la noche|de la noche|de la noche")]
    [TestCase("es-CO", "short", "de la mañana|de la mañana|de la mañana|de la mañana|de la mañana|de la mañana|de la mañana|de la mañana|de la mañana|de la mañana|de la mañana|de la mañana|m.|de la tarde|de la tarde|de la tarde|de la tarde|de la tarde|de la tarde|de la tarde|de la noche|de la noche|de la noche|de la noche")]
    [TestCase("ko", "short", "밤|밤|밤|아침|아침|아침|오전|오전|오전|오전|오전|오전|정오|오후|오후|오후|오후|오후|저녁|저녁|저녁|밤|밤|밤")]
    [TestCase("hi", "long", "रात|रात|रात|रात|सुबह|सुबह|सुबह|सुबह|सुबह|सुबह|सुबह|सुबह|दोपहर|दोपहर|दोपहर|दोपहर|शाम|शाम|शाम|शाम|रात|रात|रात|रात")]
    [TestCase("ru", "short", "ночи|ночи|ночи|ночи|утра|утра|утра|утра|утра|утра|утра|утра|полд.|дня|дня|дня|дня|дня|вечера|вечера|вечера|вечера|ночи|ночи")]
    [TestCase("ar", "long", "في المساء|ليلاً|ليلاً|في الصباح|في الصباح|في الصباح|صباحًا|صباحًا|صباحًا|صباحًا|صباحًا|صباحًا|ظهرًا|بعد الظهر|بعد الظهر|بعد الظهر|بعد الظهر|بعد الظهر|مساءً|مساءً|مساءً|مساءً|مساءً|مساءً")]
    [TestCase("pt", "short", "da madrugada|da madrugada|da madrugada|da madrugada|da madrugada|da madrugada|da manhã|da manhã|da manhã|da manhã|da manhã|da manhã|meio-dia|da tarde|da tarde|da tarde|da tarde|da tarde|da tarde|da noite|da noite|da noite|da noite|da noite")]
    [TestCase("it", "narrow", "di notte|di notte|di notte|di notte|di notte|di notte|di mattina|di mattina|di mattina|di mattina|di mattina|di mattina|mezzogiorno|di pomeriggio|di pomeriggio|di pomeriggio|di pomeriggio|di pomeriggio|di sera|di sera|di sera|di sera|di sera|di sera")]
    [TestCase("ga", "long", "r.n.|r.n.|r.n.|r.n.|r.n.|r.n.|r.n.|r.n.|r.n.|r.n.|r.n.|r.n.|i.n.|i.n.|i.n.|i.n.|i.n.|i.n.|i.n.|i.n.|i.n.|i.n.|i.n.|i.n.")]
    public void TheDayPeriodIsTheLocalesForEachHour(string locale, string width, string expected)
    {
        var engine = new Engine();
        var actual = engine.Evaluate($$"""
            (function () {
                var f = new Intl.DateTimeFormat('{{locale}}', { dayPeriod: '{{width}}', {{Base}} });
                var out = [];
                for (var h = 0; h < 24; h++) {
                    out.push(f.format(Date.UTC(2022, 11, 24, h)));
                }
                return out.join('|');
            })()
            """).AsString();

        actual.Should().Be(expected);
    }

    /// <summary>
    /// <c>zh-Hant</c>'s own time patterns write the flexible day period (<c>Bh:mm</c>); these are the issue's examples.
    /// </summary>
    [TestCase("{ timeStyle: 'short' }", 0, 5, 0, "凌晨12:05", "凌晨")]
    [TestCase("{ timeStyle: 'short' }", 6, 5, 0, "清晨6:05", "清晨")]
    [TestCase("{ timeStyle: 'short' }", 12, 0, 0, "中午12:00", "中午")]
    [TestCase("{ timeStyle: 'short' }", 19, 45, 0, "晚上7:45", "晚上")]
    [TestCase("{ timeStyle: 'medium' }", 23, 59, 59, "晚上11:59:59", "晚上")]
    [TestCase("{ weekday: 'short', hour: 'numeric', minute: '2-digit', hour12: true }", 19, 45, 0, "週六下午7:45", "下午")]
    public void AZhHantTimeWritesTheFlexibleDayPeriod(string options, int hour, int minute, int second, string expected, string dayPeriod)
    {
        var engine = new Engine();
        var formatter = $"new Intl.DateTimeFormat('zh-Hant', Object.assign({{ {Base} }}, {options}))";
        var date = $"Date.UTC(2022, 11, 24, {hour}, {minute}, {second})";

        engine.Evaluate($"{formatter}.format({date})").AsString().Should().Be(expected);
        engine.Evaluate($"{formatter}.formatToParts({date}).filter(function (p) {{ return p.type === 'dayPeriod'; }})[0].value")
            .AsString().Should().Be(dayPeriod);
    }

    /// <summary>
    /// A Temporal value goes through the same pattern.
    /// </summary>
    [Test]
    public void APlainTimeWritesTheFlexibleDayPeriod()
    {
        var engine = new Engine();
        engine.Evaluate("new Temporal.PlainTime(6, 5).toLocaleString('zh-Hant', { timeStyle: 'short' })").AsString().Should().Be("清晨6:05");
    }

    /// <summary>
    /// Noon is written only for a time the pattern shows as exactly 12:00: an hour alone shows 12:30 as noon, an hour
    /// and minute do not, and a second written beside them must be zero too. Midnight is never written.
    /// </summary>
    [TestCase("en", "{ hour: 'numeric', dayPeriod: 'short' }", 12, 30, 0, "12 noon")]
    [TestCase("en", "{ hour: 'numeric', minute: 'numeric', dayPeriod: 'short' }", 12, 30, 0, "12:30 in the afternoon")]
    [TestCase("en", "{ hour: 'numeric', minute: 'numeric', dayPeriod: 'short' }", 12, 0, 0, "12:00 noon")]
    [TestCase("en", "{ hour: 'numeric', minute: 'numeric', second: 'numeric', dayPeriod: 'short' }", 12, 0, 5, "12:00:05 in the afternoon")]
    [TestCase("en", "{ hour: 'numeric', dayPeriod: 'long' }", 0, 0, 0, "12 in the morning")]
    [TestCase("fr", "{ dayPeriod: 'long' }", 0, 0, 0, "du matin")]
    [TestCase("de", "{ hour: 'numeric', dayPeriod: 'long', hour12: true }", 10, 0, 0, "10 Uhr vormittags")]
    [TestCase("ga", "{ hour: 'numeric', dayPeriod: 'narrow', hour12: true }", 15, 0, 0, "3 i.n.")]
    public void NoonIsATimeShownAsExactlyNoon(string locale, string options, int hour, int minute, int second, string expected)
    {
        var engine = new Engine();
        var formatter = $"new Intl.DateTimeFormat('{locale}', Object.assign({{ {Base} }}, {options}))";
        var date = $"Date.UTC(2022, 11, 24, {hour}, {minute}, {second})";

        engine.Evaluate($"{formatter}.format({date})").AsString().Should().Be(expected);
        engine.Evaluate($"{formatter}.formatToParts({date}).map(function (p) {{ return p.value; }}).join('')").AsString().Should().Be(expected);
    }

    /// <summary>
    /// A range writes each date's day period from its own hour, and a shared one from the start: <c>zh-Hant</c>'s
    /// interval patterns write <c>B</c>, and an hour range keeps the start's period, as ICU does.
    /// </summary>
    [TestCase("zh-Hant", "{ hour: 'numeric', dayPeriod: 'short' }", 5, 5, 9, 30, "清晨5–9時", "dayPeriod:shared:清晨|hour:startRange:5|literal:shared:–|hour:endRange:9|literal:shared:時")]
    [TestCase("zh-Hant-MY", "{ hour: 'numeric', minute: '2-digit' }", 6, 5, 7, 30, "清晨6:05至7:30", "dayPeriod:shared:清晨|hour:startRange:6|literal:startRange::|minute:startRange:05|literal:shared:至|hour:endRange:7|literal:endRange::|minute:endRange:30")]
    [TestCase("zh-Hant-MY", "{ hour: 'numeric', minute: '2-digit' }", 6, 5, 19, 30, "清晨6:05至晚上7:30", "dayPeriod:startRange:清晨|hour:startRange:6|literal:startRange::|minute:startRange:05|literal:shared:至|dayPeriod:endRange:晚上|hour:endRange:7|literal:endRange::|minute:endRange:30")]
    public void ARangeWritesEachDatesDayPeriod(string locale, string options, int startHour, int startMinute, int endHour, int endMinute, string expected, string parts)
    {
        var engine = new Engine();
        var formatter = $"new Intl.DateTimeFormat('{locale}', Object.assign({{ {Base} }}, {options}))";
        var dates = $"Date.UTC(2022, 11, 24, {startHour}, {startMinute}), Date.UTC(2022, 11, 24, {endHour}, {endMinute})";

        engine.Evaluate($"{formatter}.formatRange({dates})").AsString().Should().Be(expected);
        engine.Evaluate($"{formatter}.formatRangeToParts({dates}).map(function (p) {{ return p.type + ':' + p.source + ':' + p.value; }}).join('|')")
            .AsString().Should().Be(parts);
    }

    /// <summary>
    /// <see cref="ICldrProvider"/> has no flexible day periods, so a host's am/pm reaches a <c>dayPeriod</c> only where
    /// the locale writes am/pm in its place — Irish, which has no day period rules — and not where it has a period.
    /// </summary>
    [Test]
    public void AHostsAmPmReachesTheDayPeriodOnlyWhereTheLocaleWritesAmPm()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new MornAndEve());
        engine.Evaluate($"new Intl.DateTimeFormat('ga', {{ dayPeriod: 'long', {Base} }}).format(Date.UTC(2022, 11, 24, 15))").AsString().Should().Be("EVE");
        engine.Evaluate($"new Intl.DateTimeFormat('de', {{ dayPeriod: 'long', {Base} }}).format(Date.UTC(2022, 11, 24, 15))").AsString().Should().Be("nachmittags");
        engine.Evaluate($"new Intl.DateTimeFormat('en', {{ hour: 'numeric', minute: 'numeric', {Base} }}).format(Date.UTC(2022, 11, 24, 15))").AsString().Should().Be("3:00 EVE");
    }

    private sealed class MornAndEve : DefaultCldrProvider
    {
        public override string[]? GetDayPeriods(string locale, string style, string? calendar) => ["MORN", "EVE"];
    }
}
