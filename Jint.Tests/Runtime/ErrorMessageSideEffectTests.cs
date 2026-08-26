namespace Jint.Tests.Runtime;

/// <summary>
/// Building an error message must never run user JavaScript. <c>ObjectInstance.ToString()</c> is the full
/// JavaScript ToString algorithm (<c>ToPrimitive</c> → <c>Get(@@toPrimitive)</c> → <c>Get("toString")</c> →
/// <b>call it</b> → <c>Get(@@toStringTag)</c>), so interpolating a <see cref="Jint.Native.JsValue"/> into a
/// <c>Throw.*</c> message is observable twice over: the extra <c>[[Get]]</c> operations show up in a Proxy
/// log, and a user <c>toString</c> that throws replaces the intended TypeError with whatever it threw.
/// These tests pin that error messages are built from side-effect-free renderings only.
/// </summary>
public class ErrorMessageSideEffectTests
{
    [Test]
    public void SetLikeWithNonCallableHasDoesNotRunUserCodeWhileBuildingTheError()
    {
        var engine = new Engine();

        engine.Execute("""
            var log = [];
            var target = {
                size: 0,
                has: {},
                keys: function () { throw new Error('keys must not be reached'); }
            };
            var setLike = new Proxy(target, {
                get: function (t, pk, r) {
                    log.push(typeof pk === 'symbol' ? 'symbol:' + String(pk.description) : pk);
                    return Reflect.get(t, pk, r);
                }
            });
            var threw = null;
            try { new Set().union(setLike); } catch (e) { threw = e; }
            """);

        engine.Evaluate("threw instanceof TypeError").AsBoolean().Should().BeTrue();

        // GetSetRecord reads exactly size, has, keys and then stops: the failure is that `has` is not
        // callable, and rendering the receiver into that message must add nothing to the log.
        engine.Evaluate("log.join(',')").AsString().Should().Be("size,has");
    }

    [Test]
    public void SetLikeWithNonCallableKeysDoesNotRunUserCodeWhileBuildingTheError()
    {
        var engine = new Engine();

        engine.Execute("""
            var log = [];
            var target = { size: 0, has: function () {}, keys: {} };
            var setLike = new Proxy(target, {
                get: function (t, pk, r) {
                    log.push(typeof pk === 'symbol' ? 'symbol:' + String(pk.description) : pk);
                    return Reflect.get(t, pk, r);
                }
            });
            var threw = null;
            try { new Set().union(setLike); } catch (e) { threw = e; }
            """);

        engine.Evaluate("threw instanceof TypeError").AsBoolean().Should().BeTrue();
        engine.Evaluate("log.join(',')").AsString().Should().Be("size,has,keys");
    }

    [Test]
    public void SetLikeWhoseToStringThrowsStillReportsTheTypeError()
    {
        var engine = new Engine();

        // A user toString that throws must not be able to replace the TypeError with its own exception.
        var value = engine.Evaluate("""
            var setLike = {
                size: 0,
                has: {},
                keys: function () {},
                toString: function () { throw new Error('boom'); }
            };
            try { new Set().union(setLike); return 'no throw'; }
            catch (e) { return e.constructor.name + ':' + e.message; }
            """);

        value.AsString().Should().StartWith("TypeError:");
    }

    [Test]
    public void DateToPrimitiveWithAThrowingToStringHintReportsTypeError()
    {
        var engine = new Engine();

        var value = engine.Evaluate("""
            var hint = { toString: function () { throw new Error('boom'); } };
            try { Date.prototype[Symbol.toPrimitive].call(new Date(), hint); return 'no throw'; }
            catch (e) { return e.constructor.name + ':' + e.message; }
            """);

        value.AsString().Should().StartWith("TypeError:");
    }

    [Test]
    public void DateToPrimitiveWithANonStringHintDoesNotCoerceIt()
    {
        var engine = new Engine();

        // An array hint is not a string, so it is rejected; coercing it for the message would call
        // Array.prototype.join and hide the fact that the argument was never a valid hint.
        var value = engine.Evaluate("""
            var log = [];
            var hint = { toString: function () { log.push('toString'); return 'number'; } };
            try { Date.prototype[Symbol.toPrimitive].call(new Date(), hint); } catch (e) { }
            return log.length;
            """);

        value.AsNumber().Should().Be(0);
    }

