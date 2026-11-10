#nullable enable

using System.Collections;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins what a wrapped CLR collection's enumerator does when the host's <c>Dispose</c> throws, or runs script that trips an
/// execution constraint, while a throw completion is closing the iterator over it. IteratorClose step 5 lets the script's
/// error win over the host's failure (https://tc39.es/ecma262/#sec-iteratorclose), but never over a constraint.
/// </summary>
public class HostIteratorDisposeTests
{
    private sealed class Sequence(Action onDispose) : IEnumerable
    {
        public IEnumerator GetEnumerator() => new Enumerator(onDispose);

        private sealed class Enumerator(Action onDispose) : IEnumerator, IDisposable
        {
            public object Current => 1;

            public bool MoveNext() => true;

            public void Reset()
            {
            }

            public void Dispose() => onDispose();
        }
    }

    private static Engine CreateEngine(Action<Options>? configure = null, Action? onDispose = null)
    {
        var engine = new Engine(configure ?? (_ => { }));
        engine.SetValue("seq", new Sequence(onDispose ?? (() => throw new InvalidOperationException("dispose"))));
        engine.Execute("function fail() { throw new Error('body'); } var caught;");
        return engine;
    }

    [Theory]
    [InlineData("try { for (const x of seq) { throw new Error('body'); } } catch (e) { caught = e.message; }", "body")]
    [InlineData("try { const [[a]] = seq; } catch (e) { caught = e.constructor.name; }", "TypeError")]
    [InlineData("try { Array.from(seq, fail); } catch (e) { caught = e.message; }", "body")]
    [InlineData("try { Iterator.zip(seq); } catch (e) { caught = e.constructor.name; }", "TypeError")]
    [InlineData("try { Iterator.from(seq).map(fail).next(); } catch (e) { caught = e.message; }", "body")]
    public void AThrowingHostDisposeKeepsTheScriptsError(string script, string expected)
    {
        var engine = CreateEngine();

        engine.Execute(script);

        engine.Evaluate("caught").Should().Be(expected);
    }

    [Theory]
    [InlineData("try { for (const x of seq) { throw new Error('body'); } } catch (e) { }")]
    [InlineData("try { const [[a]] = seq; } catch (e) { }")]
    [InlineData("try { Array.from(seq, fail); } catch (e) { }")]
    [InlineData("try { Iterator.zip(seq); } catch (e) { }")]
    public void AConstraintThatTripsInsideTheHostsDisposeEndsTheRun(string script)
    {
        Engine? engine = null;
        engine = CreateEngine(
            options => options.MaxStatements(1_000),
            () => engine!.Evaluate("(function () { for (let i = 0; i < 100000; i++) { } })()"));

        Record.Exception(() => engine.Execute(script)).Should().BeOfType<StatementsCountOverflowException>();
    }

    [Theory]
    [InlineData("MaxStatements")]
    [InlineData("TimeoutInterval")]
    public void ClrFunctionDoesNotHandACaughtClrExceptionAConstraintThatTripsInAScriptCallback(string bound)
    {
        Engine? engine = null;
        engine = new Engine(options =>
        {
            options.CatchClrExceptions();
            if (bound == "MaxStatements")
            {
                options.MaxStatements(1_000);
            }
            else
            {
                options.TimeoutInterval(TimeSpan.FromSeconds(2));
            }
        });
        engine.SetValue("callBack", new ClrFunction(engine, "callBack", (_, _) =>
        {
            if (bound == "TimeoutInterval")
            {
                Thread.Sleep(2500);
            }

            return engine!.Evaluate("(function () { for (let i = 0; i < 100000; i++) { } })()");
        }));

        var thrown = Record.Exception(() => engine.Evaluate("try { callBack(); } catch (e) { }"));

        thrown.Should().NotBeNull();
        thrown.Should().Match<Exception>(e => e is StatementsCountOverflowException || e is TimeoutException);
    }
}
