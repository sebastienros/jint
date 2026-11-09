#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Where, when, how strictly and with which receiver a decorator expression is evaluated and its decorator called,
/// against the decorators spec text, https://github.com/tc39/ecma262/pull/2417 (sebastienros/jint#4403).
/// <para>
/// A class's own decorators are evaluated by the class declaration or expression, in the scope around it, before
/// <see href="https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation">ClassDefinitionEvaluation</see>
/// starts. An element's are evaluated by
/// <see href="https://tc39.es/ecma262/#sec-static-semantics-classelementevaluation">ClassElementEvaluation</see>, so
/// after the heritage, with the class scope and the class's PrivateEnvironment, just before that element's key.
/// DecoratorEvaluation keeps the reference a decorator was read through as its [[Receiver]], and the decorator is
/// called with it as a call would be (<see href="https://tc39.es/ecma262/#sec-evaluatecall">EvaluateCall</see>):
/// <c>obj</c> for <c>@obj.dec</c>, <c>undefined</c> for a plain value.
/// </para>
/// <para>
/// Node has no decorators, so the expected values follow the spec text. One point follows test262 instead:
/// that text calls every part of a class strict mode code
/// (<see href="https://tc39.es/ecma262/#sec-strict-mode-code">strict mode code</see>), but test262 parses
/// <c>@yield() class C {}</c> as sloppy code (<c>decorator-call-expr-identifier-reference-yield.js</c>, noStrict)
/// and so does Jint's parser, so the class's own decorator list runs as the code around it does.
/// </para>
/// </summary>
public class DecoratorEvaluationTests
{
    private const string Log = """
        var order = [];
        function log(name) { order.push(name); return function () {}; }
        function key(name) { order.push(name); return name; }
        class B {}
        """;

    private static JsValue Evaluate(string script) => new Engine().Evaluate(Log + "\n" + script);

    private static string Run(string script) => Evaluate(script).AsString();

    // ------------------------------------------------------------------ evaluation order

    [Test]
    public void ElementDecoratorsAreEvaluatedAfterTheHeritage()
    {
        Run("""
            @(log('d')) class C extends (key('h'), B) { @(log('m')) m() {} }
            order.join();
            """).Should().Be("d,h,m");
    }

    [Test]
    public void EachElementDecoratorListIsEvaluatedJustBeforeItsKey()
    {
        // `static` keeps a computed key from reading as a member access on the decorator expression before it.
        Run("""
            class C {
                @(log('d1')) @(log('d2')) static [key('k1')]() {}
                static [key('k2')] = 1;
                @(log('d3')) static [key('k3')] = 1;
                @(log('d4')) static accessor [key('k4')] = 1;
                @(log('d5')) static get [key('k5')]() { return 1; }
            }
            order.join();
            """).Should().Be("d1,d2,k1,k2,d3,k3,d4,k4,d5,k5");
    }

    [Test]
    public void AThrowingElementDecoratorLeavesTheLaterKeysUnevaluated()
    {
        Run("""
            var result;
            try {
                class C extends (key('h'), B) {
                    static [key('k1')]() {}
                    @(log('d2'), boom()) static [key('k2')]() {}
                }
            } catch (e) {
                result = e.constructor.name;
            }
            result + ':' + order.join();
            """).Should().Be("ReferenceError:h,k1,d2");
    }

    // ------------------------------------------------------------------ scope

    /// <summary>
    /// The class scope holds the class's own binding, uninitialized until the class is done: an element decorator
    /// that reads it throws, where the scope around the class would have answered with the outer binding.
    /// </summary>
    [Test]
    public void AnElementDecoratorSeesTheClassBindingInItsTemporalDeadZone()
    {
        Run("""
            var C = 'outer';
            var result;
            try {
                var D = class C { @(log(typeof C)) m() {} };
                result = 'no throw: ' + order.join();
            } catch (e) {
                result = e.constructor.name;
            }
            result;
            """).Should().Be("ReferenceError");
    }

    /// <summary>
    /// The class's own decorators are evaluated before the class scope exists, so they see the outer binding.
    /// </summary>
    [Test]
    public void AClassDecoratorSeesTheBindingAroundTheClass()
    {
        Run("""
            var C = 'outer';
            var D = @(log(typeof C)) class C {};
            order.join();
            """).Should().Be("string");
    }

    [Test]
    public void AnElementDecoratorResolvesTheClassPrivateNames()
    {
        Run("""
            var check;
            function dec() {}
            class C {
                static #p = 1;
                @(check = o => #p in o, dec) m() {}
            }
            check(C) + ',' + check({});
            """).Should().Be("true,false");
    }

    /// <summary>
    /// A private name an element decorator uses is the class's own when the class declares it, even if an
    /// enclosing class declares one with the same description.
    /// </summary>
    [Test]
    public void AnElementDecoratorResolvesAPrivateNameToItsOwnClassFirst()
    {
        Run("""
            class Outer {
                static #d(value) { return value; }
                static make() {
                    return class Inner {
                        #d;
                        @(Outer.#d) m() {}
                    };
                }
            }
            var result;
            try {
                Outer.make();
                result = 'no throw';
            } catch (e) {
                result = e.constructor.name;
            }
            result;
            """).Should().Be("TypeError");
    }

