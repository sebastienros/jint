#nullable enable

using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host <c>operator</c> that throws is a host method that throws, and the embedder has to receive the
/// same exception from both.
/// </summary>
/// <remarks>
/// Every other interop call site normalizes what it catches with
/// <c>e as TargetInvocationException ?? new TargetInvocationException(e)</c>; the operator-overloading path
/// wrapped <c>e.InnerException</c> instead, and by the time it catches, the invoke has already been
/// unwrapped — so <c>InnerException</c> is the host exception's own cause, or nothing at all. The pairs
/// below are the measurement: the same failure raised from an ordinary method and from an operator, which
/// must arrive identically.
/// </remarks>
public class HostOperatorExceptionTests
{
    public sealed class Meter
    {
        public Meter(double value) => Value = value;

        public double Value { get; }

        /// <summary>A plain failure, with a message and no cause of its own.</summary>
        public static Meter operator +(Meter left, Meter right) => throw new InvalidOperationException("HOST BOOM");

        /// <summary>A failure that carries a cause, which is the half that got reported instead of it.</summary>
        public static Meter operator -(Meter left, Meter right)
            => throw new NotSupportedException("metres do not subtract", new ArgumentException("nested cause"));

        public Meter Add(Meter other) => throw new InvalidOperationException("HOST BOOM");

        public Meter Subtract(Meter other)
            => throw new NotSupportedException("metres do not subtract", new ArgumentException("nested cause"));
    }

    private static Exception? Evaluate(string source)
    {
        var engine = new Engine(options => options.Interop.AllowOperatorOverloading = true);
        engine.SetValue("a", new Meter(1));
        engine.SetValue("b", new Meter(2));

        try
        {
            engine.Evaluate(source);
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    [Fact]
    public void AHostOperatorsFailureArrivesWithItsMessage()
    {
        var fromMethod = Evaluate("a.Add(b)");
        var fromOperator = Evaluate("a + b");

        fromMethod.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("HOST BOOM");
        fromOperator.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("HOST BOOM");
    }

    [Fact]
    public void AHostOperatorsFailureArrivesRatherThanItsCause()
    {
        var fromMethod = Evaluate("a.Subtract(b)");
        var fromOperator = Evaluate("a - b");

        fromMethod.Should().BeOfType<NotSupportedException>().Which.Message.Should().Be("metres do not subtract");
        fromOperator.Should().BeOfType<NotSupportedException>().Which.Message.Should().Be("metres do not subtract");
    }

    [Fact]
    public void TheSameHoldsWhenTheHostAsksForClrExceptionsToBeCaught()
    {
        // The handler names the host exception, which is how an embedder writes one. Wrapping
        // e.InnerException handed it a JavaScriptErrorWrapperException instead - the engine's own carrier
        // for an error value, which no host handler recognizes - so the handler declined and that wrapper
        // left Evaluate as the exception the embedder saw, invisible to the script's own catch as well.
        static Engine NewEngine()
        {
            var engine = new Engine(options =>
            {
                options.Interop.AllowOperatorOverloading = true;
                options.CatchClrExceptions(e => e is InvalidOperationException);
            });
            engine.SetValue("a", new Meter(1));
            engine.SetValue("b", new Meter(2));
            return engine;
        }

        var fromMethod = Invoking(() => NewEngine().Evaluate("a.Add(b)")).Should().Throw<JavaScriptException>().Which;
        var fromOperator = Invoking(() => NewEngine().Evaluate("a + b")).Should().Throw<JavaScriptException>().Which;

        fromMethod.Message.Should().Be("HOST BOOM");
        fromOperator.Message.Should().Be("HOST BOOM");

        JintException.TryGetClrException(fromMethod, out var fromMethodClr).Should().BeTrue();
        JintException.TryGetClrException(fromOperator, out var fromOperatorClr).Should().BeTrue();
        fromMethodClr.Should().BeOfType<InvalidOperationException>();
        fromOperatorClr.Should().BeOfType<InvalidOperationException>();

        // and the script's own catch reaches it, which it did not while the wrapper was escaping
        NewEngine().Evaluate("try { a + b } catch (e) { e.message }").AsString().Should().Be("HOST BOOM");
    }
}
