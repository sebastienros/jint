#nullable enable

using System.Collections.Generic;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="ReferenceResolverInterests"/> is a subscription filter: the engine simply does not call the
/// resolver for situations it did not subscribe to, and keeps the interpreter fast paths those situations
/// would otherwise disable. These tests pin, per flag, both which callbacks fire and what the script sees.
/// </summary>
public class ReferenceResolverInterestsTests
{
    private static bool IsNullish(JsValue value) => value.IsNull() || value.IsUndefined();

    /// <summary>
    /// Counts every callback and records the kind of base each property reference had, so a test can assert
    /// "not consulted" as directly as "consulted". Behaviour-wise it is the classic null-propagation helper:
    /// a null/undefined base yields <c>undefined</c> instead of throwing.
    /// </summary>
    private sealed class CountingResolver : IReferenceResolver
    {
        public int UnresolvableCalls;
        public int CallableCalls;
        public int CoercibleCalls;
        public readonly List<string> PropertyBases = new();

        public int PropertyCalls => PropertyBases.Count;

        public void Reset()
        {
            UnresolvableCalls = 0;
            CallableCalls = 0;
            CoercibleCalls = 0;
            PropertyBases.Clear();
        }

        public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
        {
            UnresolvableCalls++;
            value = JsValue.Undefined;
            return true;
        }

        public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value)
        {
            var baseValue = reference.Base;
            PropertyBases.Add(Describe(baseValue));

            if (IsNullish(baseValue))
            {
                value = JsValue.Undefined;
                return true;
            }

            return false;
        }

        public bool TryGetCallable(Engine engine, object callee, out JsValue value)
        {
            CallableCalls++;
            value = new ClrFunction(engine, "substitute", (_, _) => "substituted");
            return true;
        }

        public bool CheckCoercible(JsValue value)
        {
            CoercibleCalls++;
            return true;
        }

