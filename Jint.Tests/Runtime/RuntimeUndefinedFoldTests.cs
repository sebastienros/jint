#nullable enable

using Jint.Native;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the run-time fold of the identifier <c>undefined</c> in <see cref="JintIdentifierExpression"/>: a read the
/// static analysis did not fold — every read of unprepared code, which is never analysed, and every read of a
/// program the analysis could not prove safe — answers the global's own <c>undefined</c> without resolving once a
/// full resolution has reached it. That property is non-writable and non-configurable
/// (https://tc39.es/ecma262/#sec-undefined), so only a binding of the name between a read and the global can give it
/// another value, and every scenario here is one such binding, met after reads of <c>undefined</c> have warmed: a
/// fold that outlived its premise shows up as <c>undefined</c> where the binding's value belongs.
/// </summary>
/// <remarks>
/// <para>Most of those bindings are fixed by where the read is, so the hazard is a fold one read site takes over
/// from another. Three are not: a <c>with</c> object's names
/// (https://tc39.es/ecma262/#sec-object-environment-records); a sloppy direct eval's <c>var</c> or function, which
/// EvalDeclarationInstantiation adds to the caller's existing variable environment
/// (https://tc39.es/ecma262/#sec-evaldeclarationinstantiation); and code reached from several scope chains at once,
/// which is what the eval cache makes of a source evaluated by several callers
/// (https://tc39.es/ecma262/#sec-performeval), functions it declares included, and what the debugger makes of a
/// prepared script evaluated in several frames.</para>
/// <para>Each scenario runs from source, prepared, and prepared with the static analysis off. The expected strings
/// were taken from Node 26, running each script with <c>vm.runInThisContext</c>; the debugger scenario has no Node
/// counterpart and its value is derived in its own summary.</para>
/// </remarks>
public class RuntimeUndefinedFoldTests
{
    public enum Preparation
    {
        Source,
        Prepared,
        PreparedWithoutStaticAnalysis,
    }

    public static readonly TestCases<Preparation> Preparations = new()
    {
        Preparation.Source,
        Preparation.Prepared,
        Preparation.PreparedWithoutStaticAnalysis,
    };

    private static Prepared<Script> Prepare(string code, Preparation preparation) => preparation switch
    {
        Preparation.Prepared => Engine.PrepareScript(code),
        _ => Engine.PrepareScript(code, options: new ScriptPreparationOptions { StaticAnalysis = false }),
    };

    private static string Run(string code, Preparation preparation)
    {
        using var engine = new Engine();
        var result = preparation == Preparation.Source
            ? engine.Evaluate(code)
            : engine.Evaluate(Prepare(code, preparation));
        return result.AsString();
    }

    private const string ShadowingScript = """
        var log = [];
        function warm() { var n = 0; for (var i = 0; i < 50; i++) { if (undefined === void 0) n++; if (n === undefined) n = -1; } return n; }
        log.push(warm(), warm());
        function check(x) { var r; for (var i = 0; i < 5; i++) r = [x, x === undefined, x !== undefined, x == undefined, x != undefined].join(); return r; }
        function param(undefined) { var r; for (var i = 0; i < 5; i++) r = [undefined, 1 === undefined, 1 == undefined].join(); return r; }
        function viaVar() { var undefined = 2; var r; for (var i = 0; i < 5; i++) r = [undefined, 2 === undefined, 2 != undefined].join(); return r; }
        function viaLet() { var r; { let undefined = 3; for (let i = 0; i < 5; i++) r = [undefined, 3 === undefined, 3 == undefined].join(); } return r; }
        function viaCatch() { try { throw 4; } catch (undefined) { var r; for (var i = 0; i < 5; i++) r = [undefined, 4 === undefined].join(); return r; } }
        var viaFunctionExpression = function undefined() { var r; for (var i = 0; i < 5; i++) r = typeof undefined + ':' + (undefined === viaFunctionExpression); return r; };
        var viaClassExpression = class undefined { static m() { var r; for (var i = 0; i < 5; i++) r = typeof undefined + ':' + (undefined === viaClassExpression); return r; } };
        function viaWith() { var r; with ({ undefined: 6 }) { for (var i = 0; i < 5; i++) r = [undefined, 6 === undefined, 6 == undefined].join(); } return r; }
        for (var round = 0; round < 3; round++) {
            log.push(check(void 0), check(null), check(0), param(1), viaVar(), viaLet(), viaCatch(), viaFunctionExpression(), viaClassExpression.m(), viaWith(), warm());
        }
        log.join(';')
        """;

