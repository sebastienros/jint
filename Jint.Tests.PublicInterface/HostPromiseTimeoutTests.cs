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
/// <para>
/// The argument-less <see cref="JsValueExtensions.UnwrapIfPromise(JsValue)"/> used to hard-code ten seconds.
/// Its default happens to be ten seconds too, so a host that configured a shorter one was ignored in silence
/// — nothing failed, the call simply waited twenty times longer than asked. The overloads taking an explicit
/// <see cref="TimeSpan"/> or a <see cref="CancellationToken"/> were always honoured and still are, which is
/// why both directions are pinned here.
/// </para>
/// <para>
/// The claim has two halves, and they are asserted separately because they can fail separately. <em>Which</em>
/// budget the wait resolved is stated by the rejection itself, on every target framework. <em>That the wait
/// actually ended on it</em> is a property of the drain rather than of the resolution, and needs a clock the
/// test controls — which is why that half is <c>net8.0</c> and later, where
/// <c>Options.Constraints.TimeProvider</c> exists. It used to be a stopwatch race against the ten-second
/// default, and since the wrong answer <em>is</em> that default, the margin could only ever sit at five
/// seconds: not derivable from the budget under test, not wideable without asserting nothing, and duly
/// failing an unrelated pull request at 47 s (sebastienros/jint#3406).
/// </para>
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
    /// The other half of the same claim where the budget it discriminates against is far enough away for a
    /// clock to answer it: the bound is half of whatever the wrong answer would have spent, so it stays a
    /// midpoint between the two candidates instead of an absolute number a loaded runner can walk past.
    /// Only the row whose wrong answer is minutes away uses it — the row whose wrong answer is the engine's
    /// own ten-second default cannot, and asserts the property directly instead.
    /// </summary>
    private static void ShouldNotHaveSpentTheOtherBudget(Stopwatch elapsed, TimeSpan wrongBudget)
        => elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromTicks(wrongBudget.Ticks / 2));

    /// <summary>
    /// A promise nobody settles, and a budget far below the ten-second default. Reading the default instead
    /// of the configured value would name ten seconds, and the rejection names what the drain was handed.
    /// </summary>
    [Test]
    public void TheArgumentLessUnwrapTakesTheConfiguredPromiseTimeout()
    {
        var configured = TimeSpan.FromMilliseconds(200);

        using var engine = new Engine(options => options.Constraints.PromiseTimeout = configured);
        var manual = engine.Tasks.RegisterPromise();

        var rejection = Assert.Throws<PromiseRejectedException>(() => manual.Promise.UnwrapIfPromise())!;

        ShouldNameTheBudgetItWaitedUnder(rejection, configured);
        rejection.Message.Should().NotContain(
            DefaultPromiseTimeout.ToString(),
            "the default is the wrong answer this row exists to discriminate against");
    }

    /// <summary>
    /// The counterpart: a caller that names a bound gets exactly that bound, whatever the engine is
    /// configured with. Here the configured value is the longer of the two, so honouring it would be the
    /// failure — and it is five minutes away, so the elapsed half of the claim still has a midpoint no
    /// runner reaches.
    /// </summary>
    [Test]
    public void AnExplicitTimeoutStillWinsOverTheConfiguredOne()
    {
        var configured = TimeSpan.FromMinutes(5);
        using var engine = new Engine(options => options.Constraints.PromiseTimeout = configured);
        var manual = engine.Tasks.RegisterPromise();

        var requested = TimeSpan.FromMilliseconds(200);

        var elapsed = Stopwatch.StartNew();
        var rejection = Assert.Throws<PromiseRejectedException>(() => manual.Promise.UnwrapIfPromise(requested))!;
        elapsed.Stop();

        ShouldNameTheBudgetItWaitedUnder(rejection, requested);
        ShouldNotHaveSpentTheOtherBudget(elapsed, configured);
    }

    /// <summary>
    /// A settled promise never reaches the wait at all, so the configured bound costs nothing on the path
    /// every host actually takes.
    /// </summary>
    [Test]
    public void ASettledPromiseIsUnwrappedWithoutConsultingTheBudget()
    {
        using var engine = new Engine(options => options.Constraints.PromiseTimeout = TimeSpan.Zero);
        var manual = engine.Tasks.RegisterPromise();
        manual.Resolve(42);

        manual.Promise.UnwrapIfPromise().AsNumber().Should().Be(42);
    }

