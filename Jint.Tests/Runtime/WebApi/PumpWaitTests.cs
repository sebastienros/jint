#if NET8_0_OR_GREATER
#nullable enable

using System.Diagnostics;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <see cref="Engine.TaskOperations.WaitForScheduledWork"/> against the one source of scheduled work that
/// only exists on .NET 8 and later: a due web-API timer.
/// </summary>
/// <remarks>
/// A file of its own rather than a gated region inside <c>Runtime.WaitForScheduledWorkTests</c>, because the
/// wait itself is core surface on every target framework and only what it is being pointed at here is not —
/// the same split <c>HostScheduledWorkTests</c> and <c>WebApiSchedulingSurfaceTests</c> already make for
/// <see cref="Engine.TaskOperations.TimeUntilNextScheduledWork"/>.
/// </remarks>
public class PumpWaitTests
{
    /// <summary>
    /// What the wait under test is given. Nothing should ever spend it: it is the bound a wait that ignored
    /// the engine's own schedule runs into. A minute rather than the ten seconds this was, for the reason
    /// #3369 gives — the absolute number is what a loaded runner crosses, the ratio below is what the
    /// assertion is about, and raising both together widens the margin without weakening it.
    /// </summary>
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The margin the clamped return has to beat. Half the ceiling — derived from it rather than written out
    /// again, so the two cannot drift — which is what makes the assertion separate "woke on the engine's own
    /// due time" from "ran out the ceiling" while saying nothing at all about latency.
    /// </summary>
    private static readonly TimeSpan EarlyReturnMargin = TimeSpan.FromTicks(Ceiling.Ticks / 2);

    /// <summary>
    /// The wait is bounded internally by the engine's own next due time, not only by the ceiling the caller
    /// passed: a <c>setTimeout</c> due in 50 ms wakes a wait that was given a minute. Without that clamp
    /// nothing would end the wait — a timer coming due enqueues nothing, and so wakes nobody — and a host
    /// pumping on a ceiling would run every timer at its own cadence instead of at the script's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The timer's effect is observed through a host-side flag rather than through a script global, and that
    /// is what keeps this off the wall clock (#3379). Probing with <c>Evaluate("fired")</c> put a second
    /// engine entry between the arming and the wait, and <c>RunAvailableContinuations</c> promotes one due
    /// timer whenever the job queue runs dry — so on a runner that took more than fifty milliseconds to get
    /// from the <c>Execute</c> to the probe, <b>the probe ran the timer</b>. The probe's own assertion still
    /// passed, because the value is read before that entry's drain; what failed was the wait below, which
    /// then had nothing left to clamp to, spent its whole ceiling and returned <see langword="false"/>.
    /// Reading a <c>bool</c> enters no engine and drains nothing, so between arming the timer and asking the
    /// wait about it there is now no drain at all.
    /// </para>
    /// <para>
    /// The clock has to stay real here: a fake one would never make the timer due, and the due time is the
    /// very thing the wait is supposed to clamp to.
    /// </para>
    /// </remarks>
    [Fact]
    public void ClampsToTheEnginesOwnSchedule()
    {
        using var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Timers));

        var fired = false;
        engine.SetValue("mark", new Action(() => fired = true));
        engine.Execute("setTimeout(mark, 50);");

        fired.Should().BeFalse("nothing has entered the engine since the timer was armed, so nothing can have run it");

        var elapsed = Stopwatch.StartNew();
        engine.Tasks.WaitForScheduledWork(Ceiling).Should().BeTrue();
        elapsed.Elapsed.Should().BeLessThan(EarlyReturnMargin);

        // The wait reports work; the pump is what runs it. That division is the whole contract.
        engine.Tasks.ProcessTasks();
        fired.Should().BeTrue();
    }
}
#endif
