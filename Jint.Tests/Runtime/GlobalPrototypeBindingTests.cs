#nullable enable
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.Runtime;

/// <summary>
/// Bare-identifier resolution through the global object's prototype chain (issue #2925).
/// GlobalEnvironmentRecord.HasBinding and GetBindingValue are spec'd in terms of [[HasProperty]] /
/// [[Get]] on the global (https://tc39.es/ecma262/#sec-global-environment-records-hasbinding-n), so a
/// name inherited from the global's prototype is a resolvable binding — for reads, <c>typeof</c>,
/// calls and assignment alike — while everything the spec keeps own-only (var/function declaration
/// hoisting, delete, restricted-property checks) must keep ignoring inherited names.
/// </summary>
public class GlobalPrototypeBindingTests
{
    [Fact]
    public void MembersOnTheGlobalPrototypeResolveAsBareIdentifiers()
    {
        var engine = new Engine();
        engine.Execute("Object.setPrototypeOf(globalThis, { answer: 42, fn: function () { return this === globalThis; } });");

        engine.Evaluate("answer").AsNumber().Should().Be(42);
        engine.Evaluate("typeof answer").AsString().Should().Be("number");
        engine.Evaluate("typeof fn").AsString().Should().Be("function");
        engine.Evaluate("fn()").AsBoolean().Should().BeTrue("a sloppy-mode call through a global binding coerces this to globalThis");
    }

    [Fact]
    public void MembersTwoLevelsUpTheChainResolveToo()
    {
        var engine = new Engine();
        engine.Execute("Object.setPrototypeOf(globalThis, Object.create({ deep: 7 }));");

        engine.Evaluate("deep").AsNumber().Should().Be(7);
        engine.Evaluate("typeof deep").AsString().Should().Be("number");
        engine.Evaluate("(function () { return deep; })()").AsNumber().Should().Be(7);
    }

    [Fact]
    public void ObjectPrototypeMembersAreVisibleAtGlobalScope()
    {
        // Object.prototype is the global object's default prototype, so this holds without any setup
        var engine = new Engine();
        engine.Evaluate("typeof toString").AsString().Should().Be("function");
        engine.Evaluate("toString()").AsString().Should().Be("[object Undefined]", "the this value of an environment-record reference is undefined");
    }

    [Fact]
    public void GenuinelyAbsentNamesStillBehaveAsBefore()
    {
        var engine = new Engine();
        engine.Evaluate("typeof missingName").AsString().Should().Be("undefined");

        Invoking(() => engine.Evaluate("missingName")).Should().ThrowExactly<JavaScriptException>().WithMessage("missingName is not defined");
        Invoking(() => engine.Evaluate("'use strict'; missingName")).Should().ThrowExactly<JavaScriptException>().WithMessage("missingName is not defined");
    }

