#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="NullPropagatingReferenceResolver"/> is the shipped reference implementation of
/// <see cref="IReferenceResolver"/>'s own documented use case: a property read off a <c>null</c>/<c>undefined</c>
/// base yields that base instead of throwing. The engine recognizes the singleton and serves the propagation
/// inline, so these tests do double duty — they pin the behaviour, and they pin that the inline lane and an
/// equivalent hand-written resolver are indistinguishable to a script.
/// </summary>
public class NullPropagatingResolverTests
{
    /// <summary>
    /// A hand-written resolver with exactly the shipped one's behaviour, used as the control the inline lane
    /// must agree with. It only reaches the public surface, so it is what a third party would actually write.
    /// </summary>
    private sealed class HandWrittenNullPropagatingResolver : IReferenceResolver
    {
        public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
        {
            value = JsValue.Undefined;
            return false;
        }

        public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value)
        {
            return value.IsNull() || value.IsUndefined();
        }

        public bool TryGetCallable(Engine engine, object callee, out JsValue value)
        {
            value = JsValue.Undefined;
            return false;
        }

        public bool CheckCoercible(JsValue value)
        {
            return value.IsNull() || value.IsUndefined();
        }
    }

    public enum Setup
    {
        /// <summary>No resolver at all — the spec-conforming control.</summary>
        Default,

        /// <summary>The recognized singleton, registered with the interest it actually uses.</summary>
        InlineNullishInterest,

        /// <summary>The recognized singleton, registered through the interest-free overload (i.e. <c>All</c>).</summary>
        InlineDefaultInterests,

        /// <summary>A hand-written equivalent, registered with the same narrow interest.</summary>
        HandWrittenNullishInterest,

        /// <summary>A hand-written equivalent, registered through the interest-free overload.</summary>
        HandWrittenDefaultInterests,
    }

    /// <summary>Every configuration that must propagate — the equivalence class the matrix tests sweep.</summary>
    private static readonly Setup[] PropagatingSetups =
    [
        Setup.InlineNullishInterest,
        Setup.InlineDefaultInterests,
        Setup.HandWrittenNullishInterest,
        Setup.HandWrittenDefaultInterests,
    ];

    private const string Prelude = """
        var obj = { x: 1, s: 'text', arr: [10, 20, 30], inner: {}, nulled: null, m: function () { return 'm'; } };
        var absent;
        var nothing = null;
        var key = 'deeper';
        """;

    private static Engine CreateEngine(Setup setup, bool strict = false)
    {
        var engine = new Engine(options =>
        {
            options.Strict = strict;

            switch (setup)
            {
                case Setup.Default:
                    break;
                case Setup.InlineNullishInterest:
                    options.SetReferencesResolver(NullPropagatingReferenceResolver.Instance, ReferenceResolverInterests.NullishPropertyBase);
                    break;
                case Setup.InlineDefaultInterests:
                    options.SetReferencesResolver(NullPropagatingReferenceResolver.Instance);
                    break;
                case Setup.HandWrittenNullishInterest:
                    options.SetReferencesResolver(new HandWrittenNullPropagatingResolver(), ReferenceResolverInterests.NullishPropertyBase);
                    break;
                case Setup.HandWrittenDefaultInterests:
                    options.SetReferencesResolver(new HandWrittenNullPropagatingResolver());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(setup), setup, null);
            }
        });

        engine.Evaluate(strict ? "'use strict';" + Prelude : Prelude);
        return engine;
    }

    /// <summary>
    /// Stringifies an outcome — value <i>or</i> error — so two engines can be compared on exactly what a
    /// script observes. <c>null</c> and <c>undefined</c> have to stay distinguishable from each other and from
    /// the strings of the same name, which is the whole point of the resolver's contract.
    /// </summary>
    private static string Outcome(Engine engine, string script)
    {
        try
        {
            return Describe(engine.Evaluate(script));
        }
        catch (JavaScriptException ex)
        {
            return "throws " + ex.Message;
        }
    }

    private static string Describe(JsValue value)
    {
        if (value.IsNull())
        {
            return "null";
        }

        if (value.IsUndefined())
        {
            return "undefined";
        }

        if (value.IsString())
        {
            return "string:" + value.AsString();
        }

        if (value.IsNumber())
        {
            return "number:" + value.AsNumber().ToString(CultureInfo.InvariantCulture);
        }

        if (value.IsBoolean())
        {
            return "boolean:" + value.AsBoolean();
        }

        return "other:" + value;
    }

    // ---------------------------------------------------------------------------------------------
    // The control: without a resolver every one of these scripts throws. The propagation tests below
    // are new capability, so this is what pins that they are actually testing something.
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("absent.x")]
    [InlineData("nothing.x")]
    [InlineData("obj.missing.deeper")]
    [InlineData("obj.nulled.deeper")]
    [InlineData("obj.missing.a.b.c")]
    [InlineData("obj.missing[key]")]
    [InlineData("obj.nulled[0]")]
    [InlineData("obj.inner.missing.deeper")]
    [InlineData("typeof obj.missing.deeper")]
    public void WithoutAResolverEveryNullishReadThrows(string script)
    {
        var engine = CreateEngine(Setup.Default);

        // two wordings, because two sites throw: the member-read lane's CheckObjectCoercible says
        // "Cannot read properties of undefined (reading 'x')" and Engine.GetValue's property-reference
        // branch — which is what `typeof` reaches — says "Cannot read property 'x' of undefined". Both are
        // sites this feature has to serve a value at instead.
        Invoking(() => engine.Evaluate(script))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Cannot read propert*");
    }

    [Fact]
    public void DefaultEngineIsCompletelyUnaffected()
    {
        var engine = CreateEngine(Setup.Default);

        engine.Evaluate("obj.x").AsNumber().Should().Be(1);
        engine.Evaluate("obj.s.length").AsNumber().Should().Be(4);
        engine.Evaluate("obj.arr[1]").AsNumber().Should().Be(20);
        engine.Evaluate("obj.m()").AsString().Should().Be("m");
        engine.Evaluate("obj.missing?.deeper").IsUndefined().Should().BeTrue();
        Invoking(() => engine.Evaluate("obj.missing.deeper")).Should().Throw<JavaScriptException>();
    }

    // ---------------------------------------------------------------------------------------------
    // The contract: a nullish base is the result of the read, so undefined stays undefined and null
    // stays null.
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Propagating))]
    public void AnUndefinedBaseYieldsUndefined(Setup setup)
    {
        var engine = CreateEngine(setup);

        engine.Evaluate("absent.x").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj.missing.deeper").IsUndefined().Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void ANullBaseYieldsNull(Setup setup)
    {
        var engine = CreateEngine(setup);

        engine.Evaluate("nothing.x").Should().Be(JsValue.Null);
        engine.Evaluate("obj.nulled.deeper").Should().Be(JsValue.Null);
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void PropagationSurvivesAWholeChain(Setup setup)
    {
        var engine = CreateEngine(setup);

        // every link after the first nullish one keeps yielding that same nullish value
        engine.Evaluate("obj.missing.a.b.c").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj.nulled.a.b.c").Should().Be(JsValue.Null);
        engine.Evaluate("absent.a.b.c.d").IsUndefined().Should().BeTrue();
        engine.Evaluate("nothing.a.b.c.d").Should().Be(JsValue.Null);
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void AnIntermediateNullishLinkPropagatesTheRest(Setup setup)
    {
        var engine = CreateEngine(setup);

        // obj.inner exists and is an object; obj.inner.missing is where the chain goes nullish
        engine.Evaluate("obj.inner.missing.deeper").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj.arr.missing.deeper").IsUndefined().Should().BeTrue();
        // a real value after a real value is still just read
        engine.Evaluate("obj.inner.missing !== undefined").AsBoolean().Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void ComputedKeysPropagateToo(Setup setup)
    {
        var engine = CreateEngine(setup);

        engine.Evaluate("obj.missing[key]").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj.missing['literal']").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj.missing[0]").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj.nulled[key]").Should().Be(JsValue.Null);
        engine.Evaluate("obj.nulled[0]").Should().Be(JsValue.Null);
        engine.Evaluate("var i = 0; obj.missing[i][i]").IsUndefined().Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void TypeofSeesThePropagatedValue(Setup setup)
    {
        var engine = CreateEngine(setup);

        engine.Evaluate("typeof obj.missing.deeper").AsString().Should().Be("undefined");
        engine.Evaluate("typeof obj.nulled.deeper").AsString().Should().Be("object");
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void PropagationReachesLanesThatSkipTheMemberReadFastPath(Setup setup)
    {
        var engine = CreateEngine(setup);

        // generator body: a suspendable context never takes the non-computed read fast lane
        engine.Evaluate("function* g() { yield obj.missing.deeper; } g().next().value")
            .IsUndefined().Should().BeTrue();
        engine.Evaluate("function* h() { yield obj.nulled.deeper; } h().next().value")
            .Should().Be(JsValue.Null);

        // repeated reads, so whatever inline caches exist have engaged by the end
        engine.Evaluate("var last; for (var i = 0; i < 20; i++) { last = obj.missing.deeper; } typeof last")
            .AsString().Should().Be("undefined");
        engine.Evaluate("var last2; for (var i = 0; i < 20; i++) { last2 = obj.nulled.deeper; } last2")
            .Should().Be(JsValue.Null);

        // through a function argument, which is how the classic null-propagation host hits it
        engine.Evaluate("function read(a) { return a.Name; } read(null)").Should().Be(JsValue.Null);
        engine.Evaluate("function read2(a) { return a.Name; } read2(undefined)").IsUndefined().Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------------
    // The boundaries: reads propagate, everything else keeps standard behaviour.
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Propagating))]
    public void CallsOnANullishBaseStillThrowTypeError(Setup setup)
    {
        var engine = CreateEngine(setup);

        Invoking(() => engine.Evaluate("obj.missing.foo()"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Property 'foo' of object is not a function");

        Invoking(() => engine.Evaluate("obj.nulled.foo()"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Property 'foo' of object is not a function");

        Invoking(() => engine.Evaluate("absent.foo()"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Property 'foo' of object is not a function");
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void WritesAreUnaffected(Setup setup)
    {
        var engine = CreateEngine(setup);

        // an ordinary write still works
        engine.Evaluate("obj.written = 5; obj.written").AsNumber().Should().Be(5);

        // a write through a nullish base still throws, exactly as it does with no resolver
        Invoking(() => engine.Evaluate("obj.missing.deeper = 1")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("nothing.x = 1")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("obj.missing.deeper++")).Should().Throw<JavaScriptException>();
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void OtherNullishOperationsKeepThrowing(Setup setup)
    {
        var engine = CreateEngine(setup);

        Invoking(() => engine.Evaluate("var { a } = null; a")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("var [b] = null; b")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("[...null]")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("''.trim.call(null)")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("Object.keys(null)")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("'x' in obj.missing")).Should().Throw<JavaScriptException>();
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void UnresolvableIdentifiersStillThrow(Setup setup)
    {
        var engine = CreateEngine(setup);

        // the shipped resolver declines TryUnresolvableReference; propagation is about property chains
        Invoking(() => engine.Evaluate("neverDeclaredAnywhere")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("neverDeclaredAnywhere.x")).Should().Throw<JavaScriptException>();

        // typeof on an undeclared name is not a read, and still answers per spec
        engine.Evaluate("typeof neverDeclaredAnywhere").AsString().Should().Be("undefined");
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void NonCallableCalleesStillThrow(Setup setup)
    {
        var engine = CreateEngine(setup);

        engine.Evaluate("obj.notCallable = 7;");
        Invoking(() => engine.Evaluate("obj.notCallable()")).Should().Throw<JavaScriptException>();
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void OrdinaryReadsAreUntouched(Setup setup)
    {
        var engine = CreateEngine(setup);

        engine.Evaluate("obj.x").AsNumber().Should().Be(1);
        engine.Evaluate("obj.arr[2]").AsNumber().Should().Be(30);
        engine.Evaluate("obj.arr.length").AsNumber().Should().Be(3);
        engine.Evaluate("obj.s.length").AsNumber().Should().Be(4);
        engine.Evaluate("obj.s.toUpperCase()").AsString().Should().Be("TEXT");
        engine.Evaluate("obj.m()").AsString().Should().Be("m");
        engine.Evaluate("var sum = 0; for (var i = 0; i < obj.arr.length; i++) { sum += obj.arr[i]; } sum")
            .AsNumber().Should().Be(60);
        // a property that legitimately holds null still reads as null, not as a propagation
        engine.Evaluate("obj.nulled").Should().Be(JsValue.Null);
    }

    // ---------------------------------------------------------------------------------------------
    // Optional chaining is unaffected — and stays observably different from propagation for a null
    // base, since `?.` yields undefined where propagation yields null.
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllSetups))]
    public void OptionalChainingBehavesIdenticallyEverywhere(Setup setup)
    {
        var engine = CreateEngine(setup);

        engine.Evaluate("obj.missing?.deeper").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj.nulled?.deeper").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj?.x").AsNumber().Should().Be(1);
        engine.Evaluate("obj.missing?.[0]").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj.missing?.deeper?.deepest").IsUndefined().Should().BeTrue();
        engine.Evaluate("obj.missing?.foo()").IsUndefined().Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void PropagationAndOptionalChainingDisagreeOnANullBase(Setup setup)
    {
        var engine = CreateEngine(setup);

        engine.Evaluate("obj.nulled.deeper").Should().Be(JsValue.Null);
        engine.Evaluate("obj.nulled?.deeper").IsUndefined().Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------------
    // Registration: the interest set is a filter the inline lane obeys exactly as the interface lane does.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TheRecommendedRegistrationPropagates()
    {
        var engine = new Engine(options => options.SetReferencesResolver(
            NullPropagatingReferenceResolver.Instance,
            ReferenceResolverInterests.NullishPropertyBase));

        engine.Evaluate("var o = {}; o.a.b.c").IsUndefined().Should().BeTrue();
    }

    [Fact]
    public void RegistrationWithoutInterestsPropagatesIdentically()
    {
        var narrow = new Engine(options => options.SetReferencesResolver(
            NullPropagatingReferenceResolver.Instance,
            ReferenceResolverInterests.NullishPropertyBase));
        var wide = new Engine(options => options.SetReferencesResolver(NullPropagatingReferenceResolver.Instance));

        foreach (var script in new[] { "var o = {}; o.a.b.c", "var n = null; n.a", "var o2 = { x: 1 }; o2.x" })
        {
            Outcome(wide, script).Should().Be(Outcome(narrow, script));
        }
    }

    [Fact]
    public void WithoutTheNullishInterestTheSingletonDoesNotPropagate()
    {
        // the interest set is a filter, and the inline lane has to honour it: a host that registered the
        // singleton but did not subscribe to nullish bases gets standard behaviour there
        var engine = new Engine(options => options.SetReferencesResolver(
            NullPropagatingReferenceResolver.Instance,
            ReferenceResolverInterests.UnresolvableReference));

        Invoking(() => engine.Evaluate("var o = {}; o.a.b"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Cannot read propert*");
    }

    [Fact]
    public void NoneMeansTheSingletonIsNeverConsulted()
    {
        var engine = new Engine(options => options.SetReferencesResolver(
            NullPropagatingReferenceResolver.Instance,
            ReferenceResolverInterests.None));

        Invoking(() => engine.Evaluate("var o = {}; o.a.b")).Should().Throw<JavaScriptException>();
    }

    [Fact]
    public void TheSingletonIsTheOnlyWayToGetAnInstance()
    {
        NullPropagatingReferenceResolver.Instance.Should().NotBeNull();
        typeof(NullPropagatingReferenceResolver).Should().BeSealed();
        typeof(NullPropagatingReferenceResolver).GetConstructors().Should().BeEmpty();
    }

    [Fact]
    public void TheResolverAnswersItsInterfaceAsDocumented()
    {
        var engine = new Engine();
        var resolver = (IReferenceResolver) NullPropagatingReferenceResolver.Instance;

        // TryPropertyReference: the value arrives bound to the base, and a nullish base is its own result
        JsValue value = JsValue.Null;
        resolver.TryPropertyReference(engine, null!, ref value).Should().BeTrue();
        value.Should().Be(JsValue.Null);

        value = JsValue.Undefined;
        resolver.TryPropertyReference(engine, null!, ref value).Should().BeTrue();
        value.IsUndefined().Should().BeTrue();

        value = new JsString("text");
        resolver.TryPropertyReference(engine, null!, ref value).Should().BeFalse();

        // the other three decline
        resolver.TryUnresolvableReference(engine, null!, out var unresolvable).Should().BeFalse();
        unresolvable.IsUndefined().Should().BeTrue();
        resolver.TryGetCallable(engine, new object(), out var callable).Should().BeFalse();
        callable.IsUndefined().Should().BeTrue();

        // CheckCoercible accepts a nullish base so the read reaches TryPropertyReference
        resolver.CheckCoercible(JsValue.Null).Should().BeTrue();
        resolver.CheckCoercible(JsValue.Undefined).Should().BeTrue();
        resolver.CheckCoercible(new JsString("text")).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------------
    // The core invariant: the inline lane and a hand-written equivalent are indistinguishable.
    // ---------------------------------------------------------------------------------------------

    public static TheoryData<string> EquivalenceScripts()
    {
        var data = new TheoryData<string>();
        foreach (var script in EquivalenceScriptList)
        {
            data.Add(script);
        }

        return data;
    }

    private static readonly string[] EquivalenceScriptList =
    [
        // reads that propagate
        "absent.x",
        "nothing.x",
        "obj.missing.deeper",
        "obj.nulled.deeper",
        "obj.missing.a.b.c.d",
        "obj.nulled.a.b.c.d",
        "obj.inner.missing.deeper",
        "obj.missing[key]",
        "obj.nulled[key]",
        "obj.missing[0]",
        "obj.nulled[0]",
        "obj.arr.missing.deeper",
        "obj.s.missing",
        "obj.x.missing",
        // typeof over the propagated value
        "typeof obj.missing.deeper",
        "typeof obj.nulled.deeper",
        "typeof absent.x",
        "typeof nothing.x",
        // optional chaining alongside propagation
        "obj.missing?.deeper",
        "obj.nulled?.deeper",
        "obj.missing?.deeper.deepest",
        "obj?.x",
        // reads that must not change
        "obj.x",
        "obj.s.length",
        "obj.arr[1]",
        "obj.arr.length",
        "obj.m()",
        "obj.s.toUpperCase()",
        "obj.nulled",
        "obj.inner.missing",
        // errors that must stay errors, with the same message
        "obj.missing.foo()",
        "obj.nulled.foo()",
        "absent.foo()",
        "obj.missing.deeper = 1",
        "nothing.x = 1",
        "obj.missing.deeper++",
        "neverDeclaredAnywhere",
        "neverDeclaredAnywhere.x",
        "var { a } = null; a",
        "[...null]",
        "''.trim.call(null)",
        "Object.keys(null)",
        "'x' in obj.missing",
        "new obj.missing.Ctor()",
        // lanes that bypass the member-read fast path
        "function* g() { yield obj.missing.deeper; } typeof g().next().value",
        "function* h() { yield obj.nulled.deeper; } h().next().value",
        "var last; for (var i = 0; i < 20; i++) { last = obj.missing.deeper; } typeof last",
        "(function (a) { return a.Name; })(null)",
        "(function (a) { return a.Name; })(undefined)",
        "JSON.stringify({ v: obj.missing.deeper })",
        "JSON.stringify({ v: obj.nulled.deeper })",
    ];

    [Theory]
    [MemberData(nameof(EquivalenceScripts))]
    public void EveryPropagatingConfigurationObservesTheSameThing(string script)
    {
        var outcomes = PropagatingSetups
            .Select(setup => new { Setup = setup, Outcome = Outcome(CreateEngine(setup), script) })
            .ToArray();

        var baseline = outcomes[0];
        foreach (var candidate in outcomes.Skip(1))
        {
            candidate.Outcome.Should().Be(
                baseline.Outcome,
                "`{0}` must observe the same thing under {1} as under {2}",
                script,
                candidate.Setup,
                baseline.Setup);
        }
    }

    [Theory]
    [MemberData(nameof(EquivalenceScripts))]
    public void TheSameEquivalenceHoldsInStrictMode(string script)
    {
        var outcomes = PropagatingSetups
            .Select(setup => new { Setup = setup, Outcome = Outcome(CreateEngine(setup, strict: true), script) })
            .ToArray();

        var baseline = outcomes[0];
        foreach (var candidate in outcomes.Skip(1))
        {
            candidate.Outcome.Should().Be(
                baseline.Outcome,
                "`{0}` must observe the same thing under {1} as under {2} in strict mode",
                script,
                candidate.Setup,
                baseline.Setup);
        }
    }

    [Theory]
    [MemberData(nameof(Propagating))]
    public void PropagationWorksTheSameInStrictAndSloppyMode(Setup setup)
    {
        var sloppy = CreateEngine(setup);
        var strict = CreateEngine(setup, strict: true);

        foreach (var script in EquivalenceScriptList)
        {
            var sloppyOutcome = Outcome(sloppy, script);
            var strictOutcome = Outcome(strict, script);

            // strict mode changes some messages (and forbids some scripts outright); the propagating reads
            // are the ones that must agree, so compare only those
            if (sloppyOutcome.StartsWith("throws", StringComparison.Ordinal)
                || strictOutcome.StartsWith("throws", StringComparison.Ordinal))
            {
                continue;
            }

            strictOutcome.Should().Be(sloppyOutcome, "`{0}` under {1}", script, setup);
        }
    }

    // ---------------------------------------------------------------------------------------------

    public static TheoryData<Setup> Propagating()
    {
        var data = new TheoryData<Setup>();
        foreach (var setup in PropagatingSetups)
        {
            data.Add(setup);
        }

        return data;
    }

    public static TheoryData<Setup> AllSetups()
    {
        var data = new TheoryData<Setup>();
        foreach (var setup in (IEnumerable<Setup>) Enum.GetValues(typeof(Setup)))
        {
            data.Add(setup);
        }

        return data;
    }
}
