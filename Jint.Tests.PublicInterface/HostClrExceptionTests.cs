#nullable enable

using System.Reflection;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host method that itself uses reflection and gets a target wrong raises <see cref="TargetException"/> from
/// inside its own body. Once <c>MethodBase.Invoke</c> has been entered that is indistinguishable from the
/// <see cref="TargetException"/> the invoke raises when <em>Jint's</em> receiver is wrong, so classifying it
/// from the exception rewrote the host's own failure into a TypeError and threw the CLR exception away — the
/// exact thing <see cref="JintException.TryGetClrException"/> exists to preserve. Receiver compatibility is
/// therefore decided before the invoke, and everything coming out of it is the host's.
/// </summary>
public class HostReflectingMethodTests
{
    private sealed class ReflectingHost
    {
        /// <summary>
        /// Two overloads keep the call on the reflection invoke lane on every target framework: the
        /// compiled-invoker fast lane is single-candidate only, and net472 has no such lane at all.
        /// </summary>
        public string Reflect() => Misdirect();

        public string Reflect(int unused) => Misdirect();

        private string Misdirect()
        {
            var method = typeof(string).GetMethod(nameof(string.ToUpperInvariant), Type.EmptyTypes)!;
            // deliberately the wrong target — the TargetException raises inside this method, not in Jint
            return (string) method.Invoke(this, null)!;
        }
    }

    private sealed class SingleMethodReflectingHost
    {
        public string Reflect()
        {
            var method = typeof(string).GetMethod(nameof(string.ToUpperInvariant), Type.EmptyTypes)!;
            return (string) method.Invoke(this, null)!;
        }
    }

    private sealed class UnrelatedHost
    {
        public int Unrelated => 1;
    }

    /// <summary>
    /// Behaves exactly like the default one, but is not it — enough for the engine to decline the compiled
    /// invoker lane, which is the configuration a host with any custom converter is in.
    /// </summary>
    private sealed class CustomTypeConverter(Engine engine) : DefaultTypeConverter(engine);

    [Fact]
    public void AHostMethodsOwnTargetExceptionIsNotMistakenForAReceiverMismatch()
    {
        var engine = new Engine(options => options.CatchClrExceptions());
        engine.SetValue("host", new ReflectingHost());

        var exception = Invoking(() => engine.Evaluate("host.Reflect()")).Should().Throw<JavaScriptException>().Which;

        exception.Message.Should().NotBe("Method 'Reflect' called on incompatible receiver");
        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeAssignableTo<TargetException>();
    }

    [Fact]
    public void TheSameHoldsWhenACustomTypeConverterDeclinesTheCompiledInvokerLane()
    {
        var engine = new Engine(options =>
        {
            options.CatchClrExceptions();
            options.SetTypeConverter(e => new CustomTypeConverter(e));
        });
        engine.SetValue("host", new SingleMethodReflectingHost());

        var exception = Invoking(() => engine.Evaluate("host.Reflect()")).Should().Throw<JavaScriptException>().Which;

        exception.Message.Should().NotBe("Method 'Reflect' called on incompatible receiver");
        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeAssignableTo<TargetException>();
    }

    [Fact]
    public void TheHostExceptionAlsoReachesAScriptCatchUnchanged()
    {
        var engine = new Engine(options => options.CatchClrExceptions());
        engine.SetValue("host", new ReflectingHost());

        engine.Evaluate("try { host.Reflect(); 'no error' } catch (e) { e instanceof TypeError }")
            .AsBoolean().Should().BeFalse("the host's own failure is not a receiver mismatch");
    }

    [Fact]
    public void AForeignReceiverStillSurfacesACatchableTypeErrorOnTheReflectionLane()
    {
        var engine = new Engine(options => options.CatchClrExceptions());
        engine.SetValue("host", new ReflectingHost());
        engine.SetValue("other", new UnrelatedHost());

        var exception = Invoking(() => engine.Evaluate("var f = host.Reflect; f.call(other)"))
            .Should().ThrowExactly<JavaScriptException>().Which;
        exception.Message.Should().Be("Method 'Reflect' called on incompatible receiver");

        engine.Evaluate("var g = host.Reflect; try { g.call(other); 'no error' } catch (e) { e instanceof TypeError }")
            .AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AForeignReceiverIsATypeErrorForASingleCandidateMethodToo()
    {
        var engine = new Engine(options => options.CatchClrExceptions());
        engine.SetValue("host", new SingleMethodReflectingHost());
        engine.SetValue("other", new UnrelatedHost());

        var exception = Invoking(() => engine.Evaluate("var f = host.Reflect; f.call(other)"))
            .Should().ThrowExactly<JavaScriptException>().Which;
        exception.Message.Should().Be("Method 'Reflect' called on incompatible receiver");
    }
}

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
    public void ChainingIsOffByDefault()
    {
        var engine = CreateEngine();

        var exception = Invoking(() => engine.Evaluate("boom()")).Should().Throw<JavaScriptException>().Which;

        // The inner exception is the private wrapper carrying the JavaScript stack, and nothing beyond it.
        exception.InnerException.Should().NotBeNull();
        exception.InnerException!.InnerException.Should().BeNull();
        exception.ToString().Should().NotContain("HostFailure");
        exception.ToString().Should().NotContain("--- End of inner exception stack trace ---\r\n   --- End of");
    }

