#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Behaviour contract for the originating CLR exception behind a JavaScript error. When
/// <c>CatchClrExceptions</c> turns a host exception into a script-catchable error, the exception itself is
/// recorded on the error object — host-only CLR state, never a JavaScript property — so it still reaches the
/// host through <see cref="JintException.TryGetClrException"/> after the error has crossed back out.
/// <para>
/// The error <em>value</em> is what carries it, because the .NET exception instance thrown inside the engine is
/// not the one the host catches: the interpreter reduces a throw to its error value and a fresh
/// <see cref="JavaScriptException"/> is built from that at the boundary. So these cover the crossings that
/// reconstruction goes through — nested frames, a script-level catch and rethrow, module evaluation, a rejected
/// promise — plus the script-visibility and cause-walking obligations the feature takes on.
/// </para>
/// </summary>
public class HostClrExceptionTests
{
    private sealed class HostFailure(string message, Exception? inner = null) : Exception(message, inner);

    private static Engine CreateEngine()
    {
        var engine = new Engine(options => options.CatchClrExceptions());
        engine.SetValue("boom", new Action(() => throw new HostFailure("host blew up", new InvalidOperationException("root cause"))));
        return engine;
    }

    [Fact]
    public void UncaughtHostExceptionReachesTheHostThroughTheJavaScriptError()
    {
        var engine = CreateEngine();

        var exception = Invoking(() => engine.Evaluate("boom()")).Should().Throw<JavaScriptException>().Which;

        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        var failure = clrException.Should().BeOfType<HostFailure>().Which;
        failure.Message.Should().Be("host blew up");
        failure.InnerException.Should().BeOfType<InvalidOperationException>();
        failure.StackTrace.Should().NotBeNull("the CLR stack trace is the reason for keeping the exception");
    }

    [Fact]
    public void SurvivesNestedScriptFrames()
    {
        var engine = CreateEngine();

        var exception = Invoking(() => engine.Evaluate("""
            function inner() { boom(); }
            function outer() { inner(); }
            outer();
            """)).Should().Throw<JavaScriptException>().Which;

        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeOfType<HostFailure>();
    }

    [Fact]
    public void SurvivesAScriptCatchingAndRethrowingTheSameValue()
    {
        var engine = CreateEngine();

        var exception = Invoking(() => engine.Evaluate("try { boom(); } catch (e) { throw e; }"))
            .Should().Throw<JavaScriptException>().Which;

        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeOfType<HostFailure>();
    }

    [Fact]
    public void SurvivesModuleEvaluation()
    {
        var engine = new Engine(options => options.CatchClrExceptions());
        engine.SetValue("boom", new Action(() => throw new HostFailure("host blew up")));
        engine.Modules.Add("failing", "boom();");

        var exception = Invoking(() => engine.Modules.Import("failing")).Should().Throw<JavaScriptException>().Which;

        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeOfType<HostFailure>();
    }

    [Fact]
    public void IsReachableThroughARejectedPromise()
    {
        var engine = CreateEngine();

        var rejection = Invoking(() => engine.Evaluate("(async () => { boom(); })()").UnwrapIfPromise())
            .Should().Throw<PromiseRejectedException>().Which;

        JintException.TryGetClrException(rejection, out var clrException).Should().BeTrue();
        clrException.Should().BeOfType<HostFailure>();
    }

    [Fact]
    public void FollowsAnErrorCauseChain()
    {
        var engine = CreateEngine();

        var exception = Invoking(() => engine.Evaluate("""
            try { boom(); } catch (e) { throw new Error('wrapped', { cause: e }); }
            """)).Should().Throw<JavaScriptException>().Which;

        exception.Message.Should().Be("wrapped");
        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeOfType<HostFailure>();
    }

    [Fact]
    public void ACauseCycleTerminates()
    {
        var engine = CreateEngine();

        var exception = Invoking(() => engine.Evaluate("""
            const a = new Error('a');
            const b = new Error('b', { cause: a });
            a.cause = b;
            throw b;
            """)).Should().Throw<JavaScriptException>().Which;

        JintException.TryGetClrException(exception, out var clrException).Should().BeFalse();
        clrException.Should().BeNull();
    }

