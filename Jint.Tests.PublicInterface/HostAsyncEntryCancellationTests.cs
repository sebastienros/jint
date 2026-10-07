#nullable enable

using Jint.Constraints;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins, from an embedder's side, that the token handed to an <c>*Async</c> entry is enough on its own to stop
/// the script that entry runs — the shape of a host whose <see cref="Options"/> are shared by every engine of
/// a tenant while its cancellation token is per request, so <c>ObserveCancellation</c>, fixed when the options
/// are built, cannot carry it.
/// </summary>
/// <remarks>
/// Before this held, such a host had to look up an <see cref="OperationDeadlineConstraint"/> it registered
/// through a per-engine factory for no other reason, arm it with <c>Begin(Timeout.InfiniteTimeSpan, token)</c>
/// around every call and <c>End()</c> it in a <c>finally</c>, and check the token up front itself. The last
/// test pins that a host which keeps that bracket is not broken by the engine now doing the same. Every
/// cancel is issued from the engine thread by a <see cref="ClrFunction"/>, and every loop is bounded, so a
/// build that ignores the token fails an assertion rather than hanging.
/// </remarks>
public class HostAsyncEntryCancellationTests
{
    private const string BoundedLoop = "var i = 0; cancel(); for (; i < 1000000; i++) { } i";

    [Test]
    public async Task ARequestTokenStopsTheScriptWithNothingRegisteredOnTheSharedOptions()
    {
        var shared = new Options();
        using var request = new CancellationTokenSource();
        var engine = new Engine(shared);
        engine.SetValue("cancel", Cancel(engine, request));

        var outcome = "completed";
        try
        {
            await engine.EvaluateAsync(BoundedLoop, cancellationToken: request.Token);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == request.Token)
        {
            outcome = "cancelled by the request";
        }

        outcome.Should().Be("cancelled by the request");
        engine.GetValue("i").AsNumber().Should().BeLessThan(1000);

        // The request's token lived for that call only: neither this engine nor another one built from the
        // same options observes it afterwards.
        engine.Evaluate("for (var k = 0; k < 100000; k++) { } k").Should().Be(100000);
        var sibling = new Engine(shared);
        (await sibling.EvaluateAsync("for (var k = 0; k < 100000; k++) { } k")).Should().Be(100000);
    }

    [Test]
    public async Task AnAlreadyCancelledRequestRunsNothing()
    {
        using var request = new CancellationTokenSource();
        request.Cancel();
        var engine = new Engine();
        var ran = false;
        engine.SetValue("touch", new ClrFunction(engine, "touch", (_, _) =>
        {
            ran = true;
            return JsValue.Undefined;
        }));

        var pending = engine.EvaluateAsync("touch(); 1", cancellationToken: request.Token);
        var exception = await Caught.ExceptionAsync(() => pending);

        pending.IsCanceled.Should().BeTrue();
        exception.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
        ran.Should().BeFalse();
    }

    [Test]
    public async Task AnInvokedHandlerIsStoppedByItsRequestToken()
    {
        using var request = new CancellationTokenSource();
        var engine = new Engine();
        engine.SetValue("cancel", Cancel(engine, request));
        engine.Execute("var i = 0; function handle() { cancel(); for (; i < 1000000; i++) { } return i; }");

        var exception = await Caught.ExceptionAsync(() => engine.InvokeAsync("handle", request.Token));

        exception.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
        engine.GetValue("i").AsNumber().Should().BeLessThan(1000);
    }

    [Test]
    public async Task AHostThatKeepsItsOperationDeadlineBracketStillGetsItsCancellation()
    {
        using var request = new CancellationTokenSource();
        var shared = new Options();
        shared.AddConstraint(() => new OperationDeadlineConstraint());
        var engine = new Engine(shared);
        engine.SetValue("cancel", Cancel(engine, request));
        var deadline = engine.Constraints.Find<OperationDeadlineConstraint>()!;

        Exception? exception;
        deadline.Begin(Timeout.InfiniteTimeSpan, request.Token);
        try
        {
            exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync(BoundedLoop, cancellationToken: request.Token));
        }
        finally
        {
            deadline.End();
        }

        exception.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
        engine.GetValue("i").AsNumber().Should().BeLessThan(1000);
    }

    private static ClrFunction Cancel(Engine engine, CancellationTokenSource source)
        => new(engine, "cancel", (_, _) =>
        {
            source.Cancel();
            return JsValue.Undefined;
        });
}
