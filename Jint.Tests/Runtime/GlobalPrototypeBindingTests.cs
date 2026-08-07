#nullable enable
using Jint.Runtime;

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
}