    [Fact]
    public void AnAccessorValuedCauseIsNeverInvoked()
    {
        var engine = CreateEngine();

        var exception = Invoking(() => engine.Evaluate("""
            globalThis.getterRan = false;
            const e = new Error('wrapped');
            Object.defineProperty(e, 'cause', { get() { globalThis.getterRan = true; return new Error('x'); } });
            throw e;
            """)).Should().Throw<JavaScriptException>().Which;

        JintException.TryGetClrException(exception, out _).Should().BeFalse();
        engine.Evaluate("globalThis.getterRan").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void AScriptThatDiscardsTheErrorKeepsNothing()
    {
        var engine = CreateEngine();

        var exception = Invoking(() => engine.Evaluate("try { boom(); } catch (e) { throw new Error('unrelated'); }"))
            .Should().Throw<JavaScriptException>().Which;

        JintException.TryGetClrException(exception, out _).Should().BeFalse();
    }

    [Fact]
    public void IsInvisibleToTheRunningScript()
    {
        var engine = CreateEngine();

        engine.Evaluate("""
            let caught;
            try { boom(); } catch (e) { caught = e; }
            caught;
            """);

        engine.Evaluate("Object.getOwnPropertyNames(caught).join(',')").AsString()
            .Should().NotContain("clr", "the exception is CLR state, not a property");
        engine.Evaluate("Reflect.ownKeys(caught).length").AsNumber()
            .Should().Be(engine.Evaluate("Object.getOwnPropertyNames(caught).length").AsNumber());
        engine.Evaluate("Object.getOwnPropertySymbols(caught).length").AsNumber().Should().Be(0);
        engine.Evaluate("JSON.stringify(caught)").AsString().Should().Be("{}");
    }

    [Fact]
    public void ADecoratorSeesTheExceptionAlreadyAttached()
    {
        Exception? seenByDecorator = null;
        var engine = new Engine(options =>
        {
            options.CatchClrExceptions();
            options.DecorateClrExceptionErrors((_, error, clrException) =>
            {
                seenByDecorator = clrException;
                error.Set("code", "E_HOST");
            });
        });
        engine.SetValue("boom", new Action(() => throw new HostFailure("host blew up")));

        var exception = Invoking(() => engine.Evaluate("boom()")).Should().Throw<JavaScriptException>().Which;

        seenByDecorator.Should().BeOfType<HostFailure>();
        exception.Error.AsObject().Get("code").AsString().Should().Be("E_HOST");
        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeSameAs(seenByDecorator);
    }

    [Fact]
    public void HostCodeCanThrowAnErrorCarryingItsOwnException()
    {
        var engine = new Engine();
        var original = new HostFailure("XML parsing failed", new InvalidOperationException("unexpected token"));

        engine.SetValue("parse", new Action(() =>
            throw new JavaScriptException(engine.Intrinsics.Error, "XML parsing failed", original)));

        // Catchable by the script, exactly as an ordinary Error is - and without CatchClrExceptions.
        engine.Evaluate("let m; try { parse(); } catch (e) { m = e instanceof Error ? e.message : 'not an error'; } m")
            .AsString().Should().Be("XML parsing failed");

        var exception = Invoking(() => engine.Evaluate("parse()")).Should().Throw<JavaScriptException>().Which;
        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeSameAs(original);
    }

    [Fact]
    public void HostThrownErrorTakesTheExceptionMessageWhenNoneIsGiven()
    {
        var engine = new Engine();
        var original = new HostFailure("XML parsing failed");

        engine.SetValue("parse", new Action(() =>
            throw new JavaScriptException(engine.Intrinsics.Error, null, original)));

        var exception = Invoking(() => engine.Evaluate("parse()")).Should().Throw<JavaScriptException>().Which;

        exception.Message.Should().Be("XML parsing failed");
        exception.Error.AsObject().Get("message").AsString().Should().Be("XML parsing failed");
    }

    [Fact]
    public void AnErrorWithNoClrOriginReportsNothing()
    {
        var engine = new Engine();

        var exception = Invoking(() => engine.Evaluate("throw new Error('plain')")).Should().Throw<JavaScriptException>().Which;

        JintException.TryGetClrException(exception, out var clrException).Should().BeFalse();
        clrException.Should().BeNull();
    }

    [Fact]
    public void NullAndUnrelatedExceptionsAreAccepted()
    {
        JintException.TryGetClrException(null, out _).Should().BeFalse();
        JintException.TryGetClrException(new InvalidOperationException(), out _).Should().BeFalse();
    }

    [Fact]
    public void AThrownNonErrorValueReportsNothing()
    {
        var engine = new Engine();

        var exception = Invoking(() => engine.Evaluate("throw 'a string'")).Should().Throw<JavaScriptException>().Which;

        exception.Error.Should().BeOfType<JsString>();
        JintException.TryGetClrException(exception, out _).Should().BeFalse();
    }
}
