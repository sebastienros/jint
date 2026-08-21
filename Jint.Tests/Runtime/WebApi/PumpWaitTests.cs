#if NET8_0_OR_GREATER
#nullable enable

using System.Diagnostics;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <see cref="Engine.AdvancedOperations.WaitForScheduledWork"/> against the one source of scheduled work that
/// only exists on .NET 8 and later: a due web-API timer.
/// </summary>
/// <remarks>
/// A file of its own rather than a gated region inside <c>Runtime.WaitForScheduledWorkTests</c>, because the
/// wait itself is core surface on every target framework and only what it is being pointed at here is not —
/// the same split <c>HostScheduledWorkTests</c> and <c>WebApiSchedulingSurfaceTests</c> already make for
/// <see cref="Engine.AdvancedOperations.TimeUntilNextScheduledWork"/>.
/// </remarks>
public class PumpWaitTests
{
    /// <summary>
    /// The wait is bounded internally by the engine's own next due time, not only by the ceiling the caller
    /// passed: a <c>setTimeout</c> due in 50 ms wakes a wait that was given ten seconds. Without that clamp
    /// nothing would end the wait — a timer coming due enqueues nothing, and so wakes nobody — and a host
    /// pumping on a ceiling would run every timer at its own cadence instead of at the script's.
    /// </summary>
    [Fact]
    public void ClampsToTheEnginesOwnSchedule()
    {
        using var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Timers));
        engine.Execute("globalThis.fired = false; setTimeout(function () { globalThis.fired = true; }, 50);");

        // Nothing has run it yet: Execute drains the loop, but a timer 50 ms out is not due.
        engine.Evaluate("fired").AsBoolean().Should().BeFalse();

        var elapsed = Stopwatch.StartNew();
        engine.Advanced.WaitForScheduledWork(TimeSpan.FromSeconds(10)).Should().BeTrue();
        elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));

        // The wait reports work; the pump is what runs it. That division is the whole contract.
        engine.Advanced.ProcessTasks();
        engine.Evaluate("fired").AsBoolean().Should().BeTrue();
    }
}
#endif