    private const string ShadowingRound = ",true,false,true,false;,false,true,true,false;0,false,true,false,true;1,true,true;2,true,false;3,true,true;4,true;function:true;function:true;6,true,true;50";
    private const string ShadowingExpected = "50;50;" + ShadowingRound + ";" + ShadowingRound + ";" + ShadowingRound;

    /// <summary>
    /// A parameter, a <c>var</c>, a <c>let</c>, a catch parameter, a function or class expression's own name and a
    /// <c>with</c> object, each named <c>undefined</c>, among reads of the global one that warmed before them and keep
    /// running between them — the strict and loose comparison lanes included.
    /// </summary>
    [TestCaseSource(nameof(Preparations))]
    public void ABindingNamedUndefinedStillShadowsTheGlobalOneAfterWarmReads(Preparation preparation)
    {
        Run(ShadowingScript, preparation)
            .Should().Be(ShadowingExpected);
    }

    /// <summary>
    /// A sloppy direct eval adds <c>var undefined</c> or <c>function undefined</c> to a function's variable
    /// environment after a read beneath it has folded: the same read site in the same call, a closure created
    /// before the eval, a nested function declaration, and the same function called again with and without the
    /// eval, whose fresh environments must each be judged on their own.
    /// </summary>
    [TestCaseSource(nameof(Preparations))]
    public void ASloppyDirectEvalDeclaringUndefinedEndsTheFoldOfAWarmRead(Preparation preparation)
    {
        const string Code = """
            var log = [];
            function sameSite(inject) {
                var r = [];
                for (var i = 0; i < 6; i++) {
                    if (inject && i === 3) eval('var undefined = 7');
                    r.push(String(undefined), 7 === undefined, 7 == undefined);
                }
                return r.join();
            }
            function closure(inject) {
                var get = function () { return [String(undefined), 8 === undefined, 8 != undefined].join(); };
                var before;
                for (var i = 0; i < 5; i++) before = get();
                if (inject) eval('var undefined = 8');
                return before + '|' + get();
            }
            function nested(inject) {
                function inner() { var r; for (var i = 0; i < 3; i++) r = typeof undefined; return r; }
                var before = inner();
                if (inject) eval('function undefined() {}');
                return before + '|' + inner();
            }
            for (var round = 0; round < 2; round++) {
                log.push(sameSite(false), sameSite(true), sameSite(false), closure(false), closure(true), closure(false), nested(false), nested(true), nested(false));
            }
            log.join(';')
            """;

        const string Unshadowed = "undefined,false,false,undefined,false,false,undefined,false,false,undefined,false,false,undefined,false,false,undefined,false,false";
        const string Injected = "undefined,false,false,undefined,false,false,undefined,false,false,7,true,true,7,true,true,7,true,true";
        const string Round = Unshadowed + ";" + Injected + ";" + Unshadowed
            + ";undefined,false,true|undefined,false,true;undefined,false,true|8,true,false;undefined,false,true|undefined,false,true"
            + ";undefined|undefined;undefined|function;undefined|undefined";

        Run(Code, preparation).Should().Be(Round + ";" + Round);
    }

