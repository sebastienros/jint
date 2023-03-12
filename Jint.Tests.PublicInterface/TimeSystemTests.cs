using System.Globalization;
using Jint.Runtime;
using NodaTime;

namespace Jint.Tests.PublicInterface;

public class TimeSystemTests
{
    [Theory]
    [InlineData("401, 0, 1, 0, 0, 0, 0", -49512821989000)]
    [InlineData("1900, 0, 1, 0, 0, 0, 0", -2208994789000)]
    [InlineData("1920, 0, 1, 0, 0, 0, 0", -1577929189000)]
    [InlineData("1969, 0, 1, 0, 0, 0, 0", -31543200000)]
    [InlineData("2000, 1, 1, 1, 1, 1, 1", 949359661001)]
    public void CanProduceValidDatesUsingNodaTimeIntegration(string input, long expected)
    {
        var dateTimeZone = DateTimeZoneProviders.Tzdb["Europe/Helsinki"];
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki");
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
        }

        var engine = new Engine(options =>
        {
            options.TimeZone = timeZone;
            options.TimeSystem = new NodaTimeSystem(dateTimeZone, timeZone);
        });

        Assert.Equal(expected, engine.Evaluate($"new Date({input}) * 1").AsNumber());
    }
}

file sealed class NodaTimeSystem : DefaultTimeSystem
{
    private readonly DateTimeZone _dateTimeZone;

    public NodaTimeSystem(
        DateTimeZone dateTimeZone,
        TimeZoneInfo timeZoneInfo) : base(timeZoneInfo, CultureInfo.CurrentCulture)
    {
        _dateTimeZone = dateTimeZone;
    }

    public override TimeSpan GetUtcOffset(long epochMilliseconds)
    {
        var offset = _dateTimeZone.GetUtcOffset(Instant.FromUnixTimeMilliseconds(epochMilliseconds));
        return offset.ToTimeSpan();
    }
}
