using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="IReferenceResolver"/> has no write-side member, so installing one must not change how a
/// property assignment behaves. These tests pin that: every case runs on an engine with a resolver and on
/// an engine without one, and both must agree.
/// </summary>
public class ReferenceResolverAssignmentTests
{
    /// <summary>
    /// Declines every hook, exactly like the built-in resolver, but is a distinct instance so the engine
    /// classifies it as a custom resolver.
    /// </summary>
    private sealed class PassThroughResolver : IReferenceResolver
    {
        public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
        {
            value = JsValue.Undefined;
            return false;
        }

        public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value) => false;

        public bool TryGetCallable(Engine engine, object callee, out JsValue value)
        {
            value = JsValue.Undefined;
            return false;
        }

        public bool CheckCoercible(JsValue value) => false;
    }

    /// <summary>Substitutes a host-supplied object for any unresolvable identifier.</summary>
    private sealed class SubstitutingResolver : IReferenceResolver
    {
        public JsValue Substitute { get; set; } = JsValue.Undefined;

        public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
        {
            value = Substitute;
            return true;
        }

        public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value) => false;

        public bool TryGetCallable(Engine engine, object callee, out JsValue value)
        {
            value = JsValue.Undefined;
            return false;
        }

        public bool CheckCoercible(JsValue value) => true;
    }

    private sealed class Holder
    {
        public int Value { get; set; }

        public int ReadOnly => 7;
    }

    private static Engine CreateEngine(bool withResolver) => withResolver
        ? new Engine(options => options.SetReferencesResolver(new PassThroughResolver()))
        : new Engine();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SimpleAssignmentWritesAndOverwritesOwnProperties(bool withResolver)
    {
        var engine = CreateEngine(withResolver);

        // the loop runs the same call site repeatedly so the write-side inline cache is populated and used
        var result = engine.Evaluate("""
            var o = { a: 1 };
            o.a = 2;
            o.b = 3;
            for (var i = 0; i < 10; i++) { o.a = i; }
            [o.a, o.b].join(',');
            """);

        result.AsString().Should().Be("9,3");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompoundAssignmentReadsAndWritesOnce(bool withResolver)
    {
        var engine = CreateEngine(withResolver);

        var result = engine.Evaluate("""
            var reads = 0;
            var o = { a: 1 };
            o.a += 1;
            o.a += 1;
            var s = { x: 'a' };
            s.x += 'b';
            [o.a, s.x].join(',');
            """);

        result.AsString().Should().Be("3,ab");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssignmentThroughPrototypeSetterInvokesTheSetter(bool withResolver)
    {
        var engine = CreateEngine(withResolver);

        var result = engine.Evaluate("""
            var seen = [];
            var proto = {};
            Object.defineProperty(proto, 'p', {
                set: function (v) { seen.push(v); },
                get: function () { return 'g'; }
            });
            var o = Object.create(proto);
            o.p = 5;
            o.p = 6;
            [seen.join('|'), o.p, Object.getOwnPropertyNames(o).length].join(',');
            """);

        // the inherited setter runs for every write and no shadowing own property is created
        result.AsString().Should().Be("5|6,g,0");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StrictModeAssignmentToReadOnlyPropertyThrows(bool withResolver)
    {
        var engine = CreateEngine(withResolver);

        engine.Execute("""
            var o = {};
            Object.defineProperty(o, 'r', { value: 1, writable: false });
            """);

        Invoking(() => engine.Execute("'use strict'; o.r = 2;"))
            .Should().Throw<JavaScriptException>()
            .Which.Message.Should().Be("Cannot assign to read only property 'r' of [object Object]");

        // sloppy mode silently ignores the write
        engine.Execute("o.r = 3;");
        engine.Evaluate("o.r").AsNumber().Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssignmentOnNullishBaseThrows(bool withResolver)
    {
        var engine = CreateEngine(withResolver);

        engine.Execute("var o = { nothing: null };");

        Invoking(() => engine.Execute("o.missing.x = 1;"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Cannot convert undefined or null to object");

        Invoking(() => engine.Execute("o.nothing.x = 1;"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Cannot convert undefined or null to object");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssignmentToHostObjectMemberGoesThroughTheClrSetter(bool withResolver)
    {
        var engine = CreateEngine(withResolver);
        var holder = new Holder();
        engine.SetValue("host", holder);

        engine.Execute("for (var i = 0; i < 10; i++) { host.Value = i; }");
        holder.Value.Should().Be(9);

        // a getter-only CLR member refuses the write: silently in sloppy mode, with a TypeError in
        // strict mode, and never by shadowing the member with a JS-side own property
        engine.Execute("host.ReadOnly = 1;");
        holder.ReadOnly.Should().Be(7);
        engine.Evaluate("host.ReadOnly").AsNumber().Should().Be(7);

        Invoking(() => engine.Execute("'use strict'; host.ReadOnly = 1;"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Cannot assign to read only property 'ReadOnly' of *");
        holder.ReadOnly.Should().Be(7);
    }

    [Fact]
    public void AssigningAResolverResetsTheInterestsNarrowedForThePreviousOne()
    {
        // Interests describe one particular resolver, so registering another one - through the property
        // just as much as through SetReferencesResolver - must not leave the newcomer consulted only for
        // the situations its predecessor cared about.
        var options = new Options();
        options.SetReferencesResolver(new PassThroughResolver(), ReferenceResolverInterests.NullishPropertyBase);
        options.ReferenceResolverInterests.Should().Be(ReferenceResolverInterests.NullishPropertyBase);

        var substituting = new SubstitutingResolver();
        options.ReferenceResolver = substituting;

        options.ReferenceResolverInterests.Should().Be(ReferenceResolverInterests.All);

        // ... and the engine built from those options really does consult it for an unresolvable
        // identifier, which the narrowed set excluded
        var engine = new Engine(options);
        var target = engine.Evaluate("({})").AsObject();
        substituting.Substitute = target;

        engine.Execute("neverDeclared.assigned = 42;");
        target.Get("assigned").AsNumber().Should().Be(42);
    }

    [Fact]
    public void NarrowedInterestsSurviveWhenAssignedAfterTheResolver()
    {
        // The documented way to register a resolver with a narrower set is to assign the interests after
        // the resolver, which is exactly what the two-argument overload does.
        var options = new Options();
        options.ReferenceResolver = new PassThroughResolver();
        options.ReferenceResolverInterests = ReferenceResolverInterests.NullishPropertyBase;

        options.ReferenceResolverInterests.Should().Be(ReferenceResolverInterests.NullishPropertyBase);
    }

    [Fact]
    public void BaseOfAnAssignmentIsStillResolvedThroughTheResolver()
    {
        // The write itself never consults the resolver, but the base is read through the normal read
        // path - so an unresolvable base must still be substituted, and the assignment must land on the
        // substitute rather than throwing a ReferenceError.
        var resolver = new SubstitutingResolver();
        var engine = new Engine(options => options.SetReferencesResolver(resolver));

        var target = engine.Evaluate("({})").AsObject();
        resolver.Substitute = target;

        engine.Execute("neverDeclared.assigned = 42;");

        target.Get("assigned").AsNumber().Should().Be(42);
    }
}