    /// <summary>
    /// The same hazard reached far past the two sites that first exposed it. Every row here threw the
    /// user's own <c>Error</c> instead of the engine's <c>TypeError</c> before the sweep.
    /// </summary>
    [TestCase("(function () { return victim; })()();", "callee that is not a reference")]
    [TestCase("[1].flatMap(victim);", "Array.prototype.flatMap mapper")]
    [TestCase("Symbol.keyFor(victim);", "Symbol.keyFor argument")]
    [TestCase("var a = [1]; a.constructor = {}; a.constructor[Symbol.species] = victim; a.filter(function () { return true; });", "ArraySpeciesCreate")]
    [TestCase("Reflect.construct(victim, []);", "Reflect.construct target")]
    [TestCase("Reflect.construct(function () {}, [], victim);", "Reflect.construct newTarget")]
    [TestCase("var t = new Int8Array(4); t.constructor = {}; t.constructor[Symbol.species] = victim; t.slice(0, 1);", "TypedArray species")]
    [TestCase("Object.getOwnPropertyDescriptor(Map.prototype, 'size').get.call(victim);", "Map.prototype.size brand check")]
    [TestCase("Object.getOwnPropertyDescriptor(Set.prototype, 'size').get.call(victim);", "Set.prototype.size brand check")]
    [TestCase("Object.getOwnPropertyDescriptor(ArrayBuffer.prototype, 'byteLength').get.call(victim);", "ArrayBuffer.prototype.byteLength brand check")]
    [TestCase("ArrayBuffer.prototype.slice.call(victim);", "ArrayBuffer.prototype.slice brand check")]
    [TestCase("Object.getOwnPropertyDescriptor(DataView.prototype, 'buffer').get.call(victim);", "DataView.prototype.buffer brand check")]
    [TestCase("DataView.prototype.getInt8.call(victim, 0);", "DataView.prototype.getInt8 brand check")]
    [TestCase("Object.getOwnPropertyDescriptor(Int8Array.prototype.__proto__, 'byteLength').get.call(victim);", "TypedArray.prototype.byteLength brand check")]
    [TestCase("RegExp.prototype.exec.call(victim, 'x');", "RegExp.prototype.exec brand check")]
    [TestCase("new ShadowRealm().evaluate(victim);", "ShadowRealm.prototype.evaluate source text")]
    [TestCase("Object.defineProperty({}, 'p', { get: victim });", "property descriptor getter")]
    [TestCase("Object.defineProperty({}, 'p', { set: victim });", "property descriptor setter")]
    [TestCase("new Promise(victim);", "Promise resolver")]
    [TestCase("var it = {}; it[Symbol.iterator] = function () { return { next: function () { return Object.assign(Object.create(victim), { value: {}, done: false }); } }; }; Math.sumPrecise(it);", "Math.sumPrecise iterator result")]
    [TestCase("victim in 1;", "'in' operator left operand")]
    public void BuildingTheErrorNeverRunsAUserToString(string script, string _)
    {
        var engine = new Engine();

        var result = engine.Evaluate($$"""
            var victim = { toString: function () { throw new Error('BOOM'); } };
            try { {{script}} return 'no throw'; }
            catch (e) { return (e && e.constructor ? e.constructor.name : typeof e) + ':' + (e && e.message); }
            """);

        // Any engine-raised error type is fine; what must never happen is the user's own Error escaping.
        result.AsString().Should().NotBe("Error:BOOM");
    }

    /// <summary>
    /// The strict-mode rows live outside the theory above because <c>'use strict'</c> is only a directive
    /// prologue at the top of a script or function body — inside the theory's <c>try</c> block it is an
    /// ordinary expression statement, the write is silently ignored in sloppy mode and the test would
    /// pass without ever reaching the message.
    /// </summary>
    [TestCase("Object.defineProperty(victim, 'p', { value: 1, writable: false }); victim.p = 2;")]
    [TestCase("Object.defineProperty(victim, 'p', { value: 1, configurable: false }); delete victim.p;")]
    public void AStrictModeRefusalDoesNotRenderTheBaseObject(string script)
    {
        var engine = new Engine();

        var result = engine.Evaluate($$"""
            'use strict';
            var victim = { toString: function () { throw new Error('BOOM'); } };
            var outcome;
            try { {{script}} outcome = 'no throw'; }
            catch (e) { outcome = (e && e.constructor ? e.constructor.name : typeof e) + ':' + (e && e.message); }
            outcome;
            """);

        result.AsString().Should().NotBe("Error:BOOM");
    }

    [Test]
    public void AFailedStrictAssignmentCoercesTheComputedKeyExactlyOnce()
    {
        var engine = new Engine();

        // The message used to quote the pre-coercion referenced name, so ToPropertyKey ran once for
        // the assignment and the interpolation ran the user toString a second time.
        var value = engine.Evaluate("""
            'use strict';
            var count = 0;
            var key = { toString: function () { count++; return 'p'; } };
            var frozen = Object.freeze({ p: 1 });
            try { frozen[key] = 2; } catch (e) { }
            count;
            """);

        value.AsNumber().Should().Be(1);
    }

