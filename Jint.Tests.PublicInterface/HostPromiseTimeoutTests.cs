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
    /// A promise nobody settles, and a budget far below the ten-second default. The elapsed time is the
    /// assertion: reading the default instead of the configured value would park for ten seconds.
    /// </summary>
    [Fact]
    public void TheArgumentLessUnwrapTakesTheConfiguredPromiseTimeout()
    {
        var configured = TimeSpan.FromMilliseconds(200);

        using var engine = new Engine(options => options.Constraints.PromiseTimeout = configured);
        var manual = engine.Advanced.RegisterPromise();

        var elapsed = Stopwatch.StartNew();
        var rejection = Assert.Throws<PromiseRejectedException>(() => manual.Promise.UnwrapIfPromise());
        elapsed.Stop();

        // Generous against a loaded CI machine, and still an order of magnitude below the ten seconds the
        // hard-coded default would have cost.
        elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        rejection.Message.Should().Contain(configured.ToString());
    }

    /// <summary>
    /// The counterpart: a caller that names a bound gets exactly that bound, whatever the engine is
    /// configured with. Here the configured value is the longer of the two, so honouring it would be the
    /// failure.
    /// </summary>
    [Fact]
    public void AnExplicitTimeoutStillWinsOverTheConfiguredOne()
    {
        using var engine = new Engine(options => options.Constraints.PromiseTimeout = TimeSpan.FromMinutes(5));
        var manual = engine.Advanced.RegisterPromise();

        var requested = TimeSpan.FromMilliseconds(200);

        var elapsed = Stopwatch.StartNew();
        var rejection = Assert.Throws<PromiseRejectedException>(() => manual.Promise.UnwrapIfPromise(requested));
        elapsed.Stop();

        elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        rejection.Message.Should().Contain(requested.ToString());
    }

    /// <summary>
    /// A settled promise never reaches the wait at all, so the configured bound costs nothing on the path
    /// every host actually takes.
    /// </summary>
    [Fact]
    public void ASettledPromiseIsUnwrappedWithoutConsultingTheBudget()
    {
        using var engine = new Engine(options => options.Constraints.PromiseTimeout = TimeSpan.Zero);
        var manual = engine.Advanced.RegisterPromise();
        manual.Resolve(42);

        manual.Promise.UnwrapIfPromise().AsNumber().Should().Be(42);
    }
}