    /// <summary>
    /// The eval cache hands one source's code, and every function that code declares, to every caller that
    /// evaluates it — callers whose scope chains differ. Strict callers add no binding to anything that exists, so
    /// nothing is ever invalidated for them: a fold one caller's chain allowed must not answer for another's, whether
    /// the read is in a function the source declares or in the source's own top level, nor for a caller under a
    /// <c>with</c>.
    /// </summary>
    [TestCaseSource(nameof(Preparations))]
    public void EvalCodeSharedBetweenCallersIsNotFoldedForAllOfThem(Preparation preparation)
    {
        const string Code = """
            var log = [];
            var source = '(function () { var r; for (var i = 0; i < 3; i++) r = [String(undefined), 5 === undefined].join(); return r; })';
            function strictPlain() { 'use strict'; return eval(source); }
            function strictShadow(undefined) { 'use strict'; return eval(source); }
            function sloppyPlain() { return eval(source); }
            function sloppyShadow(undefined) { return eval(source); }
            function strictTop() { 'use strict'; return String(eval('undefined')); }
            function strictTopShadow(undefined) { 'use strict'; return String(eval('undefined')); }
            var withObject = { undefined: 'with' };
            var underWith;
            with (withObject) { underWith = function () { 'use strict'; return String(eval('undefined')); }; }
            for (var round = 0; round < 3; round++) {
                var plain = [strictPlain(), strictPlain(), sloppyPlain(), sloppyPlain()];
                var shadowed = [strictShadow(5), sloppyShadow(5)];
                for (var i = 0; i < 5; i++) plain.forEach(function (f) { f(); });
                log.push(plain.map(function (f) { return f(); }).join('/'), shadowed.map(function (f) { return f(); }).join('/'));
                log.push(strictTop(), strictTop(), strictTopShadow('p'), underWith(), strictTop());
            }
            log.join(';')
            """;

        const string Round = "undefined,false/undefined,false/undefined,false/undefined,false;5,true/5,true;undefined;undefined;p;with;undefined";
        Run(Code, preparation).Should().Be(Round + ";" + Round + ";" + Round);
    }

    /// <summary>
    /// A <c>with</c> object that gains a property named <c>undefined</c> part-way through a warm loop: a read whose
    /// chain crosses an object environment is never folded.
    /// </summary>
    [TestCaseSource(nameof(Preparations))]
    public void AWithObjectGainingUndefinedShadowsAWarmRead(Preparation preparation)
    {
        const string Code = """
            var o = {};
            function viaWithLater() { var r = []; with (o) { for (var i = 0; i < 6; i++) { if (i === 3) o.undefined = 12; r.push(String(undefined), 12 === undefined); } } delete o.undefined; return r.join(); }
            [viaWithLater(), viaWithLater()].join(';')
            """;

        const string Once = "undefined,false,undefined,false,undefined,false,12,true,12,true,12,true";
        Run(Code, preparation).Should().Be(Once + ";" + Once);
    }

    /// <summary>
    /// One preparation run on two engines, alternately: each engine's reads fold against its own global, and what one
    /// engine folded never answers for the other.
    /// </summary>
    [TestCase(Preparation.Prepared)]
    [TestCase(Preparation.PreparedWithoutStaticAnalysis)]
    public void APreparationSharedByTwoEnginesFoldsOnEachOnItsOwn(Preparation preparation)
    {
        var prepared = Prepare(ShadowingScript, preparation);
        using var first = new Engine();
        using var second = new Engine();

        first.Evaluate(prepared).AsString().Should().Be(ShadowingExpected);
        second.Evaluate(prepared).AsString().Should().Be(ShadowingExpected);
        first.Evaluate(prepared).AsString().Should().Be(ShadowingExpected);
        second.Evaluate(prepared).AsString().Should().Be(ShadowingExpected);
    }

    /// <summary>
    /// A shadow realm has a global <c>undefined</c> of its own, and code evaluated in it may declare a lexical
    /// <c>undefined</c>, which lives in the evaluation's own environment rather than on the realm's global.
    /// </summary>
    [TestCaseSource(nameof(Preparations))]
    public void ShadowRealmCodeFoldsAndShadowsOnItsOwn(Preparation preparation)
    {
        const string Code = """
            var realm = new ShadowRealm();
            var inner = "function f(undefined) { var r; for (var i = 0; i < 5; i++) r = undefined; return r; }"
                + "var n = 0; for (var i = 0; i < 50; i++) if (undefined === void 0) n++;"
                + "n + ':' + f(9) + ':' + typeof undefined";
            var shadowed = "let undefined = 10; var m = 0; for (var i = 0; i < 5; i++) if (undefined === 10) m++; m + ':' + undefined";
            var log = [];
            for (var round = 0; round < 3; round++) {
                log.push(realm.evaluate(inner), realm.evaluate(shadowed), realm.evaluate(inner));
                var n = 0; for (var i = 0; i < 20; i++) if (undefined === void 0) n++;
                log.push(n);
            }
            log.join(';')
            """;

        const string Round = "50:9:undefined;5:10;50:9:undefined;20";
        Run(Code, preparation).Should().Be(Round + ";" + Round + ";" + Round);
    }

