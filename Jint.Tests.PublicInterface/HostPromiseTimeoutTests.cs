#nullable enable

using System.Diagnostics;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>Options.Constraints.PromiseTimeout</c> from the embedder's side: the bound a host configures is the
/// bound the blocking unwrap actually waits under.
/// </summary>
/// <remarks>
/// The argument-less <see cref="JsValueExtensions.UnwrapIfPromise(JsValue)"/> used to hard-code ten seconds.
/// Its default happens to be ten seconds too, so a host that configured a shorter one was ignored in silence
/// — nothing failed, the call simply waited twenty times longer than asked. The overloads taking an explicit
/// <see cref="TimeSpan"/> or a <see cref="CancellationToken"/> were always honoured and still are, which is
/// why both directions are pinned here.
/// </remarks>
public class HostPromiseTimeoutTests
{
    /// <summary>
    /// <c>Options.Constraints.PromiseTimeout</c>'s own default, and so what the wrong answer costs in each
    /// row below where the engine's configured value is not the one asserted about.
    /// </summary>
    private static readonly TimeSpan DefaultPromiseTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Which budget the wait actually ran under, stated by the wait itself. <c>UnwrapIfPromiseCore</c>
    /// resolves the effective timeout into one local, hands that local to the drain, and formats the very
    /// same local into this message — so a wait that consulted the wrong budget cannot name the right one,
    /// and this is a witness rather than a restatement of the configuration.
    /// </summary>
    private static void ShouldNameTheBudgetItWaitedUnder(PromiseRejectedException rejection, TimeSpan budget)
        => rejection.Message.Should().Contain(budget.ToString());

    /// <summary>
    /// The other half of the same claim, and the half a clock is needed for: that the wait <em>ended</em> on
    /// the budget it names rather than sitting out the longer one it should have ignored. The bound is half
    /// of whatever the wrong answer would have spent, so it stays a midpoint between the two candidates
    /// instead of an absolute number a loaded runner can walk past — #3358 saw a 200 ms wait measured at
    /// 1 m 3 s against a fixed 5 s.
    /// </summary>
    private static void ShouldNotHaveSpentTheOtherBudget(Stopwatch elapsed, TimeSpan wrongBudget)
        => elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromTicks(wrongBudget.Ticks / 2));

    /// <summary>
    /// A promise nobody settles, and a budget far below the ten-second default. Reading the default instead
    /// of the configured value would name ten seconds and park for ten seconds, and both are asserted.
    /// </summary>
    [Fact]
    public void TheArgumentLessUnwrapTakesTheConfiguredPromiseTimeout()
    {
        var configured = TimeSpan.FromMilliseconds(200);

        using var engine = new Engine(options => options.Constraints.PromiseTimeout = configured);
        var manual = engine.Tasks.RegisterPromise();

        var elapsed = Stopwatch.StartNew();
        var rejection = Assert.Throws<PromiseRejectedException>(() => manual.Promise.UnwrapIfPromise());
        elapsed.Stop();

        ShouldNameTheBudgetItWaitedUnder(rejection, configured);
        ShouldNotHaveSpentTheOtherBudget(elapsed, DefaultPromiseTimeout);
    }

    /// <summary>
    /// The counterpart: a caller that names a bound gets exactly that bound, whatever the engine is
    /// configured with. Here the configured value is the longer of the two, so honouring it would be the
    /// failure.
    /// </summary>
    [Fact]
    public void AnExplicitTimeoutStillWinsOverTheConfiguredOne()
    {
        var configured = TimeSpan.FromMinutes(5);
        using var engine = new Engine(options => options.Constraints.PromiseTimeout = configured);
        var manual = engine.Tasks.RegisterPromise();

        var requested = TimeSpan.FromMilliseconds(200);

        var elapsed = Stopwatch.StartNew();
        var rejection = Assert.Throws<PromiseRejectedException>(() => manual.Promise.UnwrapIfPromise(requested));
        elapsed.Stop();

        ShouldNameTheBudgetItWaitedUnder(rejection, requested);
        ShouldNotHaveSpentTheOtherBudget(elapsed, configured);
    }

    /// <summary>
    /// A settled promise never reaches the wait at all, so the configured bound costs nothing on the path
    /// every host actually takes.
    /// </summary>
    [Fact]
    public void ASettledPromiseIsUnwrappedWithoutConsultingTheBudget()
    {
        using var engine = new Engine(options => options.Constraints.PromiseTimeout = TimeSpan.Zero);
        var manual = engine.Tasks.RegisterPromise();
        manual.Resolve(42);

        manual.Promise.UnwrapIfPromise().AsNumber().Should().Be(42);
    }
}
