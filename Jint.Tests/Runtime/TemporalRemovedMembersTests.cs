namespace Jint.Tests.Runtime;

/// <summary>
/// The Temporal reduction of 2024-05-24 also took five members off the <c>Temporal.PlainDateTime</c> and
/// <c>Temporal.ZonedDateTime</c> prototypes, across three normative commits: <c>withPlainDate</c>,
/// <c>epochSeconds</c>, <c>epochMicroseconds</c>, <c>toPlainYearMonth</c> and <c>toPlainMonthDay</c>.
/// Neither prototype object
/// lists them any more — https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plaindatetime-prototype-object
/// and https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-zoneddatetime-prototype-object.
/// test262 asserts their absence in <c>staging/Temporal/removed-methods.js</c>, which Jint's harness does not
/// generate, so the property lists are pinned here instead.
/// </summary>
public class TemporalRemovedMembersTests
{
    [Fact]
    public void PlainDateTimePrototypeExposesExactlyTheMembersTheProposalDefines()
    {
        new Engine().Evaluate("Object.getOwnPropertyNames(Temporal.PlainDateTime.prototype).sort().join()")
            .AsString().Should().Be(
                "add,calendarId,constructor,day,dayOfWeek,dayOfYear,daysInMonth,daysInWeek,daysInYear,equals,era," +
                "eraYear,hour,inLeapYear,microsecond,millisecond,minute,month,monthCode,monthsInYear,nanosecond," +
                "round,second,since,subtract,toJSON,toLocaleString,toPlainDate,toPlainTime,toString,toZonedDateTime," +
                "until,valueOf,weekOfYear,with,withCalendar,withPlainTime,year,yearOfWeek");
    }

    [Fact]
    public void ZonedDateTimePrototypeExposesExactlyTheMembersTheProposalDefines()
    {
        new Engine().Evaluate("Object.getOwnPropertyNames(Temporal.ZonedDateTime.prototype).sort().join()")
            .AsString().Should().Be(
                "add,calendarId,constructor,day,dayOfWeek,dayOfYear,daysInMonth,daysInWeek,daysInYear," +
                "epochMilliseconds,epochNanoseconds,equals,era,eraYear,getTimeZoneTransition,hour,hoursInDay," +
                "inLeapYear,microsecond,millisecond,minute,month,monthCode,monthsInYear,nanosecond,offset," +
                "offsetNanoseconds,round,second,since,startOfDay,subtract,timeZoneId,toInstant,toJSON,toLocaleString," +
                "toPlainDate,toPlainDateTime,toPlainTime,toString,until,valueOf,weekOfYear,with,withCalendar," +
                "withPlainTime,withTimeZone,year,yearOfWeek");
    }

    [Theory]
    [InlineData("Temporal.PlainDateTime", "withPlainDate")]
    [InlineData("Temporal.ZonedDateTime", "epochSeconds")]
    [InlineData("Temporal.ZonedDateTime", "epochMicroseconds")]
    [InlineData("Temporal.ZonedDateTime", "toPlainYearMonth")]
    [InlineData("Temporal.ZonedDateTime", "toPlainMonthDay")]
    public void DoesNotExposeTheRemovedMembers(string type, string removed)
    {
        var engine = new Engine();

        engine.Evaluate($"'{removed}' in {type}.prototype").AsBoolean().Should().BeFalse();
        engine.Evaluate($"{type}.prototype.{removed} === undefined").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void StillAnswersThroughTheSurvivingMembers()
    {
        new Engine().Evaluate("""
            const zdt = Temporal.Instant.fromEpochMilliseconds(1000).toZonedDateTimeISO('UTC');
            const pdt = new Temporal.PlainDateTime(2026, 8, 14, 12, 30, 45);
            [
                pdt.withPlainTime(new Temporal.PlainTime(1, 2, 3)).toString(),
                zdt.epochMilliseconds,
                zdt.epochNanoseconds,
                zdt.toPlainDate().toString(),
                zdt.toPlainDateTime().toString(),
                zdt.toPlainTime().toString(),
            ].join();
            """).AsString().Should().Be("2026-08-14T01:02:03,1000,1000000000,1970-01-01,1970-01-01T00:00:01,00:00:01");
    }
}
