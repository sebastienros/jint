using Jint.Native;

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

    [Test]
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

    [Test]
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

    [Test]
    public void WithParameterComputedProperties()
    {
        const string Script = @"
            var qux = 'corge';
            return function({ [qux]: grault }) {
              equal('garply', grault);
            }({ corge: 'garply' });";

        _engine.Execute(Script);
    }

    [Test]
    public void WithParameterFunctionLengthProperty()
    {
        _engine.Execute("equal(0, ((x = 42, y) => {}).length);");
        _engine.Execute("equal(1, ((x, y = 42, z) => {}).length);");
        _engine.Execute("equal(1, ((a, b = 39,) => {}).length);");
        _engine.Execute("equal(2, function({a, b}, [c, d]){}.length);");
    }

    [Test]
    public void WithNestedRest()
    {
        _engine.Execute("return function([x, ...[y, ...z]]) { equal(1, x); equal(2, y); equal('3,4', z + ''); }([1, 2, 3, 4]);");
    }

    [Test]
    public void EmptyRest()
    {
        _engine.Execute("function test({ ...props }){}; test({});");
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
    public void FunctionShapedDefaultDoesNotClaimASuppliedNonFunction()
    {
        // The one-liner from the report: the initializer is syntactically a function, the supplied
        // value is not, and the initializer is therefore never evaluated. Naming used to be selected
        // on the initializer's syntax alone and applied to whatever got bound, so the cast to
        // Function threw an InvalidCastException straight out of Evaluate — past any script catch.
        new Engine().Evaluate("var { a = () => {} } = { a: 1 }; a").AsNumber().Should().Be(1);

        new Engine().Evaluate("""
            var caught = 'nothing';
            try { var { a = () => {} } = { a: 1 }; } catch (e) { caught = 'caught'; }
            caught;
            """).AsString().Should().Be("nothing");
    }

    /// <summary>
    /// Initializer kinds a destructuring default can be written as, paired with the name the bound
    /// function ends up carrying when the default *is* taken. Anonymous definitions get the bound
    /// name (NamedEvaluation); a definition that names itself keeps its own name. Every expectation
    /// here is node v24's answer.
    /// </summary>
    private static readonly (string Initializer, string NameWhenTaken)[] DefaultInitializers =
    [
        ("() => {}", "a"),
        ("function () {}", "a"),
        ("function foo() {}", "foo"),
        ("function* () {}", "a"),
        ("function* gen() {}", "gen"),
        ("class {}", "a"),
        ("class Kls {}", "Kls"),
        ("async () => {}", "a"),
    ];

    /// <summary>
    /// The five destructuring shapes that share
    /// <c>DestructuringPatternAssignmentExpression.ProcessPatterns</c>. <c>#DEFAULT#</c> is the
    /// element carrying the initializer, <c>#VALUE#</c> the value the pattern is fed.
    /// </summary>
    public static TestCases<string> PatternShapes() =>
    [
        "var { #DEFAULT# } = { a: #VALUE# }; describe(a)",
        "var [ #DEFAULT# ] = [ #VALUE# ]; describe(a)",
        "var a; ({ #DEFAULT# } = { a: #VALUE# }); describe(a)",
        "var a; ([ #DEFAULT# ] = [ #VALUE# ]); describe(a)",
        "var r; for (var { #DEFAULT# } of [{ a: #VALUE# }]) { r = a; } describe(r)",
    ];

    private const string Describe =
        "function describe(v) { return typeof v === 'function' ? 'fn:' + v.name : typeof v + ':' + String(v); } ";

    [TestCaseSource(nameof(PatternShapes))]
    public void ADefaultIsOnlyNamedWhenTheDefaultIsWhatGotBound(string shape)
    {
        // NamedEvaluation applies when the initializer is an anonymous function definition *and* the
        // supplied value was undefined, so the initializer is the value being bound. A supplied value
        // is bound as it stands, whatever the initializer looks like.
        // https://tc39.es/ecma262/#sec-runtime-semantics-keyedbindinginitialization
        var supplied = new[]
        {
            ("1", "number:1"),
            ("'s'", "string:s"),
            ("({})", "object:[object Object]"),
            ("(function supplied() {})", "fn:supplied"),
        };

        var expected = new List<string>();
        var actual = new List<string>();

        foreach (var (initializer, nameWhenTaken) in DefaultInitializers)
        {
            foreach (var (source, description) in supplied)
            {
                Record(initializer, source, description);
            }

            // undefined is the one supplied value that lets the initializer run.
            Record(initializer, "undefined", "fn:" + nameWhenTaken);
        }

        string.Join("\n", actual).Should().Be(string.Join("\n", expected));

        void Record(string initializer, string suppliedSource, string description)
        {
            var script = shape
                .Replace("#DEFAULT#", "a = " + initializer)
                .Replace("#VALUE#", suppliedSource);

            expected.Add(script + "  =>  " + description);

            string outcome;
            try
            {
                outcome = new Engine().Evaluate(Describe + script).AsString();
            }
            catch (Exception ex)
            {
                outcome = ex.GetType().Name + ": " + ex.Message;
            }

            actual.Add(script + "  =>  " + outcome);
        }
    }

    [Test]
    public void NamedEvaluationOfADefaultFollowsTheBindingTarget()
    {
        // Expectations are node v24's. The bound name — not the source key — is what names the
        // function, a member-expression target is not an identifier reference so nothing is named,
        // and only undefined lets the initializer run at all.
        var engine = new Engine();
        string Describe(string script) => engine.Evaluate(DestructuringTests.Describe + script).AsString();

        Describe("var { a: b = () => {} } = {}; describe(b)").Should().Be("fn:b");
        Describe("var { a: b = () => {} } = { a: 1 }; describe(b)").Should().Be("number:1");
        Describe("var b; ({ a: b = () => {} } = {}); describe(b)").Should().Be("fn:b");
        Describe("var b; ({ a: b = () => {} } = { a: 1 }); describe(b)").Should().Be("number:1");

        Describe("var k = 'a'; var { [k]: a = () => {} } = {}; describe(a)").Should().Be("fn:a");
        Describe("var k = 'a'; var { [k]: a = () => {} } = { a: 1 }; describe(a)").Should().Be("number:1");

        // A member-expression target gets no NamedEvaluation, so the arrow stays nameless.
        Describe("var o = {}; ({ a: o.x = () => {} } = {}); describe(o.x)").Should().Be("fn:");
        Describe("var o = {}; ([ o.x = () => {} ] = []); describe(o.x)").Should().Be("fn:");
        Describe("var o = {}; ({ a: o.x = () => {} } = { a: 1 }); describe(o.x)").Should().Be("number:1");

        // Nested pattern with its own default.
        Describe("var [ { a = () => {} } = {} ] = []; describe(a)").Should().Be("fn:a");
        Describe("var [ { a = () => {} } = {} ] = [{ a: 1 }]; describe(a)").Should().Be("number:1");

        // null is not undefined — the initializer never runs.
        Describe("var { a = () => {} } = { a: null }; describe(a)").Should().Be("object:null");

        // An initializer that merely produces a function is not a function *definition*, so it is
        // never a NamedEvaluation candidate and stays nameless even when taken.
        Describe("var { a = 0 || (() => {}) } = {}; describe(a)").Should().Be("fn:");
        Describe("var { a = 0 || (() => {}) } = { a: 1 }; describe(a)").Should().Be("number:1");

        // for-of over an array pattern, both ways round.
        Describe("var r; for (var [ a = () => {} ] of [[]]) { r = a; } describe(r)").Should().Be("fn:a");
        Describe("var r; for (var [ a = () => {} ] of [[1]]) { r = a; } describe(r)").Should().Be("number:1");

        // Parameter defaults were already correct; pinned so the two paths cannot drift.
        Describe("function f(a = () => {}) { return a; } describe(f())").Should().Be("fn:a");
        Describe("function g(a = () => {}) { return a; } describe(g(1))").Should().Be("number:1");
        Describe("function h({ a = () => {} } = {}) { return a; } describe(h())").Should().Be("fn:a");
        Describe("function i({ a = () => {} } = {}) { return a; } describe(i({ a: 1 }))").Should().Be("number:1");
    }
}
