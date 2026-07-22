namespace Jint.Tests.Runtime;

public class DestructuringTests
{
    private readonly Engine _engine;

    public DestructuringTests()
    {
        _engine = new Engine()
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));
    }

    [Fact]
    public void WithParameterStrings()
    {
        const string Script = @"
            return function([a, b, c]) {
              equal('a', a);
              equal('b', b);
              return c === void undefined;
            }('ab');";

        _engine.Evaluate(Script).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void WithParameterObjectPrimitives()
    {
        const string Script = @"
            return function({toFixed}, {slice}) {
              equal(Number.prototype.toFixed, toFixed);
              equal(String.prototype.slice, slice);
              return true;
            }(2,'');";

        _engine.Evaluate(Script).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void WithParameterComputedProperties()
    {
        const string Script = @"
            var qux = 'corge';
            return function({ [qux]: grault }) {
              equal('garply', grault);
            }({ corge: 'garply' });";

        _engine.Execute(Script);
    }

    [Fact]
    public void WithParameterFunctionLengthProperty()
    {
        _engine.Execute("equal(0, ((x = 42, y) => {}).length);");
        _engine.Execute("equal(1, ((x, y = 42, z) => {}).length);");
        _engine.Execute("equal(1, ((a, b = 39,) => {}).length);");
        _engine.Execute("equal(2, function({a, b}, [c, d]){}.length);");
    }

    [Fact]
    public void WithNestedRest()
    {
        _engine.Execute("return function([x, ...[y, ...z]]) { equal(1, x); equal(2, y); equal('3,4', z + ''); }([1, 2, 3, 4]);");
    }

    [Fact]
    public void EmptyRest()
    {
        _engine.Execute("function test({ ...props }){}; test({});");
    }

    [Fact]
    public void ObjectRestFromPrimitiveCopiesOwnEnumerableProperties()
    {
        // RestBindingInitialization / RestDestructuringAssignmentEvaluation perform
        // CopyDataProperties(restObj, value, excludedNames), whose step 2 ToObject's the
        // primitive source (https://tc39.es/ecma262/#sec-copydataproperties) — so a string's
        // index properties are copied while already-destructured keys are excluded.
        _engine.Evaluate("var { ...r1 } = 'ab'; JSON.stringify(r1)").AsString().Should().Be("""{"0":"a","1":"b"}""");
        _engine.Evaluate("var { 0: first, ...r2 } = 'ab'; first + '|' + JSON.stringify(r2)").AsString().Should().Be("a|{\"1\":\"b\"}");
        _engine.Evaluate("var { ...r3 } = 42; JSON.stringify(r3)").AsString().Should().Be("{}");

        // Destructuring assignment (non-declaration) form.
        _engine.Evaluate("var q; (({ ...q } = 'ab')); JSON.stringify(q)").AsString().Should().Be("""{"0":"a","1":"b"}""");

        // Function parameter rest.
        _engine.Evaluate("(function({ ...p }) { return JSON.stringify(p); })('ab')").AsString().Should().Be("""{"0":"a","1":"b"}""");
    }

    [Fact]
    public void VarDestructuringInForOfShouldHoistInStrictMode()
    {
        // Nested destructuring var names must be hoisted to function scope.
        // Previously only Identifier bindings were collected; patterns like [[x]] were skipped,
        // causing ReferenceError in strict mode.
        var result = _engine.Evaluate("""
            'use strict';
            (function() {
                var results = [];
                for (var [[x, y, z] = [4, 5, 6]] of [[]]) {
                    results.push(x, y, z);
                }
                return results.join(',');
            })()
            """);

        result.AsString().Should().Be("4,5,6");
    }

    [Fact]
    public void VarObjectDestructuringInForOfShouldHoistInStrictMode()
    {
        var result = _engine.Evaluate("""
            'use strict';
            (function() {
                for (var { a, b } of [{ a: 1, b: 2 }]) {}
                return a + ',' + b;
            })()
            """);

        result.AsString().Should().Be("1,2");
    }

    [Fact]
    public void VarDestructuringInForAwaitOfShouldHoistInStrictMode()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            'use strict';
            (async function() {
                for await (var [[x] = [1]] of [[]]) {}
                return x;
            })()
            """);

        result = result.UnwrapIfPromise();
        result.AsInteger().Should().Be(1);
    }

    [Fact]
    public void ComputedKeysEvaluateAnyExpressionType()
    {
        // TryGetComputedPropertyKey used to have a node-type allowlist with a silent Undefined
        // fallback: computed keys like a parenthesized sequence or `new` expression bound the
        // property "undefined" and never ran the key expression (side effects were skipped).
        var engine = new Engine();

        // SequenceExpression key in parameter-position destructuring — must evaluate (side effect!)
        // and bind the right property.
        var result = engine.Evaluate("""
            var calls = 0;
            function fb(x, { [(calls++, "k")]: v }) { return v; }
            fb(1, { k: 6 }) + ':' + calls;
            """);
        result.AsString().Should().Be("6:1");

        // NewExpression key (key comes from the constructed object's toString).
        result = engine.Evaluate("""
            function KeyObj() {} KeyObj.prototype.toString = function() { return 'nk'; };
            function fc(x, { [new KeyObj()]: v }) { return v; }
            fc(1, { nk: 7 });
            """);
        result.AsNumber().Should().Be(7);

        // Same expression types in object literals and non-parameter destructuring.
        result = engine.Evaluate("""
            var calls2 = 0;
            var o = { [(calls2++, 'a')]: 1, [new KeyObj()]: 2 };
            var { [(calls2++, 'a')]: a } = o;
            [o.a, o.nk, a, calls2].join(',');
            """);
        result.AsString().Should().Be("1,2,1,2");

        // ChainExpression (optional chaining) and TaggedTemplateExpression keys.
        result = engine.Evaluate("""
            var holder = { key: 'c' };
            function tag(strings) { return 't' + strings[0]; }
            var o2 = { [holder?.key]: 3, [tag`x`]: 4 };
            [o2.c, o2.tx].join(',');
            """);
        result.AsString().Should().Be("3,4");
    }
}
