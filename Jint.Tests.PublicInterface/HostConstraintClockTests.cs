#if NET8_0_OR_GREATER

using Jint.Constraints;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins the clock seam the time-based execution constraints expose: <c>Options.Constraints.TimeProvider</c>
/// for the constraint the engine registers from <see cref="ConstraintsOptionsExtensions.TimeoutInterval"/>,
/// and <see cref="OperationDeadlineConstraint(TimeProvider)"/> for the one the host constructs itself.
/// <para>
/// These live in the project <b>without</b> <c>InternalsVisibleTo</c>, so everything here compiling at all
/// is the guarantee that an embedder can reach it. That is the point of the seam being public rather than
/// internal: a host that configures a timeout has exactly the same untestable wall clock Jint's own suite
/// had, and #3232 is about ending that for both of them at once.
/// </para>
/// <para>
/// Only <c>net8.0</c> and later. <c>TimeProvider</c> arrived in .NET 8, and reaching it from Jint's
/// downlevel targets would mean a second runtime dependency on a package that has exactly one.
/// </para>
/// </summary>
public class HostConstraintClockTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// A callee long enough to reach the interpreter's amortized constraint check (every 64 statements),
    /// so a row that expects the timeout to be consulted is not merely hoping it was.
    /// </summary>
    private const string Source = """
        function work() {
            var total = 0;
            for (var i = 0; i < 200; i++) {
                total += i;
            }
            return total;
        }
        """;

    [Fact]
    public void ATimeoutIntervalIsMeasuredAgainstTheConfiguredClock()
    {
        var clock = new ManualClock();
        var engine = new Engine(options =>
        {
            options.Constraints.TimeProvider = clock;
            options.LimitExecutionTime(Interval);
        });
        engine.Execute(Source);
        var work = engine.GetValue("work");

        // The clock has not moved, so no amount of real time can fail this entry...
        Invoking(() => work.Call()).Should().NotThrow();

        // ...and one tick short of the interval it is still inside its budget. The deadline is armed by
        // the per-entry reset, so this is measured from the start of *this* entry.
        clock.Advance(Interval - TimeSpan.FromTicks(1));
        Invoking(() => work.Call()).Should().NotThrow(
            "the deadline is re-armed on entry, so the interval has not elapsed within this entry");

        // ...but an entry that begins with the clock already past its own deadline fails on its first
        // check. Advancing the whole interval *after* the entry has started is what a wall-clock test can
        // never do, so it could only ever assert the one-sided "at least this much elapsed".
        var advancing = new AdvancingClock(Interval);
        var advancingEngine = new Engine(options =>
        {
            options.Constraints.TimeProvider = advancing;
            options.LimitExecutionTime(Interval);
        });
        advancingEngine.Execute(Source);

        Invoking(() => advancingEngine.GetValue("work").Call())
            .Should().Throw<TimeoutException>("the clock passed the deadline while the entry was running");
    }

    [Fact]
    public void TheClockMayBeConfiguredAfterTheIntervalItAppliesTo()
    {
        // The factory the extension method registers runs while the engine is being constructed, not when
        // the interval was configured, so neither order silently drops the clock.
        var clock = new ManualClock();
        var options = new Options().LimitExecutionTime(Interval);
        options.Constraints.TimeProvider = clock;

        var engine = new Engine(options);
        engine.Execute(Source);
        var work = engine.GetValue("work");

        Invoking(() => work.Call()).Should().NotThrow();

        var advancing = new AdvancingClock(Interval);
        var lateOptions = new Options().LimitExecutionTime(Interval);
        lateOptions.Constraints.TimeProvider = advancing;
        var lateEngine = new Engine(lateOptions);
        lateEngine.Execute(Source);

        Invoking(() => lateEngine.GetValue("work").Call()).Should().Throw<TimeoutException>();
    }

    [Fact]
    public void TheDefaultClockIsTheSystemOneAndBehavesExactlyAsItAlwaysDid()
    {
        var options = new Options();
        options.Constraints.TimeProvider.Should().BeSameAs(TimeProvider.System);

        // Explicitly naming the system clock is the same engine as never naming one: TimeProvider's own
        // base GetTimestamp/TimestampFrequency are Stopwatch's, and the constraint folds it away.
        var engine = new Engine(o =>
        {
            o.Constraints.TimeProvider = TimeProvider.System;
            o.LimitExecutionTime(TimeSpan.FromSeconds(30));
        });
        engine.Execute(Source);

        Invoking(() => engine.GetValue("work").Call()).Should().NotThrow();
    }

    [Fact]
    public void AClockThatReportsNoTickRateIsRejectedWhereItIsSupplied()
    {
        // A zero frequency turns every interval into zero ticks, which would arm a deadline equal to the
        // operation's own start and fail its first check. Named where the clock is handed over rather than
        // surfacing later as a timeout nobody can explain.
        Invoking(() => new Engine(o =>
            {
                o.Constraints.TimeProvider = new FrequencylessClock();
                o.LimitExecutionTime(Interval);
            }))
            .Should().Throw<ArgumentException>().WithMessage("*TimestampFrequency*");

        Invoking(() => new OperationDeadlineConstraint(new FrequencylessClock()))
            .Should().Throw<ArgumentException>().WithMessage("*TimestampFrequency*");
    }

    [Fact]
    public void AnOperationDeadlineWithoutAClockIsTheOneItAlwaysWas()
    {
        // The parameterless constructor and an explicit null are the same constraint, so the overload is
        // additive: nothing an embedder already wrote changes meaning.
        foreach (var deadline in new[] { new OperationDeadlineConstraint(), new OperationDeadlineConstraint(null) })
        {
            var engine = new Engine(options => options.AddConstraint(deadline));
            engine.Execute(Source);

            deadline.Begin(TimeSpan.FromMinutes(10));
            try
            {
                Invoking(() => engine.GetValue("work").Call()).Should().NotThrow();
            }
            finally
            {
                deadline.End();
            }
        }
    }

    /// <summary>
    /// A clock the test moves itself. Reports <see cref="TimeSpan.TicksPerSecond"/> so that a tick of this
    /// clock and a tick of a <see cref="TimeSpan"/> are the same unit.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        internal void Advance(TimeSpan amount) => _timestamp += amount.Ticks;
    }

    /// <summary>
    /// A clock that moves on by <paramref name="step"/> every time it is read. An entry arms its deadline
    /// from one reading and checks it against the next, so a step of a whole interval means every entry
    /// provably outlives its own budget — "the interval elapsed while this entry was running", stated
    /// exactly, which is the claim a wall-clock row can only approximate with a sleep and a skip.
    /// </summary>
    private sealed class AdvancingClock(TimeSpan step) : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            var now = _timestamp;
            _timestamp += step.Ticks;
            return now;
        }
    }

    /// <summary>A clock violating <see cref="TimeProvider"/>'s own positive-frequency requirement.</summary>
    private sealed class FrequencylessClock : TimeProvider
    {
        public override long TimestampFrequency => 0;

        public override long GetTimestamp() => 0;
    }
}

#endif
