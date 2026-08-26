namespace Jint.Tests.Runtime;

/// <summary>
/// The <c>Temporal.Now</c> namespace object — https://tc39.es/proposal-temporal/#sec-temporal-now-object.
/// The June 2024 removals took the calendar-taking <c>plainDate</c>, <c>plainDateTime</c> and
/// <c>zonedDateTime</c> out; only the <c>*ISO</c> forms plus <c>instant</c> and <c>timeZoneId</c> remain.
/// test262 asserts their absence in <c>staging/Temporal/removed-methods.js</c>, which Jint's harness does not
/// generate, so the property list is pinned here instead.
/// </summary>
public class TemporalNowTests
{
    [Test]
    public void ExposesExactlyTheMembersTheProposalDefines()
    {
        new Engine().Evaluate("Object.getOwnPropertyNames(Temporal.Now).sort().join()")
            .AsString().Should().Be("instant,plainDateISO,plainDateTimeISO,plainTimeISO,timeZoneId,zonedDateTimeISO");
    }

    [TestCase("plainDate")]
    [TestCase("plainDateTime")]
    [TestCase("zonedDateTime")]
    public void DoesNotExposeTheRemovedCalendarTakingForms(string removed)
    {
        var engine = new Engine();

        engine.Evaluate($"'{removed}' in Temporal.Now").AsBoolean().Should().BeFalse();
        engine.Evaluate($"Temporal.Now.{removed} === undefined").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void StillAnswersThroughTheIsoForms()
    {
        new Engine().Evaluate("""
            [
                Temporal.Now.instant() instanceof Temporal.Instant,
                Temporal.Now.plainDateISO() instanceof Temporal.PlainDate,
                Temporal.Now.plainDateTimeISO() instanceof Temporal.PlainDateTime,
                Temporal.Now.plainTimeISO() instanceof Temporal.PlainTime,
                Temporal.Now.zonedDateTimeISO() instanceof Temporal.ZonedDateTime,
                typeof Temporal.Now.timeZoneId() === 'string',
                Temporal.Now.plainDateISO().calendarId === 'iso8601',
            ].join();
            """).AsString().Should().Be("true,true,true,true,true,true,true");
    }

    [Test]
    public void CarriesTheNamespaceToStringTag()
    {
        new Engine().Evaluate("Temporal.Now[Symbol.toStringTag] + '/' + Object.prototype.toString.call(Temporal.Now)")
            .AsString().Should().Be("Temporal.Now/[object Temporal.Now]");
    }
}