    // ------------------------------------------------------------------ strictness

    [Test]
    public void AClassDecoratorInSloppyCodeIsSloppy()
    {
        Run("""
            @(leakedByDecorator = function () {}) class C {}
            typeof leakedByDecorator;
            """).Should().Be("function");
    }

    /// <summary>
    /// An arrow function takes the strictness of the code it is created in, so one created by a class decorator in
    /// sloppy code is sloppy.
    /// </summary>
    [Test]
    public void AnArrowCreatedByAClassDecoratorInSloppyCodeIsSloppy()
    {
        Run("""
            @(c => { leakedByArrow = 1; }) class C {}
            typeof leakedByArrow;
            """).Should().Be("number");
    }

    [Test]
    public void AClassDecoratorInStrictCodeIsStrict()
    {
        new Engine().Evaluate("""
            'use strict';
            var result;
            try {
                @(leakedInStrictCode = function () {}) class C {}
                result = typeof leakedInStrictCode;
            } catch (e) {
                result = e.constructor.name;
            }
            result;
            """).AsString().Should().Be("ReferenceError");
    }

    [Test]
    public void AnElementDecoratorInSloppyCodeIsStrict()
    {
        Run("""
            var result;
            try {
                class C { @(leakedByElementDecorator = function () {}) m() {} }
                result = typeof leakedByElementDecorator;
            } catch (e) {
                result = e.constructor.name;
            }
            result;
            """).Should().Be("ReferenceError");
    }

    [Test]
    public void TheHeritageAfterASloppyClassDecoratorIsStrict()
    {
        Run("""
            var result;
            try {
                @(log('d')) class C extends (leakedByHeritage = B) {}
                result = typeof leakedByHeritage;
            } catch (e) {
                result = e.constructor.name;
            }
            result + ':' + order.join();
            """).Should().Be("ReferenceError:d");
    }

    // ------------------------------------------------------------------ receiver

    [Test]
    public void AMemberDecoratorIsCalledWithItsObjectAsReceiver()
    {
        Run("""
            var seen = [];
            var obj = { dec(value, context) { seen.push(context.kind + ':' + (this === obj)); } };
            @obj.dec class C {
                @obj.dec m() {}
                @obj.dec static s() {}
                @obj.dec get g() { return 1; }
                @obj.dec x;
                @obj.dec accessor y;
            }
            seen.sort().join();
            """).Should().Be("accessor:true,class:true,field:true,getter:true,method:true,method:true");
    }

    [Test]
    public void AParenthesizedMemberDecoratorKeepsItsReceiver()
    {
        Run("""
            var seen = [];
            var obj = { dec(value, context) { seen.push(context.kind + ':' + (this === obj)); } };
            @(obj.dec) class C { @(obj['dec']) m() {} }
            seen.join();
            """).Should().Be("method:true,class:true");
    }

    [Test]
    public void ANestedMemberDecoratorIsCalledWithTheInnermostObject()
    {
        Run("""
            var obj = { inner: { dec() { seen = this === obj.inner; } } };
            var seen;
            @obj.inner.dec class C {}
            String(seen);
            """).Should().Be("true");
    }

    [Test]
    public void APrivateMemberDecoratorIsCalledWithItsObjectAsReceiver()
    {
        Run("""
            var seen = [];
            class Outer {
                static #dec(value, context) { seen.push(context.kind + ':' + (this === Outer)); }
                static make() {
                    @Outer.#dec class C { @Outer.#dec m() {} }
                    return C;
                }
            }
            Outer.make();
            seen.join();
            """).Should().Be("method:true,class:true");
    }

    /// <summary>
    /// Only a reference carries a receiver: a call's result, a sequence's or an identifier bound in a declarative
    /// scope is called with <c>undefined</c>, which a strict decorator sees as it is.
    /// </summary>
    [Test]
    public void ADecoratorReadThroughNoPropertyReferenceIsCalledWithUndefined()
    {
        Run("""
            var seen = [];
            function dec(value, context) { 'use strict'; seen.push(context.kind + ':' + (this === undefined)); }
            var obj = { make() { return dec; }, dec };
            @dec @obj.make() @(0, obj.dec) class C { @dec @obj.make() @(0, obj.dec) m() {} }
            seen.join();
            """).Should().Be("method:true,method:true,method:true,class:true,class:true,class:true");
    }

    /// <summary>
    /// An identifier resolved through a <c>with</c> object is a reference whose base environment supplies that
    /// object as the receiver, as it does for a call.
    /// </summary>
    [Test]
    public void ADecoratorResolvedThroughAWithObjectIsCalledWithThatObject()
    {
        Run("""
            var obj = { dec() { seen = this === obj; } };
            var seen;
            with (obj) {
                @dec class C {}
            }
            String(seen);
            """).Should().Be("true");
    }
}
