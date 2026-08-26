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
        return Caught.Exception(() => engine.Evaluate(source));
    }

    [Test]
    public void AHostOperatorsFailureArrivesWithItsMessage()
    {
        var fromMethod = Evaluate("a.Add(b)");
        var fromOperator = Evaluate("a + b");

        fromMethod.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("HOST BOOM");
        fromOperator.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("HOST BOOM");
    }

    [Test]
    public void AHostOperatorsFailureArrivesRatherThanItsCause()
    {
        var fromMethod = Evaluate("a.Subtract(b)");
        var fromOperator = Evaluate("a - b");

        fromMethod.Should().BeOfType<NotSupportedException>().Which.Message.Should().Be("metres do not subtract");
        fromOperator.Should().BeOfType<NotSupportedException>().Which.Message.Should().Be("metres do not subtract");
    }

    [Test]
    public void TheSameHoldsWhenTheHostAsksForClrExceptionsToBeCaught()
    {
        // CatchClrExceptions builds the JavaScript error from the *meaningful* exception, so it reads the
        // same value the two tests above assert on: the host exception is what TryGetClrException hands
        // back, and what the message is drawn from once the host opts into detailed messages. Wrapping the
        // wrong thing put a placeholder TargetInvocationException in both places.
        var engine = new Engine(options =>
        {
            options.Interop.AllowOperatorOverloading = true;
            options.Interop.ExposeDetailedExceptionMessages = true;
            options.CatchClrExceptions();
        });
        engine.SetValue("a", new Meter(1));
        engine.SetValue("b", new Meter(2));

        var exception = Invoking(() => engine.Evaluate("a + b")).Should().Throw<JavaScriptException>().Which;

        exception.Message.Should().Be("HOST BOOM");
        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeOfType<InvalidOperationException>();

        engine.Evaluate("try { a + b } catch (e) { e.message }").AsString().Should().Be("HOST BOOM");
    }
}