    /// <summary>
    /// A forward guard rather than a regression test: today <c>Object.defineProperty</c> routes the new
    /// descriptor into ordinary property storage and leaves <c>Function._nameDescriptor</c> holding the
    /// original <see cref="Jint.Native.JsString"/>, so the message never reached the tampered value and
    /// this passes both before and after the fix. It is kept because the message now reads the field
    /// directly, and nothing else would notice if a future change let a tampered <c>name</c> land there.
    /// </summary>
    [Test]
    public void ConstructorRequiresNewDoesNotCoerceATamperedNameProperty()
    {
        var engine = new Engine();

        // `name` is configurable on every built-in constructor, so a script may replace it with an
        // object whose toString throws.
        var value = engine.Evaluate("""
            Object.defineProperty(Map, 'name', { value: { toString: function () { throw new Error('BOOM'); } } });
            try { Map(); return 'no throw'; }
            catch (e) { return (e && e.constructor ? e.constructor.name : typeof e) + ':' + (e && e.message); }
            """);

        value.AsString().Should().StartWith("TypeError:");
    }

    [Test]
    public void InstanceOfWithASymbolPrototypeReportsTheIntendedError()
    {
        var engine = new Engine();

        // The prototype is proven non-object here, but TypeConverter.ToString throws for a Symbol,
        // which used to replace the intended message with "Cannot convert a Symbol value to a string".
        var value = engine.Evaluate("""
            function f() {}
            f.prototype = Symbol('tag');
            try { ({}) instanceof f; return 'no throw'; } catch (e) { return e.message; }
            """);

        value.AsString().Should().Contain("non-object prototype");
    }

    /// <summary>
    /// A guard against over-correcting this bug class. The base of a nullish read is proven
    /// <c>undefined</c> or <c>null</c> by the caller, so interpolating it runs nothing — and the wording
    /// it produces is the single most recognizable error message in JavaScript. Rendering the value's
    /// <c>Type</c> instead would silently turn it into "... of Undefined" for every embedder.
    /// </summary>
    [TestCase("var u; typeof u.x;", "Cannot read property 'x' of undefined")]
    [TestCase("var n = null; typeof n.x;", "Cannot read property 'x' of null")]
    public void ANullishReadKeepsTheLowercaseLiteralWording(string script, string expected)
    {
        var engine = new Engine();

        var value = engine.Evaluate($$"""
            try { {{script}} return 'no throw'; } catch (e) { return e.message; }
            """);

        value.AsString().Should().Be(expected);
    }

    /// <summary>
    /// Safety must not cost the diagnostic. Where the offending <i>value</i> is what the reader needs,
    /// the message names it — rendering its type instead would leave "search for 'String' in ...".
    /// </summary>
    [TestCase("'foo' in 1;", "Cannot use 'in' operator to search for 'foo' in 1")]
    [TestCase("Symbol('s') in 1;", "Cannot use 'in' operator to search for 'Symbol(s)' in 1")]
    [TestCase("Symbol.keyFor(5);", "5 is not a symbol")]
    [TestCase("(function () { return 5; })()();", "5 is not a function")]
    public void ASafeMessageStillNamesTheOffendingValue(string script, string expected)
    {
        var engine = new Engine();

        var value = engine.Evaluate($$"""
            try { {{script}} return 'no throw'; } catch (e) { return e.message; }
            """);

        value.AsString().Should().Be(expected);
    }

    [Test]
    public void ASymbolKeyOnANullishBaseReportsTheReadError()
    {
        var engine = new Engine();

        // A Symbol *is* primitive, so the "only render a primitive key" guard let it through to
        // TypeConverter.ToString, which throws for symbols — replacing this TypeError with
        // "Cannot convert a Symbol value to a string".
        var value = engine.Evaluate("""
            var u;
            try { u[Symbol.iterator]; return 'no throw'; } catch (e) { return e.message; }
            """);

        value.AsString().Should().Be("Cannot read properties of undefined (reading 'Symbol(Symbol.iterator)')");
    }

    [Test]
    public void DateToPrimitiveStillNamesAnInvalidStringHint()
    {
        var engine = new Engine();

        // A hint that *is* a string is provably primitive, so it stays in the message verbatim.
        var value = engine.Evaluate("""
            try { Date.prototype[Symbol.toPrimitive].call(new Date(), 'boolean'); return 'no throw'; }
            catch (e) { return e.message; }
            """);

        value.AsString().Should().Contain("boolean");
    }
}