#if NET8_0_OR_GREATER

    /// <summary>
    /// How long the wait is given to end when it must not. A "did not happen" bound, so a loaded runner can
    /// only strengthen it: nothing about being slow turns a wait that stayed pending into one that returned.
    /// It is long enough for the drain's ten-millisecond poll to have re-read the clock some twenty-five
    /// times and found the budget still unspent.
    /// </summary>
    private static readonly TimeSpan StillWaitingProbe = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The half a message cannot state: that the drain <em>ended</em> on the budget it named. Asserted
    /// against a clock the test moves itself, so it is exact — the wait survives one tick short of the
    /// configured budget and ends on the tick that reaches it, and no amount of real time on either side
    /// changes either answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the row sebastienros/jint#3406 asked for, and the seam that makes it possible is not new
    /// public API: <c>Options.Constraints.TimeProvider</c> already existed for
    /// <c>LimitExecutionTime</c>, and the promise budget sits beside it on the same options group. What
    /// changed is that the engine's blocking drain now reads its deadline off that clock instead of off
    /// <c>DateTime.UtcNow</c> — monotonic like the pump's own ceiling has always been, and steerable like
    /// every other Jint budget a test needs to be sure about.
    /// </para>
    /// <para>
    /// The discrimination is positive rather than a margin. Had the drain consulted the ten-second default,
    /// two hundred milliseconds of this clock would never reach it and the wait would still be pending when
    /// the ceiling ran out — so the wrong answer is excluded by the test hanging, not by a number chosen to
    /// sit between two candidates.
    /// </para>
    /// </remarks>
    [Test]
    public void TheArgumentLessUnwrapEndsExactlyWhenTheConfiguredBudgetElapsesOnTheEnginesClock()
    {
        var configured = TimeSpan.FromMilliseconds(200);

        using var clock = new GatedClock();
        using var engine = new Engine(options =>
        {
            options.Constraints.TimeProvider = clock;
            options.Constraints.PromiseTimeout = configured;
        });
        var manual = engine.Tasks.RegisterPromise();

        clock.Reads.Should().Be(
            0,
            "the deadline has to be anchored at a reading of this clock the drain itself takes");

        Exception? failure = null;
        var unwrap = DedicatedThread.RunAsync(
            () => failure = Caught.Exception(() => manual.Promise.UnwrapIfPromise()));

        clock.Armed.Wait(TestBudgets.WedgeCeiling).Should().BeTrue(
            "the drain has to have armed its deadline before this thread may move the clock under it");

        clock.Advance(configured - TimeSpan.FromTicks(1));
        unwrap.Wait(StillWaitingProbe).Should().BeFalse(
            "one tick short of the configured budget is inside it, whatever the machine is doing");

        clock.Advance(TimeSpan.FromTicks(1));
        unwrap.Wait(TestBudgets.WedgeCeiling).Should().BeTrue(
            "the configured budget has now elapsed on the engine's own clock — the ten-second default never would");

        var rejection = failure.Should().BeOfType<PromiseRejectedException>().Subject;
        ShouldNameTheBudgetItWaitedUnder(rejection, configured);
    }

    /// <summary>
    /// A clock the test moves itself, which says when it has been read often enough for the drain's
    /// deadline to be anchored.
    /// </summary>
    /// <remarks>
    /// Reports <see cref="TimeSpan.TicksPerSecond"/> so that a tick of this clock and a tick of a
    /// <see cref="TimeSpan"/> are the same unit, exactly as <c>HostConstraintClockTests</c>'s does.
    /// </remarks>
    private sealed class GatedClock : TimeProvider, IDisposable
    {
        private long _timestamp;
        private int _reads;

        /// <summary>
        /// Set once this clock has been read twice: the drain reads once to arm its deadline and again on
        /// its first pass to ask what is left, so a second reading proves the first has already been turned
        /// into a deadline. Advancing before that would move the origin the deadline is anchored at, which
        /// is the one thing a test steering a clock must not do.
        /// </summary>
        internal ManualResetEventSlim Armed { get; } = new(false);

        /// <summary>How many readings this clock has handed out.</summary>
        internal int Reads => Volatile.Read(ref _reads);

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            // Read before the count is published, so a reading can never be attributed to a deadline that
            // was armed from a later one.
            var now = Interlocked.Read(ref _timestamp);
            if (Interlocked.Increment(ref _reads) >= 2)
            {
                Armed.Set();
            }

            return now;
        }

        internal void Advance(TimeSpan amount) => Interlocked.Add(ref _timestamp, amount.Ticks);

        public void Dispose() => Armed.Dispose();
    }

#endif
}