    /// <summary>
    /// The debugger evaluates a prepared script in whatever environment it is asked to, and every function the script
    /// declares shares one definition between those evaluations. Evaluated without a frame it runs in the innermost
    /// execution context, here that of the script function calling the host: <c>plain</c>'s chain reaches the global
    /// <c>undefined</c>, <c>shadow</c>'s stops at its parameter, so the last answer is the parameter's 13.
    /// </summary>
    [TestCase(Preparation.Prepared)]
    [TestCase(Preparation.PreparedWithoutStaticAnalysis)]
    public void ADebuggerEvaluationOfAPreparedScriptIsNotFoldedAcrossEnvironments(Preparation preparation)
    {
        // the analysis cannot see the frame, so it is kept off here by a dead eval; see the remarks of this class
        var watch = Prepare(
            "(function () { function inner() { var x; for (var i = 0; i < 3; i++) x = undefined; return x; } return String(inner()); })();"
            + "function unusedEval() { return eval('0'); }",
            preparation);

        using var engine = new Engine();
        engine.SetValue("probe", new Func<JsValue>(() => engine.Debugger.Evaluate(watch)));

        engine.Evaluate("""
            function plain() { return probe(); }
            function shadow(undefined) { return probe(); }
            var r = [];
            for (var i = 0; i < 5; i++) r.push(plain());
            r.push(shadow(13));
            r.join()
            """).AsString().Should().Be("undefined,undefined,undefined,undefined,undefined,13");
    }

    /// <summary>
    /// The change itself: a read of <c>undefined</c> that nothing folded statically is folded by its first resolution,
    /// so the reads after it answer from <see cref="Jint.Runtime.Environments.Environment.BindingName.CalculatedValue"/>
    /// as a statically folded read does. Parsed without the analysis, which is how <c>Execute(string)</c> parses.
    /// </summary>
    [Test]
    public void AReadOfTheGlobalUndefinedFoldsInUnanalysedCode()
    {
        using var engine = new Engine();
        var identifier = ReadIdentifier("undefined");
        identifier.Identifier.CalculatedValue.Should().BeNull("nothing has resolved the read yet");

        identifier.GetValue(new EvaluationContext(engine)).Should().BeSameAs(JsValue.Undefined);

        identifier.Identifier.CalculatedValue.Should().BeSameAs(JsValue.Undefined, "a resolution reaching the global's own undefined folds the read");
        identifier.GetValue(new EvaluationContext(engine)).Should().BeSameAs(JsValue.Undefined);
    }

    /// <summary>
    /// The control for the test above: a global of any other name is never folded, however read-only it is.
    /// </summary>
    [TestCase("NaN")]
    [TestCase("Infinity")]
    public void AReadOfAnotherReadOnlyGlobalIsNotFolded(string name)
    {
        using var engine = new Engine();
        var identifier = ReadIdentifier(name);

        identifier.GetValue(new EvaluationContext(engine));
        identifier.GetValue(new EvaluationContext(engine));

        identifier.Identifier.CalculatedValue.Should().BeNull();
    }

    private static JintIdentifierExpression ReadIdentifier(string name)
    {
        var prepared = Engine.PrepareScript(name, options: new ScriptPreparationOptions { StaticAnalysis = false });
        var statement = prepared.Program!.Body[0].Should().BeAssignableTo<ExpressionStatement>().Which;
        var identifier = statement.Expression.Should().BeOfType<Identifier>().Which;
        return new JintIdentifierExpression(identifier);
    }
}
