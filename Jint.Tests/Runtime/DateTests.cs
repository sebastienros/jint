using Jint.Native;

namespace Jint.Tests.Runtime;

public class DateTests
{
    private readonly Engine _engine;

    public DateTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(Assert.True))
            .SetValue("equal", new Action<object, object>(Assert.Equal));
    }

    [Fact]
    public void NaNToString()
    {
        var value = _engine.Evaluate("new Date(NaN).toString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void NaNToDateString()
    {
        var value = _engine.Evaluate("new Date(NaN).toDateString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void NaNToTimeString()
    {
        var value = _engine.Evaluate("new Date(NaN).toTimeString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void NaNToLocaleString()
    {
        var value = _engine.Evaluate("new Date(NaN).toLocaleString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void NaNToLocaleDateString()
    {
        var value = _engine.Evaluate("new Date(NaN).toLocaleDateString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void NaNToLocaleTimeString()
    {
        var value = _engine.Evaluate("new Date(NaN).toLocaleTimeString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void ToJsonFromNaNObject()
    {
        var result = _engine.Evaluate("JSON.stringify({ date: new Date(NaN) });");
        Assert.Equal("{\"date\":null}", result.ToString());
    }

    [Fact]
    public void ValuePrecisionIsIntegral()
    {
        var number = _engine.Evaluate("new Date() / 1").AsNumber();
        Assert.Equal((long) number, number);

        var dateInstance = _engine.Realm.Intrinsics.Date.Construct(123);
        Assert.Equal((long) dateInstance.DateValue, dateInstance.DateValue);
    }

    [Fact]
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

        var engine = new Engine(options => options.LocalTimeZone(timeZoneInfo));

        Assert.Equal("Tue Feb 01 2022 00:00:00 GMT+0800 (China Standard Time)", engine.Evaluate("new Date(2022,1,1).toString()"));
        Assert.Equal("Tue Feb 01 2022 00:00:00 GMT+0800 (China Standard Time)", engine.Evaluate("new Date(2022,1,1)").ToString());
    }

    [Fact]
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

        var engine = new Engine(options => options.LocalTimeZone(timeZoneInfo));

        // July 4, 2022 is in summer (EDT = UTC-4, daylight saving time)
        Assert.Contains("(Eastern Daylight Time)", engine.Evaluate("new Date(2022, 6, 4).toString()").AsString());

        // January 4, 2022 is in winter (EST = UTC-5, standard time)
        Assert.Contains("(Eastern Standard Time)", engine.Evaluate("new Date(2022, 0, 4).toString()").AsString());
    }

    [Fact]
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

        var engine = new Engine(options => options.LocalTimeZone(timeZoneInfo));

        const string script = """
            (function() {
                var d = new Date(2022, 6, 4); // July 4, 2022 - DST (EDT)
                return d.toLocaleString('en-US', { timeZoneName: 'short' });
            })();
            """;

        // Should return "EDT" abbreviation, not "Eastern Daylight Time"
        var result = engine.Evaluate(script).AsString();
        Assert.Contains("EDT", result);

        const string scriptWinter = """
            (function() {
                var d = new Date(2022, 0, 4); // January 4, 2022 - standard time (EST)
                return d.toLocaleString('en-US', { timeZoneName: 'short' });
            })();
            """;

        // Should return "EST" abbreviation, not "Eastern Standard Time"
        var resultWinter = engine.Evaluate(scriptWinter).AsString();
        Assert.Contains("EST", resultWinter);
    }

    [Theory]
    [InlineData("Thu, 30 Jan 2020 08:00:00 PST", 1580400000000)]
    [InlineData("Thursday January 01 1970 00:00:25 UTC", 25000)]
    [InlineData("Wednesday 31 December 1969 18:01:26 MDT", 86000)]
    [InlineData("Wednesday 31 December 1969 19:00:08 EST", 8000)]
    [InlineData("Wednesday 31 December 1969 17:01:59 PDT", 119000)]
    [InlineData("December 31 1969 17:01:14 MST", 74000)]
    [InlineData("January 01 1970 01:46:06 +0145", 66000)]
    [InlineData("December 31 1969 17:00:50 PDT", 50000)]
    public void CanParseLocaleString(string input, long expected)
    {
        Assert.Equal(expected, _engine.Evaluate($"new Date('{input}') * 1").AsNumber());
    }

    [Theory]
    [InlineData("December 31 1900 12:00:00 +0300", 31)]
    [InlineData("January 1 1969 12:00:00 +0300", 1)]
    [InlineData("December 31 1969 12:00:00 +0300", 31)]
    [InlineData("January 1 1970 12:00:00 +0300", 1)]
    [InlineData("December 31 1970 12:00:00 +0300", 31)]
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
        var engine = new Engine(options => options.LocalTimeZone(timeZoneInfo));
        Assert.Equal(expectedDate, _engine.Evaluate($"new Date('{input}').getDate()").AsNumber());
    }

    [Fact]
    public void CanUseMoment()
    {
        var momentJs = EngineTests.GetEmbeddedFile("moment.js");
        _engine.Execute(momentJs);

        var parsedDate = _engine.Evaluate("moment().format('YYYY')").ToString();
        Assert.Equal(DateTime.Now.Year.ToString(), parsedDate);
    }

    [Fact]
    public void CanParseEmptyDate()
    {
        Assert.True(double.IsNaN(_engine.Evaluate("Date.parse('')").AsNumber()));
    }

    [Fact]
    public void DateTimeMinValueFlag()
    {
        var date = DateTime.MinValue;
        var jsDate = new JsDate(_engine, date);
        Assert.Equal(DateFlags.DateTimeMinValue, jsDate._dateValue.Flags);

        date = date.AddMilliseconds(1);
        jsDate = new JsDate(_engine, date);
        Assert.Equal(DateFlags.None, jsDate._dateValue.Flags);
    }
    
    [Fact]
    public void DateTimeMaxValueFlag()
    {
        var date = DateTime.MaxValue;
        var jsDate = new JsDate(_engine, date);
        Assert.Equal(DateFlags.DateTimeMaxValue, jsDate._dateValue.Flags);

        date = date.AddMilliseconds(-1);
        jsDate = new JsDate(_engine, date);
        Assert.Equal(DateFlags.None, jsDate._dateValue.Flags);
    }

    [Fact]
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

        var engine = new Engine(options => options.LocalTimeZone(nztz));

        // NZDT (GMT+13) ends at 3:00 AM on the first Sunday of April 2025.
        // At midnight April 6, we are still in NZDT (GMT+13).
        var result1 = engine.Evaluate("new Date(2025, 3, 6, 0, 0, 0).toString()").AsString();
        Assert.Contains("GMT+1300", result1);
        Assert.Contains("Apr 06 2025 00:00:00", result1);

        // NZDT (GMT+13) begins at 2:00 AM on the last Sunday of September 2025.
        // At midnight Sep 28, we are still in NZST (GMT+12).
        var result2 = engine.Evaluate("new Date(2025, 8, 28, 0, 0, 0).toString()").AsString();
        Assert.Contains("GMT+1200", result2);
        Assert.Contains("Sep 28 2025 00:00:00", result2);
    }
}