    [Fact]
    public void ChainingSurfacesTheExceptionToInnerExceptionWalkers()
    {
        var engine = new Engine(options =>
        {
            options.CatchClrExceptions();
            options.ChainClrExceptions();
        });
        engine.SetValue("boom", new Action(() => throw new HostFailure("host blew up", new InvalidOperationException("root cause"))));

        var exception = Invoking(() => engine.Evaluate("boom()")).Should().Throw<JavaScriptException>().Which;

        // JavaScriptException -> wrapper (JavaScript stack) -> the host exception -> its own inner chain.
        var chained = exception.InnerException!.InnerException.Should().BeOfType<HostFailure>().Which;
        chained.InnerException.Should().BeOfType<InvalidOperationException>();

        var rendered = exception.ToString();
        rendered.Should().Contain("host blew up");
        rendered.Should().Contain(nameof(HostFailure));
        rendered.Should().Contain("root cause");
    }

    [Fact]
    public void ChainingKeepsTheClrStackOutOfTheJavaScriptErrorString()
    {
        var engine = new Engine(options =>
        {
            options.CatchClrExceptions();
            options.ChainClrExceptions();
        });
        engine.SetValue("boom", new Action(() => throw new HostFailure("host blew up", new InvalidOperationException("root cause"))));

        var exception = Invoking(() => engine.Evaluate("function inner() { boom(); } inner();"))
            .Should().Throw<JavaScriptException>().Which;

        // GetJavaScriptErrorString is what a host shows a script author: the JavaScript error and its
        // JavaScript frames, never the host's .NET stack
        var errorString = exception.GetJavaScriptErrorString();
        errorString.Should().StartWith("Error: host blew up");
        errorString.Should().Contain("at inner", "the JavaScript frames are what this accessor is for");
        errorString.Should().NotContain(nameof(HostFailure));
        errorString.Should().NotContain("root cause");
        errorString.Should().NotContain("End of inner exception stack trace");

        // and everything the option promises is untouched
        exception.ToString().Should().Contain(nameof(HostFailure));
        exception.InnerException!.InnerException.Should().BeOfType<HostFailure>();
        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeOfType<HostFailure>();
    }

    [Fact]
    public void TheJavaScriptErrorStringIsTheSameWhetherChainingIsOnOrOff()
    {
        static string Render(bool chain)
        {
            var engine = new Engine(options =>
            {
                options.CatchClrExceptions();
                options.ChainClrExceptions(chain);
            });
            engine.SetValue("boom", new Action(() => throw new HostFailure("host blew up")));

            return Invoking(() => engine.Evaluate("function inner() { boom(); } inner();"))
                .Should().Throw<JavaScriptException>().Which.GetJavaScriptErrorString();
        }

        Render(chain: true).Should().Be(Render(chain: false));
    }

    [Fact]
    public void ChainingLeavesGetBaseExceptionAlone()
    {
        var engine = new Engine(options =>
        {
            options.CatchClrExceptions();
            options.ChainClrExceptions();
        });
        engine.SetValue("boom", new Action(() => throw new HostFailure("host blew up")));

        var exception = Invoking(() => engine.Evaluate("boom()")).Should().Throw<JavaScriptException>().Which;

        exception.GetBaseException().Should().BeSameAs(exception);
    }

    [Fact]
    public void ChainingDoesNotChangeWhatTheScriptSees()
    {
        var engine = new Engine(options =>
        {
            options.CatchClrExceptions();
            options.ChainClrExceptions();
        });
        engine.SetValue("boom", new Action(() => throw new HostFailure("host blew up")));

        engine.Evaluate("let caught; try { boom(); } catch (e) { caught = e; }");

        engine.Evaluate("caught instanceof Error").AsBoolean().Should().BeTrue();
        engine.Evaluate("caught.message").AsString().Should().Be("host blew up");
        engine.Evaluate("JSON.stringify(caught)").AsString().Should().Be("{}");
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
