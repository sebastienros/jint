using System.Globalization;
using Jint.Runtime;
using Microsoft.Extensions.Time.Testing;
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
    
    [Fact]
    public void CanUseTimeProvider()
    {
        var defaultEngine = new Engine();
        var defaultNow = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var defaultScriptNow = defaultEngine.Evaluate("new Date() * 1").AsNumber();
        Assert.InRange(defaultScriptNow, defaultNow, defaultNow + 100);
        
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(new DateTimeOffset(2023, 11, 6, 0, 0, 0, 0, TimeSpan.Zero));
        
        var timeProviderEngine = new Engine(options =>
        {
            options.TimeSystem = new TimeProviderTimeSystem(timeProvider);
        });

        var timeProviderNow = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var timeProviderScriptNow = timeProviderEngine.Evaluate("new Date() * 1").AsNumber();
        Assert.InRange(timeProviderNow, timeProviderScriptNow, timeProviderScriptNow + 100);
        
        Assert.NotInRange(timeProviderScriptNow, defaultScriptNow - 10000, defaultScriptNow + 10000);
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

file sealed class TimeProviderTimeSystem : DefaultTimeSystem
{
    private readonly TimeProvider _timeProvider;
    
    public TimeProviderTimeSystem(TimeProvider timeProvider) : base(TimeZoneInfo.Utc, CultureInfo.CurrentCulture)
    {
        _timeProvider = timeProvider;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _timeProvider.GetUtcNow();
    }
}
