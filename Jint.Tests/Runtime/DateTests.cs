using Jint.Native;

namespace Jint.Tests.Runtime;

public class DateTests
{
    private readonly Engine _engine;

    public DateTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));
    }

    [Test]
    public void NaNToString()
    {
        var value = _engine.Evaluate("new Date(NaN).toString();").AsString();
        value.Should().Be("Invalid Date");
    }

    [Test]
    public void NaNToDateString()
    {
        var value = _engine.Evaluate("new Date(NaN).toDateString();").AsString();
        value.Should().Be("Invalid Date");
    }

    [Test]
    public void NaNToTimeString()
    {
        var value = _engine.Evaluate("new Date(NaN).toTimeString();").AsString();
        value.Should().Be("Invalid Date");
    }

    [Test]
    public void NaNToLocaleString()
    {
        var value = _engine.Evaluate("new Date(NaN).toLocaleString();").AsString();
        value.Should().Be("Invalid Date");
    }

    [Test]
    public void NaNToLocaleDateString()
    {
        var value = _engine.Evaluate("new Date(NaN).toLocaleDateString();").AsString();
        value.Should().Be("Invalid Date");
    }

    [Test]
    public void NaNToLocaleTimeString()
    {
        var value = _engine.Evaluate("new Date(NaN).toLocaleTimeString();").AsString();
        value.Should().Be("Invalid Date");
    }

    [Test]
    public void ToJsonFromNaNObject()
    {
        var result = _engine.Evaluate("JSON.stringify({ date: new Date(NaN) });");
        result.ToString().Should().Be("{\"date\":null}");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-date-time-string-format says YYYY is "four decimal digits from 0000
    /// to 9999, or as an expanded year of "+" or "-" followed by six decimal digits", and
    /// https://tc39.es/ecma262/#sec-expanded-years fixes the boundary: a year outside 0000-9999 gets the
    /// expanded form and nothing else does. Only years whose absolute value already runs to six digits
    /// used to come out right, which is why the two rows test262 pins by string - the ±8.64e15 extremes
    /// in built-ins/Date/parse/time-value-maximum-range.js - never caught it.
    /// </summary>
    // Unchanged: six digits either way.
    [TestCase(-8640000000000000, "-271821-04-20T00:00:00.000Z")]
    [TestCase(-3217862419200000, "-100000-01-01T00:00:00.000Z")]
    // Five digits before, six now.
    [TestCase(-377736739200000, "-010000-01-01T00:00:00.000Z")]
    // Four digits before, six now. A negative year is always expanded, however small.
    [TestCase(-62198755200000, "-000001-01-01T00:00:00.000Z")]
    [TestCase(-62167219200001, "-000001-12-31T23:59:59.999Z")]
    // Year 0 is inside 0000-9999, so it keeps the plain four-digit form and takes no sign at all.
    [TestCase(-62167219200000, "0000-01-01T00:00:00.000Z")]
    [TestCase(-62135596800000, "0001-01-01T00:00:00.000Z")]
    [TestCase(0, "1970-01-01T00:00:00.000Z")]
    [TestCase(253402300799999, "9999-12-31T23:59:59.999Z")]
    // One millisecond earlier is the first instant of year 10000, which a separate defect makes throw a
    // CLR exception; this row shows the expanded year without depending on that one.
    [TestCase(253402300800001, "+010000-01-01T00:00:00.001Z")]
    [TestCase(3093527980800000, "+100000-01-01T00:00:00.000Z")]
    [TestCase(8640000000000000, "+275760-09-13T00:00:00.000Z")]
    public void ToIsoStringExpandsYearsOutsideFourDigits(long timeValue, string expected)
    {
        _engine.Evaluate($"new Date({timeValue}).toISOString()").AsString().Should().Be(expected);
    }

    /// <summary>
    /// toJSON has no year formatting of its own - https://tc39.es/ecma262/#sec-date.prototype.tojson
    /// invokes toISOString - so the fix has to reach JSON.stringify too, which is where an embedder
    /// serializing a Date actually meets it.
    /// </summary>
    [Test]
    public void ToJsonExpandsYearsOutsideFourDigits()
    {
        _engine.Evaluate("new Date(-62198755200000).toJSON()").AsString().Should().Be("-000001-01-01T00:00:00.000Z");
        _engine.Evaluate("JSON.stringify(new Date(-62198755200000))").AsString().Should().Be("\"-000001-01-01T00:00:00.000Z\"");
        _engine.Evaluate("JSON.stringify({ d: new Date(253402300800001) })").AsString().Should().Be("{\"d\":\"+010000-01-01T00:00:00.001Z\"}");
    }

    /// <summary>
    /// The practical damage: Jint's own parser implements the format correctly, so the four- and
    /// five-digit strings the formatter used to emit came back as NaN from Date.parse.
    /// </summary>
    [Test]
    public void ToIsoStringOfAnExpandedYearParsesBack()
    {
        _engine.Evaluate("Date.parse(new Date(-62198755200000).toISOString())").AsNumber().Should().Be(-62198755200000);
        _engine.Evaluate("Date.parse(new Date(253402300800001).toISOString())").AsNumber().Should().Be(253402300800001);
    }

    /// <summary>
    /// The neighbouring formatters deliberately do not expand. https://tc39.es/ecma262/#sec-datestring
    /// and https://tc39.es/ecma262/#sec-date.prototype.toutcstring both write a sign followed by
    /// ToZeroPaddedDecimalString(abs(yv), 4), where four is a minimum and not a width, so -0001 and
    /// 10000 are the correct answers there and must stay put.
    /// </summary>
    [Test]
    public void ToUtcStringDoesNotExpandTheYear()
    {
        _engine.Evaluate("new Date(-62198755200000).toUTCString()").AsString().Should().Be("Fri, 01 Jan -0001 00:00:00 GMT");
        _engine.Evaluate("new Date(253402300800001).toUTCString()").AsString().Should().Be("Sat, 01 Jan 10000 00:00:00 GMT");
    }

    [Test]
    public void ValuePrecisionIsIntegral()
    {
        var number = _engine.Evaluate("new Date() / 1").AsNumber();
        number.Should().Be((long) number);

        var dateInstance = _engine.Realm.Intrinsics.Date.Construct(123);
        dateInstance.DateValue.Should().Be((long) dateInstance.DateValue);
    }

    [Test]
    public void ToStringFollowsJavaScriptFormat()
    {
        TimeZoneInfo timeZoneInfo;
        try
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        }
        catch
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }

        var engine = new Engine(options => options.TimeZone = timeZoneInfo);

        engine.Evaluate("new Date(2022,1,1).toString()").Should().Be("Tue Feb 01 2022 00:00:00 GMT+0800 (China Standard Time)");
        engine.Evaluate("new Date(2022,1,1)").ToString().Should().Be("Tue Feb 01 2022 00:00:00 GMT+0800 (China Standard Time)");
    }

    [Test]
    public void ToStringUsesDaylightNameWhenInDst()
    {
        TimeZoneInfo timeZoneInfo;
        try
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }

        var engine = new Engine(options => options.TimeZone = timeZoneInfo);

        // July 4, 2022 is in summer (EDT = UTC-4, daylight saving time)
        engine.Evaluate("new Date(2022, 6, 4).toString()").AsString().Should().Contain("(Eastern Daylight Time)");

        // January 4, 2022 is in winter (EST = UTC-5, standard time)
        engine.Evaluate("new Date(2022, 0, 4).toString()").AsString().Should().Contain("(Eastern Standard Time)");
    }

    [Test]
    public void ToLocaleStringUsesShortTimeZoneAbbreviation()
    {
        TimeZoneInfo timeZoneInfo;
        try
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }

        var engine = new Engine(options => options.TimeZone = timeZoneInfo);

        const string script = """
            (function() {
                var d = new Date(2022, 6, 4); // July 4, 2022 - DST (EDT)
                return d.toLocaleString('en-US', { timeZoneName: 'short' });
            })();
            """;

        // Should return "EDT" abbreviation, not "Eastern Daylight Time"
        var result = engine.Evaluate(script).AsString();
        result.Should().Contain("EDT");

        const string scriptWinter = """
            (function() {
                var d = new Date(2022, 0, 4); // January 4, 2022 - standard time (EST)
                return d.toLocaleString('en-US', { timeZoneName: 'short' });
            })();
            """;

        // Should return "EST" abbreviation, not "Eastern Standard Time"
        var resultWinter = engine.Evaluate(scriptWinter).AsString();
        resultWinter.Should().Contain("EST");
    }

    [TestCase("Thu, 30 Jan 2020 08:00:00 PST", 1580400000000)]
    [TestCase("Thursday January 01 1970 00:00:25 UTC", 25000)]
    [TestCase("Wednesday 31 December 1969 18:01:26 MDT", 86000)]
    [TestCase("Wednesday 31 December 1969 19:00:08 EST", 8000)]
    [TestCase("Wednesday 31 December 1969 17:01:59 PDT", 119000)]
    [TestCase("December 31 1969 17:01:14 MST", 74000)]
    [TestCase("January 01 1970 01:46:06 +0145", 66000)]
    [TestCase("December 31 1969 17:00:50 PDT", 50000)]
    public void CanParseLocaleString(string input, long expected)
    {
        _engine.Evaluate($"new Date('{input}') * 1").AsNumber().Should().Be(expected);
    }

    [TestCase("December 31 1900 12:00:00 +0300", 31)]
    [TestCase("January 1 1969 12:00:00 +0300", 1)]
    [TestCase("December 31 1969 12:00:00 +0300", 31)]
    [TestCase("January 1 1970 12:00:00 +0300", 1)]
    [TestCase("December 31 1970 12:00:00 +0300", 31)]
    public void CanParseDate(string input, int expectedDate)
    {
        TimeZoneInfo timeZoneInfo;
        try
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kiev");
        }
        catch
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
        }
        var engine = new Engine(options => options.TimeZone = timeZoneInfo);
        _engine.Evaluate($"new Date('{input}').getDate()").AsNumber().Should().Be(expectedDate);
    }

    [Test]
    public void CanUseMoment()
    {
        var momentJs = EngineTests.GetEmbeddedFile("moment.js");
        _engine.Execute(momentJs);

        var parsedDate = _engine.Evaluate("moment().format('YYYY')").ToString();
        parsedDate.Should().Be(DateTime.Now.Year.ToString());
    }

    [Test]
    public void CanParseEmptyDate()
    {
        double.IsNaN(_engine.Evaluate("Date.parse('')").AsNumber()).Should().BeTrue();
    }

    [Test]
    public void DateTimeMinValueFlag()
    {
        var date = DateTime.MinValue;
        var jsDate = new JsDate(_engine, date);
        jsDate._dateValue.Flags.Should().Be(DateFlags.DateTimeMinValue);

        date = date.AddMilliseconds(1);
        jsDate = new JsDate(_engine, date);
        jsDate._dateValue.Flags.Should().Be(DateFlags.None);
    }
    
    [Test]
    public void DateTimeMaxValueFlag()
    {
        var date = DateTime.MaxValue;
        var jsDate = new JsDate(_engine, date);
        jsDate._dateValue.Flags.Should().Be(DateFlags.DateTimeMaxValue);

        date = date.AddMilliseconds(-1);
        jsDate = new JsDate(_engine, date);
        jsDate._dateValue.Flags.Should().Be(DateFlags.None);
    }

    /// <summary>
    /// 253402300800000 is the first millisecond of year 10000 — one past the last instant
    /// <see cref="DateTime"/> can represent, and far inside the legal Date range of
    /// https://tc39.es/ecma262/#sec-timeclip, so every one of these must produce a string. The bound
    /// deciding whether a time value could take the DateTime shortcut was rounded up onto this value,
    /// so the shortcut was taken for a value the conversion behind it then threw
    /// ArgumentOutOfRangeException on. That is a CLR exception rather than a JavaScriptException, so it
    /// escaped engine.Evaluate and script try/catch could not see it.
    /// </summary>
    [Test]
    public void FormattingTheFirstInstantOfYear10000DoesNotThrowAClrException()
    {
        var engine = new Engine(options => options.TimeZone = TimeZoneInfo.Utc);

        // How many digits the expanded year gets is a separate defect; what this asserts is that a
        // string comes back at all.
        engine.Evaluate("new Date(253402300800000).toISOString()").AsString().Should().EndWith("10000-01-01T00:00:00.000Z");
        engine.Evaluate("new Date(253402300800000).toJSON()").AsString().Should().EndWith("10000-01-01T00:00:00.000Z");
        engine.Evaluate("JSON.stringify({ d: new Date(253402300800000) })").AsString().Should().EndWith("10000-01-01T00:00:00.000Z\"}");
        engine.Evaluate("new Date(253402300800000).getUTCFullYear()").AsNumber().Should().Be(10000);

        // toUTCString reached the same value through DateTimeOffset.FromUnixTimeMilliseconds, whose own
        // message names 253402300799999 as the largest it accepts. toString renders in the configured
        // local time zone, so use UTC above to keep this boundary instant in year 10000 on every host.
        engine.Evaluate("new Date(253402300800000).toUTCString()").AsString().Should().Be("Sat, 01 Jan 10000 00:00:00 GMT");
        engine.Evaluate("new Date(253402300800000).toString()").AsString().Should().Contain("10000");

        // The escape itself: a CLR exception is invisible to script, so this used to throw out of
        // Evaluate rather than run the catch clause or return a value.
        engine.Evaluate("(function () { try { return new Date(253402300800000).toISOString(); } catch (e) { return 'caught'; } })()")
            .AsString().Should().EndWith("10000-01-01T00:00:00.000Z");
    }

    /// <summary>
    /// The neighbours on both sides always worked, which is what made the failing band exactly one
    /// millisecond wide and easy to miss.
    /// </summary>
    [Test]
    public void ToIsoStringWorksOnBothSidesOfTheDateTimeUpperBound()
    {
        _engine.Evaluate("new Date(253402300799998).toISOString()").AsString().Should().Be("9999-12-31T23:59:59.998Z");
        _engine.Evaluate("new Date(253402300799999).toISOString()").AsString().Should().Be("9999-12-31T23:59:59.999Z");
        _engine.Evaluate("new Date(253402300800001).toISOString()").AsString().Should().EndWith("10000-01-01T00:00:00.001Z");
    }

    /// <summary>
    /// The bound itself. DateTime.MaxValue is 9999-12-31T23:59:59.9999999, so the last millisecond it
    /// can represent is 253402300799999, and rounding that up is what handed a host passing
    /// DateTime.MaxValue a Date sitting in year 10000. The low end never had the problem: the epoch is
    /// a whole number of milliseconds after DateTime.MinValue, so that division comes out exact.
    /// </summary>
    [Test]
    public void DateTimeMaxValueBecomesTheLastMillisecondItCanRepresent()
    {
        var jsDate = new JsDate(_engine, DateTime.MaxValue);
        jsDate.DateValue.Should().Be(253402300799999d);
        jsDate.ToDateTime().Should().Be(DateTime.MaxValue);

        _engine.SetValue("d", jsDate);
        _engine.Evaluate("d.toISOString()").AsString().Should().Be("9999-12-31T23:59:59.999Z");

        new JsDate(_engine, DateTime.MinValue).DateValue.Should().Be(-62135596800000d);
    }

    /// <summary>
    /// Date.parse("9999-12-31T23:59:59.999Z") answered 253402300799998, one millisecond short of the
    /// value toISOString had just been given, so the two did not round-trip. The parser read the epoch
    /// distance off TimeSpan.TotalMilliseconds, which is a double division whose numerator — (double) of
    /// the tick count — stops being exact once the count passes 2^53. The correctly rounded quotient for
    /// that instant is 253402300799998.96875, and the truncating cast then dropped the fraction. It is
    /// the same defect #2965 fixed in JsDate.Max, one conversion further along.
    ///
    /// The affected band begins in 2427, which is where the spacing between doubles first stops dividing
    /// the 10000 ticks in a millisecond; below it every quotient was already exact, so the low rows are
    /// controls that never moved.
    /// </summary>
    [TestCase("9999-12-31T23:59:59.999Z", 253402300799999)]
    [TestCase("9999-12-31T23:59:59.000Z", 253402300799000)]
    [TestCase("2427-01-01T00:00:00.001Z", 14421542400001)]
    [TestCase("2500-06-15T12:30:45.678Z", 16739526645678)]
    // Controls from below the band.
    [TestCase("1970-01-01T00:00:00.000Z", 0)]
    [TestCase("2000-01-01T00:00:00.001Z", 946684800001)]
    [TestCase("2026-08-11T12:34:56.789Z", 1786451696789)]
    public void ParseIsExactToTheMillisecond(string input, long expected)
    {
        _engine.Evaluate($"Date.parse('{input}')").AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// Date.parse("0000-01-01T00:00:00.000Z") answered NaN, and that string is exactly what
    /// Date.prototype.toISOString emits for the instant, so the engine could not read back what it had
    /// just written. https://tc39.es/ecma262/#sec-date-time-string-format admits 0000 as one of the four
    /// digit years, and https://tc39.es/ecma262/#sec-expanded-years spells the same year "+000000"; both
    /// mean year 0, which is 1 BC. <see cref="DateTime"/> has no year 0 at all, and nothing routed the
    /// four-digit spelling to the path that does its own arithmetic.
    ///
    /// NaN was not the worst of it. Falling through to the loose invariant parser, some of these came
    /// back as year 2000 — "0000-01-01" as 946684800000 and "0000-02-29" as 951782400000 — which is a
    /// wrong answer where NaN would at least have been an honest one.
    /// </summary>
    [TestCase("0000-01-01T00:00:00.000Z", -62167219200000)]
    [TestCase("0000-01-01T00:00:00Z", -62167219200000)]
    [TestCase("0000-01-01", -62167219200000)]
    [TestCase("0000-01", -62167219200000)]
    [TestCase("0000", -62167219200000)]
    // Year 0 is a leap year, so the substitute year the parser borrows has to be one too.
    [TestCase("0000-02-29", -62162121600000)]
    [TestCase("0000-03-01", -62162035200000)]
    [TestCase("0000-12-31T23:59:59.999Z", -62135596800001)]
    // An offset in the string still applies, and here it crosses back into the year before.
    [TestCase("0000-01-01T00:00:00.000+02:00", -62167226400000)]
    // The expanded spelling of the same year.
    [TestCase("+000000-01-01T00:00:00.000Z", -62167219200000)]
    [TestCase("+000000-01-01", -62167219200000)]
    [TestCase("+000000", -62167219200000)]
    public void ParseAcceptsYearZeroInEverySpellingTheGrammarAdmits(string input, long expected)
    {
        _engine.Evaluate($"Date.parse('{input}')").AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// The MimeKit fallback in the same method read the same property, so a string only it can parse —
    /// a named US zone, which DateTime.TryParse does not accept — landed a millisecond early too. It
    /// floored where the other site truncated, and the fix keeps that difference.
    /// </summary>
    [TestCase("Sat, 15 Jun 5627 12:30:13 PST", 115418118613000)]
    [TestCase("Sat, 15 Jun 5634 12:30:13 PST", 115639043413000)]
    [TestCase("Thu, 30 Jan 2020 08:00:00 PST", 1580400000000)]
    public void ParseThroughTheMimeKitFallbackIsExactToTheMillisecond(string input, long expected)
    {
        _engine.Evaluate($"Date.parse('{input}')").AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// The pairing that makes it visible from script: a value the engine formatted itself has to come
    /// back unchanged. Every millisecond of the last second of year 9999 goes through, because roughly
    /// one in five of them landed low.
    /// </summary>
    [Test]
    public void EveryMillisecondOfTheLastSecondOfYear9999RoundTripsThroughParse()
    {
        const string Script = """
            (function () {
                var mismatched = 0;
                for (var ms = 253402300799000; ms <= 253402300799999; ms++) {
                    if (Date.parse(new Date(ms).toISOString()) !== ms) { mismatched++; }
                }
                return mismatched;
            })();
            """;

        _engine.Evaluate(Script).AsNumber().Should().Be(0);
    }

    /// Every toLocale* method converted the time value with DatePresentation.ToDateTime() before handing
    /// it to the ECMA-402 formatting path, and that conversion is unguarded.
    /// <see cref="DateTime"/> spans years 1 through 9999; https://tc39.es/ecma262/#sec-timeclip admits
    /// every time value up to 8.64e15, which is years -271821 through 275760. Both ends therefore threw
    /// ArgumentOutOfRangeException straight out of engine.Evaluate — a CLR exception, so script
    /// try/catch never saw it, the same escape #2954 closed for sort and #2965 for the first instant of
    /// year 10000.
    ///
    /// #2981 answered those years with the culture-independent rendering their non-locale siblings give,
    /// because the alternative it wanted — carrying the real fields in on a substitute year congruent
    /// mod 400 — "needs the whole component pipeline to learn about per-field overrides it does not
    /// have". It has them now, so these methods produce a real locale string again: the substitute year
    /// preserves month, day, time and weekday, and the formatter prints the true year over it.
    /// </summary>
    // the maximum time value, 275760-09-13T00:00:00.000Z
    [TestCase(8640000000000000, "9/13/275760, 12:00:00 AM", "9/13/275760", "12:00:00 AM")]
    // one past what DateTime can hold, +010000-01-01T00:00:00.001Z
    [TestCase(253402300800001, "1/1/10000, 12:00:00 AM", "1/1/10000", "12:00:00 AM")]
    // one before what DateTime can hold, 0000-12-31T23:59:59.999Z
    [TestCase(-62135596800001, "12/31/0, 11:59:59 PM", "12/31/0", "11:59:59 PM")]
    // the minimum time value, -271821-04-20T00:00:00.000Z
    [TestCase(-8640000000000000, "4/20/-271821, 12:00:00 AM", "4/20/-271821", "12:00:00 AM")]
    public void ToLocaleStringOutsideDateTimeRangeRendersALocaleString(
        long timeValue, string expectedDateTime, string expectedDate, string expectedTime)
    {
        var engine = new Engine(options => options.TimeZone = TimeZoneInfo.Utc);

        engine.Evaluate($"new Date({timeValue}).toLocaleString('en-US')").AsString().Should().Be(expectedDateTime);
        engine.Evaluate($"new Date({timeValue}).toLocaleDateString('en-US')").AsString().Should().Be(expectedDate);
        engine.Evaluate($"new Date({timeValue}).toLocaleTimeString('en-US')").AsString().Should().Be(expectedTime);
    }

    /// <summary>
    /// The options bag reaches these years now rather than being read, validated and discarded.
    /// </summary>
    [Test]
    public void ToLocaleStringOutsideDateTimeRangeHonoursTheOptionsBag()
    {
        var engine = new Engine(options => options.TimeZone = TimeZoneInfo.Utc);

        engine.Evaluate("new Date(8640000000000000).toLocaleString('en-US', { month: 'long' })")
            .AsString().Should().Be("September");

        engine.Evaluate("new Date(8640000000000000).toLocaleDateString('en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })")
            .AsString().Should().Be("Saturday September 13, 275760");

        // A malformed locale is still a RangeError: the formatter is constructed before anything is
        // rendered, and none of its validation may be skipped.
        engine.Evaluate("(function () { try { return new Date(8640000000000000).toLocaleString('!!bad!!'); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("RangeError");
    }

    /// <summary>
    /// The escape itself. A CLR exception leaves engine.Evaluate without passing through any JavaScript
    /// catch clause, so the only way to show the difference is from inside script.
    /// </summary>
    [Test]
    public void ToLocaleStringOutsideDateTimeRangeDoesNotThrowAClrException()
    {
        var engine = new Engine(options => options.TimeZone = TimeZoneInfo.Utc);

        engine.Evaluate("(function () { try { return new Date(8640000000000000).toLocaleString(); } catch (e) { return 'caught'; } })()")
            .AsString().Should().Contain("275760");

        engine.Evaluate("(function () { try { return new Date(-8640000000000000).toLocaleDateString(); } catch (e) { return 'caught'; } })()")
            .AsString().Should().Contain("-271821");
    }

    /// <summary>
    /// An explicit numeric offset shifts the wall clock past the end of <see cref="DateTime"/> for a time
    /// value that is itself inside it, so the addition applying the offset threw where every other
    /// conversion on the path succeeded. TimeZoneInfo.ConvertTimeFromUtc — the named-zone branch beside
    /// it — saturates rather than throwing, and the offset branch now does the same.
    /// </summary>
    [Test]
    public void ToLocaleStringAtTheDateTimeBoundaryWithAnOffsetTimeZoneDoesNotThrowAClrException()
    {
        var engine = new Engine(options => options.TimeZone = TimeZoneInfo.Utc);

        engine.Evaluate("new Date(253402300799999).toLocaleString('en-US', { timeZone: '+03:00' })").AsString()
            .Should().NotBeEmpty();
        engine.Evaluate("new Date(-62135596800000).toLocaleString('en-US', { timeZone: '-03:00' })").AsString()
            .Should().NotBeEmpty();
    }

    /// <summary>
    /// There is no switch left to land on the wrong side of: the millisecond either side of the end of
    /// <see cref="DateTime"/>'s range is formatted by ECMA-402, and neither looks like what
    /// https://tc39.es/ecma262/#sec-datestring renders. #2981 made the second of these two answer with
    /// the culture-independent string, and that is the 4.16.0 behaviour this changes.
    /// </summary>
    [Test]
    public void ToLocaleStringIsLocaleFormattedOnBothSidesOfTheDateTimeBoundary()
    {
        var engine = new Engine(options => options.TimeZone = TimeZoneInfo.Utc);

        var localeString = engine.Evaluate("new Date(0).toLocaleString('en-US')").AsString();
        localeString.Should().NotBe(engine.Evaluate("new Date(0).toString()").AsString());
        localeString.Should().Contain("1970");

        engine.Evaluate("new Date(253402300799999).toLocaleDateString('en-US')").AsString()
            .Should().Be("12/31/9999");
        engine.Evaluate("new Date(253402300800001).toLocaleDateString('en-US')").AsString()
            .Should().Be("1/1/10000");

        foreach (var timeValue in new[] { "253402300799999", "253402300800001" })
        {
            engine.Evaluate($"new Date({timeValue}).toLocaleDateString('en-US')").AsString()
                .Should().NotBe(engine.Evaluate($"new Date({timeValue}).toDateString()").AsString());
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-expanded-years: "The year 0 is considered positive and must be
    /// prefixed with a + sign. The representation of the year 0 as -000000 is invalid." test262 pins the
    /// three date-time forms in built-ins/Date/parse/year-zero.js; the bare year obeys the same rule, and
    /// a rejected year must not fall through to a parser that would read it as something else.
    /// </summary>
    [TestCase("-000000")]
    [TestCase("-000000-01-01")]
    [TestCase("-000000-01-01T00:00:00.000Z")]
    [TestCase("-000000-03-31T00:45Z")]
    public void ParseRejectsMinusZeroAsAnExpandedYear(string input)
    {
        double.IsNaN(_engine.Evaluate($"Date.parse('{input}')").AsNumber()).Should().BeTrue();
    }

    /// <summary>
    /// The round trip year 0 could not make, and its neighbour one millisecond below year 1.
    /// </summary>
    [Test]
    public void ToIsoStringOfYearZeroParsesBack()
    {
        _engine.Evaluate("Date.parse(new Date(-62167219200000).toISOString())").AsNumber().Should().Be(-62167219200000);
        _engine.Evaluate("Date.parse(new Date(-62135596800001).toISOString())").AsNumber().Should().Be(-62135596800001);
    }

    /// <summary>
    /// Reaching year 0 meant rebuilding the expanded-year path, which was wrong for every year it already
    /// handled whenever the string did not carry an explicit offset. It converted the parse to UTC and
    /// then read the fields back out, so the date-only-means-UTC rule of
    /// https://tc39.es/ecma262/#sec-date-time-string-format was lost, and where the shift crossed
    /// midnight the reconstruction kept the target year with the shifted month and day: "+010000-01-01"
    /// came back as 253433916000000, which is the last day of year 10000 rather than the first. The
    /// offset-bearing rows are the ones that always worked and have to stay put.
    /// </summary>
    [TestCase("+010000-01-01T00:00:00.000Z", 253402300800000)]
    [TestCase("+010000-01-01", 253402300800000)]
    [TestCase("+000001-01-01", -62135596800000)]
    [TestCase("-000001-01-01", -62198755200000)]
    [TestCase("-000001-01-01T00:00:00.000Z", -62198755200000)]
    [TestCase("+002000-02-29", 951782400000)]
    [TestCase("+275760-09-13T00:00:00.000Z", 8640000000000000)]
    [TestCase("-271821-04-20T00:00:00.000Z", -8640000000000000)]
    public void ParseReadsADateOnlyExpandedYearAsUtc(string input, long expected)
    {
        _engine.Evaluate($"Date.parse('{input}')").AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// A four-digit 0000 followed by anything the grammar does not put there stays on the loose parser,
    /// where V8 reads it as year 2000 through its own legacy fallback. Routing on the separator is what
    /// keeps the two engines agreeing about a string neither format defines.
    /// </summary>
    [Test]
    public void ParseLeavesNonGrammarYearZeroFormsOnTheLooseParser()
    {
        _engine.Evaluate("new Date(Date.parse('0000/01/01')).getUTCFullYear()").AsNumber().Should().Be(2000);
    }

    [Test]
    public void DstTransitionShouldUseCorrectOffset()
    {
        TimeZoneInfo nztz;
        try
        {
            nztz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            nztz = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
        }

        var engine = new Engine(options => options.TimeZone = nztz);

        // NZDT (GMT+13) ends at 3:00 AM on the first Sunday of April 2025.
        // At midnight April 6, we are still in NZDT (GMT+13).
        var result1 = engine.Evaluate("new Date(2025, 3, 6, 0, 0, 0).toString()").AsString();
        result1.Should().Contain("GMT+1300");
        result1.Should().Contain("Apr 06 2025 00:00:00");

        // NZDT (GMT+13) begins at 2:00 AM on the last Sunday of September 2025.
        // At midnight Sep 28, we are still in NZST (GMT+12).
        var result2 = engine.Evaluate("new Date(2025, 8, 28, 0, 0, 0).toString()").AsString();
        result2.Should().Contain("GMT+1200");
        result2.Should().Contain("Sep 28 2025 00:00:00");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-date.prototype.settime sets [[DateValue]] to the clipped value v,
    /// and setTime stored the raw argument instead — the one setter in the file that did.
    /// The returned value was always right, so the object and its own return value disagreed. It
    /// surfaces because an infinite argument becomes a DatePresentation flagged Infinity whose Value
    /// is 0, so getTime read the stored infinity back as the epoch rather than as NaN.
    /// </summary>
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    [TestCase("NaN")]
    [TestCase("8.64e15 + 1")]
    [TestCase("-8.64e15 - 1")]
    [TestCase("Number.MAX_VALUE")]
    [TestCase("-Number.MAX_VALUE")]
    public void SetTimeStoresTheClippedTimeValue(string argument)
    {
        _engine.Execute($"var d = new Date(); var returned = d.setTime({argument});");

        double.IsNaN(_engine.Evaluate("returned").AsNumber()).Should().BeTrue("setTime returns the clipped value");
        double.IsNaN(_engine.Evaluate("d.getTime()").AsNumber()).Should().BeTrue("getTime reports the clipped value");
        double.IsNaN(_engine.Evaluate("d.valueOf()").AsNumber()).Should().BeTrue("valueOf reports the clipped value");

        // Numeric coercion of a Date has its own fast path in TypeConverter.ToNumeric, which reads
        // [[DateValue]] directly rather than going through valueOf, so it has to agree as well.
        double.IsNaN(_engine.Evaluate("+d").AsNumber()).Should().BeTrue("unary + reports the clipped value");
    }

    /// <summary>
    /// The extremes https://tc39.es/ecma262/#sec-timeclip admits are legal time values and must survive
    /// the round trip untouched — clipping one millisecond too eagerly would be the opposite defect.
    /// </summary>
    [TestCase("8.64e15", 8640000000000000d)]
    [TestCase("-8.64e15", -8640000000000000d)]
    [TestCase("0", 0d)]
    [TestCase("-1", -1d)]
    public void SetTimeAtTheClipBoundaryRoundTrips(string argument, double expected)
    {
        _engine.Execute($"var d = new Date(); var returned = d.setTime({argument});");

        _engine.Evaluate("returned").AsNumber().Should().Be(expected);
        _engine.Evaluate("d.getTime()").AsNumber().Should().Be(expected);
        _engine.Evaluate("d.valueOf()").AsNumber().Should().Be(expected);
        _engine.Evaluate("+d").AsNumber().Should().Be(expected);
    }

    [Test]
    public void SetTimeAtTheUpperClipBoundaryStillFormats()
    {
        _engine.Execute("var d = new Date(); d.setTime(8.64e15);");
        _engine.Evaluate("d.toISOString()").AsString().Should().Be("+275760-09-13T00:00:00.000Z");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-date.prototype.toisostring throws a RangeError when [[DateValue]] is
    /// NaN, which in the spec's value domain is every non-finite time value there is. Jint's
    /// DatePresentation is wider — an infinity is flags plus a Value of 0 — so the IsNaN guard let one
    /// through to the formatter, which produced the epoch string for it.
    /// </summary>
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    [TestCase("NaN")]
    [TestCase("Number.MAX_VALUE")]
    [TestCase("8.64e15 + 1")]
    public void ToIsoStringThrowsRangeErrorForANonFiniteTimeValue(string argument)
    {
        _engine.Execute($"var d = new Date(); d.setTime({argument});");

        _engine.Evaluate("(function () { try { return d.toISOString(); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("RangeError");
    }

    [Test]
    public void ToIsoStringThrowsRangeErrorForADateConstructedFromNaN()
    {
        _engine.Evaluate("(function () { try { return new Date(NaN).toISOString(); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("RangeError");
    }

    /// <summary>
    /// The invariant behind both of the above, stated once: whatever setTime hands back is what the
    /// object then reports. Object.is rather than === so the NaN rows say something.
    /// </summary>
    [TestCase("0")]
    [TestCase("1")]
    [TestCase("-1")]
    [TestCase("1.5")]
    [TestCase("8.64e15")]
    [TestCase("-8.64e15")]
    [TestCase("8.64e15 + 1")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    [TestCase("NaN")]
    [TestCase("Number.MAX_VALUE")]
    public void SetTimeReturnsWhatGetTimeSubsequentlyReports(string argument)
    {
        _engine.Evaluate($"var d = new Date(); var returned = d.setTime({argument}); Object.is(returned, d.getTime()) && Object.is(returned, d.valueOf());")
            .AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The class of defect rather than the instance: DatePresentation stores an infinity as flags plus a
    /// Value of 0, so any reader testing IsNaN alone reads it back as the epoch. ToJsValue is the single
    /// place every such read funnels through, so answering NaN for anything that is not a finite in-range
    /// time value closes it for the next caller too.
    /// </summary>
    [Test]
    public void AnInfinityFlaggedDatePresentationReadsBackAsNaN()
    {
        DatePresentation positive = double.PositiveInfinity;
        positive.IsInfinity.Should().BeTrue();
        double.IsNaN(positive.ToJsValue().AsNumber()).Should().BeTrue();

        DatePresentation negative = double.NegativeInfinity;
        negative.IsInfinity.Should().BeTrue();
        double.IsNaN(negative.ToJsValue().AsNumber()).Should().BeTrue();

        // The DateTime sentinels are finite, in-range time values and must keep reading back as numbers.
        DatePresentation.MinValue.ToJsValue().AsNumber().Should().Be(JsDate.Min);
        DatePresentation.MaxValue.ToJsValue().AsNumber().Should().Be(JsDate.Max);
    }
}