    [Fact]
    public void InheritedAccessorGetsTheGlobalAsReceiver()
    {
        var engine = new Engine();
        engine.Execute("Object.setPrototypeOf(globalThis, { get self() { return this; } });");

        engine.Evaluate("self === globalThis").AsBoolean().Should().BeTrue();
        engine.Evaluate("(function () { return self; })() === globalThis").AsBoolean().Should().BeTrue();

        // and two levels up
        engine.Execute("Object.setPrototypeOf(globalThis, Object.create({ get deepSelf() { return this; } }));");
        engine.Evaluate("deepSelf === globalThis").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void OwnAndDeclaredBindingsShadowInheritedOnes()
    {
        var engine = new Engine();
        engine.Execute("Object.setPrototypeOf(globalThis, { x: 1, v: 1, lx: 1 });");

        engine.Evaluate("globalThis.x = 2; x").AsNumber().Should().Be(2);

        // var hoisting is own-only by spec (CreateGlobalVarBinding), so the declaration shadows with undefined
        engine.Evaluate("(0, eval)('var v; typeof v')").AsString().Should().Be("undefined");

        engine.Evaluate("let lx = 2; lx").AsNumber().Should().Be(2);
    }

    [Fact]
    public void DeleteOfAnInheritedNameSucceedsWithoutTouchingThePrototype()
    {
        var engine = new Engine();
        engine.Execute("Object.setPrototypeOf(globalThis, { d: 1 });");

        engine.Evaluate("delete d").AsBoolean().Should().BeTrue("DeleteBinding is own-only by spec");
        engine.Evaluate("d").AsNumber().Should().Be(1);
    }

    [Fact]
    public void AssignmentToAnInheritedNameFollowsOrdinarySetSemantics()
    {
        var engine = new Engine();

        // inherited setter runs with the global as receiver, and no own property is created
        engine.Execute("Object.setPrototypeOf(globalThis, { set s(v) { this.observed = v; }, get s() { return 'live'; } });");
        engine.Execute("s = 42;");
        engine.Evaluate("observed").AsNumber().Should().Be(42);
        engine.Evaluate("globalThis.hasOwnProperty('s')").AsBoolean().Should().BeFalse();
        engine.Evaluate("s").AsString().Should().Be("live");

        // strict assignment to an inherited writable data property is not a ReferenceError; it shadows
        engine.Execute("Object.setPrototypeOf(globalThis, { w: 1 });");
        engine.Execute("(function () { 'use strict'; w = 5; })();");
        engine.Evaluate("globalThis.hasOwnProperty('w') && w === 5").AsBoolean().Should().BeTrue();

        // inherited non-writable data: sloppy is a silent no-op, strict is a TypeError
        engine.Execute("var proto = Object.defineProperty({}, 'nw', { value: 1, writable: false }); Object.setPrototypeOf(globalThis, proto);");
        engine.Execute("nw = 9;");
        engine.Evaluate("nw").AsNumber().Should().Be(1);
        engine.Evaluate("globalThis.hasOwnProperty('nw')").AsBoolean().Should().BeFalse();
        engine.Evaluate("(function () { 'use strict'; try { nw = 9; return 'no error'; } catch (e) { return e instanceof TypeError ? 'TypeError' : 'other'; } })()").AsString().Should().Be("TypeError");
    }

    private static PropertyFlag GlobalPropertyFlags(Engine engine, string name)
        => engine.Realm.GlobalObject.GetOwnProperty(name).Flags;

    [Fact]
    public void ShadowingAnInheritedNameCreatesAMutableBindingLikeAVarDeclarationDoes()
    {
        // Assigning to a name that resolves on the global's prototype shadows it, and the shadow is a
        // global binding like any other. It is created by ordinary [[Set]] though, which knows nothing
        // about bindings and left off the marker that lets the two stores in GlobalObject write the
        // descriptor's value in place — so every write after the first took the validate-and-apply path,
        // permanently. An eval-scoped `var` produces the same configurable/enumerable/writable property
        // through the binding machinery's own helper, so the two must be indistinguishable.
        var engine = new Engine();
        engine.Execute("Object.setPrototypeOf(globalThis, { shadowed: 1 });");
        engine.Execute("shadowed = 5; (0, eval)('var declared = 5');");

        var shadowed = GlobalPropertyFlags(engine, "shadowed");

        shadowed.Should().HaveFlag(PropertyFlag.MutableBinding);
        shadowed.Should().Be(GlobalPropertyFlags(engine, "declared"));
    }

    [Fact]
    public void TheMutableBindingMarkerDoesNotChangeHowAShadowedGlobalBehaves()
    {
        var engine = new Engine();
        engine.Execute("Object.setPrototypeOf(globalThis, { w: 1 }); w = 5;");

        // the marker is internal: the property describes itself exactly as [[Set]] left it
        engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptor(globalThis, 'w'))").AsString()
            .Should().Be("""{"value":5,"writable":true,"enumerable":true,"configurable":true}""");

        // clearing writable is still honoured — the in-place store the marker enables sits behind the
        // writable check, not in front of it
        engine.Execute("Object.defineProperty(globalThis, 'w', { writable: false });");
        engine.Execute("w = 9;");
        engine.Evaluate("w").AsNumber().Should().Be(5);
        engine.Evaluate("(function () { 'use strict'; try { w = 9; return 'no error'; } catch (e) { return e instanceof TypeError ? 'TypeError' : 'other'; } })()")
            .AsString().Should().Be("TypeError");

        // redefining it as an accessor still routes assignment to the setter
        engine.Execute("Object.defineProperty(globalThis, 'w', { get: function () { return 'got'; }, set: function (v) { globalThis.observed = v; } });");
        engine.Evaluate("w").AsString().Should().Be("got");
        engine.Execute("w = 11;");
        engine.Evaluate("observed").AsNumber().Should().Be(11);

        // and the marker is not a claim that the property cannot be deleted
        engine.Evaluate("delete w").AsBoolean().Should().BeTrue();
        engine.Evaluate("globalThis.hasOwnProperty('w')").AsBoolean().Should().BeFalse();
        engine.Evaluate("w").AsNumber().Should().Be(1, "the inherited property is visible again");
    }

#if NET
    [Fact]
    public void RepeatedWritesToAShadowedGlobalStoreInPlace()
    {
        // A destructuring assignment target has no per-site write cache, so every iteration goes through
        // GlobalObject.SetFromMutableBinding — the store the marker decides. Without it each write built
        // a fresh PropertyDescriptor and a fresh key for ValidateAndApplyPropertyDescriptor to consume.
        const int iterations = 20_000;

        var engine = new Engine();
        engine.Execute("Object.setPrototypeOf(globalThis, { acc: 0 }); acc = 0;");

        var prepared = Engine.PrepareScript($$"""
            var src = [7];
            (function () {
                for (var i = 0; i < {{iterations}}; i++) { [acc] = src; }
            })();
            """);
        engine.Evaluate(prepared);
        engine.Evaluate(prepared);

        var before = GC.GetAllocatedBytesForCurrentThread();
        engine.Evaluate(prepared);
        var perWrite = (double) (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;

        // Array destructuring legitimately allocates an iterator and its result objects per iteration
        // (104 bytes when this was written). The validate-and-apply path added exactly 64 on top of
        // that, every write: one PropertyDescriptor to carry the new value and one JsString key.
        perWrite.Should().BeLessThan(130, $"writing a shadowed global allocated {perWrite} bytes per write");
    }
#endif

    [Fact]
    public void ProtoAssignmentAtGlobalScopeStillRunsTheInheritedSetter()
    {
        // __proto__ lives on Object.prototype, i.e. on the global's prototype chain: the bare
        // assignment must keep routing through the inherited setter and actually swap the prototype
        var engine = new Engine();
        engine.Execute("__proto__ = { marker: 123 };");
        engine.Evaluate("Object.getPrototypeOf(globalThis).marker").AsNumber().Should().Be(123);
        engine.Evaluate("marker").AsNumber().Should().Be(123);
    }

    [Fact]
    public void NoCacheServesStalePrototypeValues()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Object.setPrototypeOf(globalThis, { cs: 1 });
            var r1 = 0;
            for (var i = 0; i < 100; i++) { r1 = cs; }
            Object.getPrototypeOf(globalThis).cs = 2;
            var r2 = cs;
            delete Object.getPrototypeOf(globalThis).cs;
            var t = typeof cs;
            globalThis.cs = 3;
            [r1, r2, t, cs].join(',');
            """).AsString();

        result.Should().Be("1,2,undefined,3");
    }

    [Fact]
    public void DirectEvalResolvesInheritedNames()
    {
        var engine = new Engine();
        engine.Execute("Object.setPrototypeOf(globalThis, { ev: 9 });");
        engine.Evaluate("eval('ev')").AsNumber().Should().Be(9);
    }

#if NET
    // Asking the global's prototype chain about a name needs the name as a JsValue, but the two entry
    // points below hand the global environment record a Key. Rebuilding a JsString from it cost one
    // 32-byte object per operation, and forever: neither shape here ever gains an own property that
    // would end the miss. The iteration count is high enough that 32 bytes apiece is unmistakable.
    private const int InheritedNameProbeIterations = 20_000;

    /// <summary>
    /// Runs <paramref name="source"/> twice to warm the engine's handler trees, then reports what a
    /// third evaluation allocates per loop iteration.
    /// </summary>
    private static double AllocatedBytesPerIteration(Engine engine, string source)
    {
        var prepared = Engine.PrepareScript(source);
        engine.Evaluate(prepared);
        engine.Evaluate(prepared);

        var before = GC.GetAllocatedBytesForCurrentThread();
        engine.Evaluate(prepared);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        return (double) allocated / InheritedNameProbeIterations;
    }

    [Fact]
    public void ReadingAnInheritedNameDoesNotBuildItsNameStringPerRead()
    {
        // typeof and a call both resolve the identifier to a Reference and read it back through
        // Engine.GetValue, which is where the Key-only GetBindingValue overload is entered; the
        // reference already carries the name as a JsString. (A plain read never had the problem: it
        // goes through TryGetBinding, whose BindingName carries the same string.)
        var engine = new Engine();
        engine.Execute("Object.setPrototypeOf(globalThis, { pg: 1, pf: function () { return 1; } });");

        var typeOfBytes = AllocatedBytesPerIteration(engine, $$"""
            (function () {
                var t;
                for (var i = 0; i < {{InheritedNameProbeIterations}}; i++) { t = typeof pg; }
                return t;
            })();
            """);

        var callBytes = AllocatedBytesPerIteration(engine, $$"""
            (function () {
                var t;
                for (var i = 0; i < {{InheritedNameProbeIterations}}; i++) { t = pf(); }
                return t;
            })();
            """);

        // Both loops allocate nothing else at all, so the whole 32 bytes was the discarded name.
        typeOfBytes.Should().BeLessThan(4, $"typeof of an inherited global allocated {typeOfBytes} bytes per read");
        callBytes.Should().BeLessThan(4, $"calling an inherited global allocated {callBytes} bytes per call");
    }

    [Fact]
    public void ProbingAnInheritedNameDoesNotBuildItsNameStringPerProbe()
    {
        // A destructuring assignment target goes through ResolveBinding, which walks the chain with a
        // Key and ends at the global environment; its own-property miss falls through to
        // [[HasProperty]] on the prototype. An inherited accessor is the shape that keeps missing —
        // the assignment runs the setter instead of shadowing, so no own property ever appears.
        var engine = new Engine();
        engine.Execute("Object.setPrototypeOf(globalThis, { set st(v) { }, get st() { return 1; } });");

        var bytes = AllocatedBytesPerIteration(engine, $$"""
            var src = [7];
            (function () {
                for (var i = 0; i < {{InheritedNameProbeIterations}}; i++) { [st] = src; }
            })();
            """);

        // Array destructuring legitimately allocates an iterator and its result objects per iteration
        // (168 bytes when this was written), so the budget is that plus headroom and still well under
        // the 200 bytes the extra name cost.
        bytes.Should().BeLessThan(184, $"destructuring into an inherited accessor allocated {bytes} bytes per assignment");
    }
#endif
}
