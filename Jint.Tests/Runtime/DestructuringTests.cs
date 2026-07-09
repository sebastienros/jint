namespace Jint.Tests.Runtime;

public class DestructuringTests
{
    private readonly Engine _engine;

    public DestructuringTests()
    {
        _engine = new Engine()
            .SetValue("equal", new Action<object, object>(Assert.Equal));
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

        Assert.True(_engine.Evaluate(Script).AsBoolean());
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

        Assert.True(_engine.Evaluate(Script).AsBoolean());
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

        Assert.Equal("4,5,6", result.AsString());
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

        Assert.Equal("1,2", result.AsString());
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
        Assert.Equal(1, result.AsInteger());
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
        Assert.Equal("6:1", result.AsString());

        // NewExpression key (key comes from the constructed object's toString).
        result = engine.Evaluate("""
            function KeyObj() {} KeyObj.prototype.toString = function() { return 'nk'; };
            function fc(x, { [new KeyObj()]: v }) { return v; }
            fc(1, { nk: 7 });
            """);
        Assert.Equal(7, result.AsNumber());

        // Same expression types in object literals and non-parameter destructuring.
        result = engine.Evaluate("""
            var calls2 = 0;
            var o = { [(calls2++, 'a')]: 1, [new KeyObj()]: 2 };
            var { [(calls2++, 'a')]: a } = o;
            [o.a, o.nk, a, calls2].join(',');
            """);
        Assert.Equal("1,2,1,2", result.AsString());

        // ChainExpression (optional chaining) and TaggedTemplateExpression keys.
        result = engine.Evaluate("""
            var holder = { key: 'c' };
            function tag(strings) { return 't' + strings[0]; }
            var o2 = { [holder?.key]: 3, [tag`x`]: 4 };
            [o2.c, o2.tx].join(',');
            """);
        Assert.Equal("3,4", result.AsString());
    }
}
