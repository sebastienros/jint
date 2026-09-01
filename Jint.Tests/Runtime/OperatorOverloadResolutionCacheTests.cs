#nullable enable

using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Tests.Runtime;

/// <summary>
/// What an engine is allowed to remember about an operator overload. The public-interface suite pins the
/// answers two engines, and two argument values, must not take from each other; this pins the mechanism that
/// keeps them apart, since "every answer was right" is also what a cache that was never consulted looks like.
/// </summary>
public class OperatorOverloadResolutionCacheTests
{
    public sealed class Amount
    {
        public static string operator +(Amount left, Amount right) => "operator";
    }

    /// <summary>Declares no operator at all, so every pair it takes part in has an empty candidate set.</summary>
    public sealed class Plain
    {
        public override string ToString() => "plain";
    }

    /// <summary>Carries two <c>+</c> overloads only an argument's value can choose between.</summary>
    public sealed class Ranged
    {
        public static string operator +(Ranged left, byte right) => "byte:" + right;
        public static string operator +(Ranged left, object right) => "object:" + right;
    }

    /// <summary>Behaves exactly like the stock converter but is not it, which is what installs a filter.</summary>
    private sealed class WrappingTypeConverter : DefaultTypeConverter
    {
        public WrappingTypeConverter(Engine engine) : base(engine)
        {
        }
    }

    private static Engine NewEngine(Action<Options>? configure = null) => new(options =>
    {
        options.Interop.AllowOperatorOverloading = true;
        configure?.Invoke(options);
    });

    private static MethodDescriptor[]? CandidatesFor(Type left, Type right)
    {
        var key = new JintBinaryExpression.OperatorKey("op_Addition", left, right);
        return JintBinaryExpression._operatorCandidates.TryGetValue(key, out var candidates) ? candidates : null;
    }

    [Test]
    public void WhatIsRememberedIsTheCandidateSetAndNotTheSelection()
    {
        var engine = NewEngine();
        engine.SetValue("m", new Ranged());

        engine.Evaluate("m + 5").AsString().Should().Be("byte:5");

        CandidatesFor(typeof(Ranged), typeof(double)).Should().NotBeNull().And.HaveCount(2,
            "the overload this value did not select has to stay available to the next evaluation");
    }

    [Test]
    public void TheSetIsScannedOnceAndSharedByEveryEngine()
    {
        var stock = NewEngine();
        stock.SetValue("a", new Amount());
        stock.SetValue("b", new Amount());
        stock.Evaluate("a + b");

        var first = CandidatesFor(typeof(Amount), typeof(Amount));

        // A converter of its own used to give an engine a resolution table of its own, because a resolution
        // was scored against that converter. A candidate set is not, so there is one table again.
        var withConverter = NewEngine(options => options.SetTypeConverter(e => new WrappingTypeConverter(e)));
        withConverter.SetValue("a", new Amount());
        withConverter.SetValue("b", new Amount());
        withConverter.Evaluate("a + b");

        CandidatesFor(typeof(Amount), typeof(Amount)).Should().BeSameAs(first,
            "a reflection scan of two types is the same answer for every engine in the process");
    }

    [Test]
    public void APairWithNoOperatorIsRememberedAsHavingNone()
    {
        var engine = NewEngine();
        engine.SetValue("p", new Plain());

        engine.Evaluate("p + 'x'").AsString().Should().Be("plainx");

        CandidatesFor(typeof(Plain), typeof(string)).Should().NotBeNull().And.BeEmpty(
            "an empty set is what lets the next evaluation of the same pair leave without allocating anything");
    }

    [Test]
    public void ACandidateIsListedOnceEvenWhenBothOperandsDeclareIt()
    {
        var engine = NewEngine();
        engine.SetValue("a", new Amount());
        engine.SetValue("b", new Amount());
        engine.Evaluate("a + b");

        CandidatesFor(typeof(Amount), typeof(Amount)).Should().NotBeNull().And.HaveCount(1,
            "both operand types are scanned, and `T + T` finds the same operator on each");
    }
}