        private static string Describe(JsValue value)
        {
            if (IsNullish(value))
            {
                return "nullish";
            }

            return value.IsObject() ? "object" : "primitive";
        }
    }

    private static (Engine Engine, CountingResolver Resolver) CreateEngine(ReferenceResolverInterests? interests)
    {
        var resolver = new CountingResolver();
        var engine = new Engine(options =>
        {
            if (interests is null)
            {
                options.SetReferenceResolver(resolver);
            }
            else
            {
                options.SetReferenceResolver(resolver, interests.Value);
            }
        });

        engine.Evaluate("var obj = { x: 1, s: 'text', arr: [10, 20, 30], m: function () { return 'm'; }, notCallable: 7 };");
        resolver.Reset();
        return (engine, resolver);
    }

    // ---------------------------------------------------------------------------------------------
    // The interest-free overload keeps the pre-filter behaviour exactly.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void InterestFreeOverloadGetsEveryInterest()
    {
        var options = new Options();
        options.SetReferenceResolver(new CountingResolver(), ReferenceResolverInterests.NullishPropertyBase);
        options.ReferenceResolverInterests.Should().Be(ReferenceResolverInterests.NullishPropertyBase);

        // re-registering through the interest-free overload must not silently inherit the narrow set
        options.SetReferenceResolver(new CountingResolver());
        options.ReferenceResolverInterests.Should().Be(ReferenceResolverInterests.All);
    }

    [Test]
    public void UnfilteredResolverBehavesAsBefore()
    {
        var (engine, resolver) = CreateEngine(interests: null);

        // null propagation through a chain of missing properties
        engine.Evaluate("obj.missing.deeper.deepest").IsUndefined().Should().BeTrue();
        // unresolvable identifiers resolve to undefined instead of throwing
        engine.Evaluate("typeof neverDeclared").AsString().Should().Be("undefined");
        engine.Evaluate("neverDeclared").IsUndefined().Should().BeTrue();
        // a non-callable callee is substituted
        engine.Evaluate("obj.notCallable()").AsString().Should().Be("substituted");

        resolver.PropertyBases.Should().Contain("object");
        resolver.PropertyBases.Should().Contain("nullish");
        resolver.UnresolvableCalls.Should().BeGreaterThan(0);
        resolver.CallableCalls.Should().Be(1);
    }

    [Test]
    public void NoneMeansTheResolverIsNeverConsulted()
    {
        var (engine, resolver) = CreateEngine(ReferenceResolverInterests.None);

        engine.Evaluate("obj.x").AsNumber().Should().Be(1);
        Invoking(() => engine.Evaluate("obj.missing.deeper")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("neverDeclared")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("obj.notCallable()")).Should().Throw<JavaScriptException>();

        resolver.PropertyCalls.Should().Be(0);
        resolver.UnresolvableCalls.Should().Be(0);
        resolver.CallableCalls.Should().Be(0);
        resolver.CoercibleCalls.Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // NullishPropertyBase only: the documented "not consulted for object bases" case.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void NullishOnlyResolverIsNotConsultedForObjectBases()
    {
        var (engine, resolver) = CreateEngine(ReferenceResolverInterests.NullishPropertyBase);

        // plain member read, repeated so the inline caches engage
        engine.Evaluate("var total = 0; for (var i = 0; i < 20; i++) { total += obj.x; } total").AsNumber().Should().Be(20);
        // computed index read off a dense array
        engine.Evaluate("obj.arr[1]").AsNumber().Should().Be(20);
        // member call
        engine.Evaluate("obj.m()").AsString().Should().Be("m");
        // member write, then read back
        engine.Evaluate("obj.written = 5; obj.written").AsNumber().Should().Be(5);
        // string primitive base
        engine.Evaluate("obj.s.length").AsNumber().Should().Be(4);

        resolver.PropertyCalls.Should().Be(0);
    }

    [Test]
    public void NullishOnlyResolverStillHandlesNullishBases()
    {
        var (engine, resolver) = CreateEngine(ReferenceResolverInterests.NullishPropertyBase);

        engine.Evaluate("obj.missing.deeper").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj.missing.deeper.deepest").IsUndefined().Should().BeTrue();

        resolver.PropertyBases.Should().OnlyContain(kind => kind == "nullish");
        resolver.CoercibleCalls.Should().BeGreaterThan(0);
    }

    [Test]
    public void NullishOnlyResolverHandlesNullishBasesInComputedReads()
    {
        var (engine, resolver) = CreateEngine(ReferenceResolverInterests.NullishPropertyBase);

        engine.Evaluate("var i = 0; obj.missing[i]").IsUndefined().Should().BeTrue();

        resolver.PropertyBases.Should().OnlyContain(kind => kind == "nullish");
    }

    [Test]
    public void NullishOnlyResolverDoesNotAnswerUnresolvableOrCallee()
    {
        var (engine, resolver) = CreateEngine(ReferenceResolverInterests.NullishPropertyBase);

        Invoking(() => engine.Evaluate("neverDeclared")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("obj.notCallable()")).Should().Throw<JavaScriptException>();

        resolver.UnresolvableCalls.Should().Be(0);
        resolver.CallableCalls.Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // ObjectPropertyBase / PrimitivePropertyBase
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void ObjectBaseOnlyResolverSeesObjectBasesAndNotNullishOrPrimitiveOnes()
    {
        var (engine, resolver) = CreateEngine(ReferenceResolverInterests.ObjectPropertyBase);

        engine.Evaluate("obj.x").AsNumber().Should().Be(1);
        resolver.PropertyBases.Should().OnlyContain(kind => kind == "object");

        resolver.Reset();
        engine.Evaluate("obj.s.length").AsNumber().Should().Be(4);
        // the `obj.s` half has an object base; the `.length` half has a primitive one and is skipped
        resolver.PropertyBases.Should().OnlyContain(kind => kind == "object");

        resolver.Reset();
        Invoking(() => engine.Evaluate("obj.missing.deeper")).Should().Throw<JavaScriptException>();
        resolver.PropertyBases.Should().NotContain("nullish");
        resolver.CoercibleCalls.Should().Be(0);
    }

    [Test]
    public void PrimitiveBaseOnlyResolverSeesPrimitiveBasesOnly()
    {
        var (engine, resolver) = CreateEngine(ReferenceResolverInterests.PrimitivePropertyBase);

        engine.Evaluate("obj.x").AsNumber().Should().Be(1);
        resolver.PropertyCalls.Should().Be(0);

        engine.Evaluate("obj.s.length").AsNumber().Should().Be(4);
        resolver.PropertyBases.Should().OnlyContain(kind => kind == "primitive");
    }

    // ---------------------------------------------------------------------------------------------
    // UnresolvableReference / NonCallableCallee
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void UnresolvableOnlyResolverAnswersMissingIdentifiersOnly()
    {
        var (engine, resolver) = CreateEngine(ReferenceResolverInterests.UnresolvableReference);

        engine.Evaluate("neverDeclared").IsUndefined().Should().BeTrue();
        resolver.UnresolvableCalls.Should().BeGreaterThan(0);

        resolver.Reset();
        engine.Evaluate("obj.x").AsNumber().Should().Be(1);
        Invoking(() => engine.Evaluate("obj.missing.deeper")).Should().Throw<JavaScriptException>();
        resolver.PropertyCalls.Should().Be(0);
        resolver.CoercibleCalls.Should().Be(0);
    }

    [Test]
    public void UnresolvableOnlyResolverAnswersCallsToMissingIdentifiers()
    {
        var (engine, resolver) = CreateEngine(ReferenceResolverInterests.UnresolvableReference);

        // the callee itself is unresolvable: the resolver supplies the this-binding, and the substituted
        // value is not callable, so the call still fails - but the resolver was consulted
        Invoking(() => engine.Evaluate("neverDeclared()")).Should().Throw<JavaScriptException>();
        resolver.UnresolvableCalls.Should().BeGreaterThan(0);
        resolver.CallableCalls.Should().Be(0);
    }

    [Test]
    public void CalleeOnlyResolverSubstitutesNonCallableCallees()
    {
        var (engine, resolver) = CreateEngine(ReferenceResolverInterests.NonCallableCallee);

        engine.Evaluate("obj.notCallable()").AsString().Should().Be("substituted");
        resolver.CallableCalls.Should().Be(1);
        resolver.PropertyCalls.Should().Be(0);

        // and a real method still dispatches normally, without consulting anything
        resolver.Reset();
        engine.Evaluate("obj.m()").AsString().Should().Be("m");
        resolver.CallableCalls.Should().Be(0);
    }

    [Test]
    public void CalleeInterestDoesNotDisableTheMemberCallFastPath()
    {
        var (engine, resolver) = CreateEngine(ReferenceResolverInterests.NonCallableCallee);

        engine.Evaluate("var out = ''; for (var i = 0; i < 20; i++) { out = obj.m(); } out").AsString().Should().Be("m");
        resolver.PropertyCalls.Should().Be(0);
        resolver.CallableCalls.Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // Optional chaining short-circuits before any resolver involvement, under every filter.
    // ---------------------------------------------------------------------------------------------

    [TestCase(ReferenceResolverInterests.None)]
    [TestCase(ReferenceResolverInterests.NullishPropertyBase)]
    [TestCase(ReferenceResolverInterests.ObjectPropertyBase)]
    [TestCase(ReferenceResolverInterests.All)]
    public void OptionalChainingShortCircuitsRegardlessOfInterests(ReferenceResolverInterests interests)
    {
        var (engine, _) = CreateEngine(interests);

        engine.Evaluate("obj.missing?.deeper").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj?.x").AsNumber().Should().Be(1);
        engine.Evaluate("obj.missing?.[0]").IsUndefined().Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------------
    // Values stay correct under every filter — the filter must never change what a read produces for
    // situations outside it.
    // ---------------------------------------------------------------------------------------------

    [TestCase(ReferenceResolverInterests.None)]
    [TestCase(ReferenceResolverInterests.NullishPropertyBase)]
    [TestCase(ReferenceResolverInterests.ObjectPropertyBase)]
    [TestCase(ReferenceResolverInterests.PrimitivePropertyBase)]
    [TestCase(ReferenceResolverInterests.UnresolvableReference)]
    [TestCase(ReferenceResolverInterests.NonCallableCallee)]
    [TestCase(ReferenceResolverInterests.All)]
    public void OrdinaryReadsProduceTheSameValuesUnderEveryFilter(ReferenceResolverInterests interests)
    {
        var (engine, _) = CreateEngine(interests);

        engine.Evaluate("obj.x").AsNumber().Should().Be(1);
        engine.Evaluate("obj.arr[2]").AsNumber().Should().Be(30);
        engine.Evaluate("obj.arr.length").AsNumber().Should().Be(3);
        engine.Evaluate("obj.s.length").AsNumber().Should().Be(4);
        engine.Evaluate("obj.s.toUpperCase()").AsString().Should().Be("TEXT");
        engine.Evaluate("obj.m()").AsString().Should().Be("m");
        engine.Evaluate("obj.assigned = 42; obj.assigned").AsNumber().Should().Be(42);
        engine.Evaluate("var sum = 0; for (var i = 0; i < obj.arr.length; i++) { sum += obj.arr[i]; } sum")
            .AsNumber().Should().Be(60);
    }
}
