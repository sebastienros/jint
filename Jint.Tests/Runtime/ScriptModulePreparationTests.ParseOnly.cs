using Jint.Native;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see cref="ScriptPreparationOptions.StaticAnalysis"/> / <see cref="ModulePreparationOptions.StaticAnalysis"/>:
/// preparation that only parses. What has to hold is that the resulting program behaves exactly like an analyzed
/// one, that the analyzer really did not run (nothing of its is on the tree), and that what the interpreter
/// rebuilds lazily still lands back on the shared tree where the next engine can reuse it.
/// </summary>
public partial class ScriptModulePreparationTests
{
    private static readonly ScriptPreparationOptions ParseOnlyScript = new() { StaticAnalysis = false };
    private static readonly ModulePreparationOptions ParseOnlyModule = new() { StaticAnalysis = false };

    [Theory]
    // one row per node type the analyzer has a case for, plus the syntax that reaches them indirectly
    [InlineData("1 + 2", "3")]                                                                      // folded binary
    [InlineData("-1", "-1")]                                                                        // folded unary
    [InlineData("'a' + 'b'", "ab")]                                                                 // folded string binary
    [InlineData("var o = { a: { b: 7 } }; o.a.b", "7")]                                             // member chain
    [InlineData("var o = {}; o['k'] = 1; o.k + o['k']", "2")]                                       // computed member write/read
    [InlineData("function f(x) { var y = x * 2; return y; } f(21)", "42")]                          // function state, var scope
    [InlineData("function f() { return 5; } f()", "5")]                                             // constant return
    [InlineData("(function f(n) { return n < 2 ? n : f(n - 1) + f(n - 2); })(10)", "55")]           // recursion
    [InlineData("var s = 0; for (let i = 0; i < 4; i++) { s += i; } s", "6")]                        // for-let loop env
    [InlineData("(function () { let t = 0; for (const v of [1, 2, 3]) { t += v; } return t; })()", "6")] // for-of
    [InlineData("var s = ''; for (var k in { a: 1, b: 2 }) { s += k; } s", "ab")]                    // for-in
    [InlineData("{ let a = 1; var s = a; } s", "1")]                                                // nested block state
    [InlineData("class C { constructor() { this.v = 3; } get double() { return this.v * 2; } } new C().double", "6")]
    [InlineData("function* g() { yield 1; yield 2; } [...g()].join('-')", "1-2")]
    [InlineData("var { p, q = 5 } = { p: 1 }; p + q", "6")]
    [InlineData("[1, 2, 3].map(x => x * x).join(',')", "1,4,9")]
    [InlineData("var n = 2; `x${n + 1}y`", "x3y")]
    [InlineData("/(\\d+)-(\\d+)/.exec('12-34')[2]", "34")]
    [InlineData("typeof undeclaredThing", "undefined")]
    [InlineData("try { null.x; 'no' } catch (e) { e.constructor.name }", "TypeError")]
    [InlineData("var a = 0; switch (2) { case 1: a = 10; break; case 2: a = 20; break; } a", "20")]
    public void ParseOnlyPreparedScriptsEvaluateAsAnalyzedOnesDo(string code, string expected)
    {
        var analyzed = new Engine().Evaluate(Engine.PrepareScript(code)).ToString();
        var parseOnly = new Engine().Evaluate(Engine.PrepareScript(code, options: ParseOnlyScript)).ToString();

        analyzed.Should().Be(expected, "the analyzed baseline itself must be right before it can be a baseline");
        parseOnly.Should().Be(expected);
    }

    [Fact]
    public void ParseOnlyPreparedModulesEvaluateAsAnalyzedOnesDo()
    {
        const string Code = """
            const nested = { a: { b: 'deep' } };
            export const answer = 6 * 7;
            export const path = nested.a.b;
            export const squares = [1, 2, 3].map(x => x * x).join(',');
            export function greet(name) { let parts = ['hello', name]; return parts.join(' '); }
            class Counter { constructor() { this.n = 0; } bump() { this.n += 1; return this.n; } }
            export const bumped = (function () { const c = new Counter(); c.bump(); return c.bump(); })();
            """;

        const string Expected = "42|deep|1,4,9|2|hello world";

        Observe(Engine.PrepareModule(Code)).Should().Be(Expected, "the analyzed baseline itself must be right");
        Observe(Engine.PrepareModule(Code, options: ParseOnlyModule)).Should().Be(Expected);

        static string Observe(Prepared<Module> module)
        {
            var engine = new Engine();
            engine.Modules.Add("main", x => x.AddModule(module));
            var ns = engine.Modules.Import("main");

            return string.Join("|",
                ns.Get("answer"),
                ns.Get("path"),
                ns.Get("squares"),
                ns.Get("bumped"),
                engine.Invoke(ns.Get("greet"), "world"));
        }
    }

    [Fact]
    public void ParseOnlyPreparationPublishesNothingOntoAScriptAst()
    {
        const string Code = "function f(x) { { let y = x + 1; return y; } } f(1);";
        var prepared = Engine.PrepareScript(Code, options: ParseOnlyScript);

        // Nothing of the analyzer's is anywhere on the tree: not the script root's hoisting scope, not the
        // function's definition state, not the block's scope state, not the identifier's binding name, not the
        // literal's constant.
        var function = prepared.Program.Body[0].Should().BeOfType<FunctionDeclaration>().Which;
        var block = ((FunctionBody) function.Body).Body[0].Should().BeOfType<NestedBlockStatement>().Which;
        var init = ((VariableDeclaration) block.Body[0]).Declarations[0].Init.Should().BeOfType<NonLogicalBinaryExpression>().Which;
        var identifier = init.Left.Should().BeOfType<Identifier>().Which;
        var literal = init.Right.Should().BeOfType<NumericLiteral>().Which;

        prepared.Program.UserData.Should().BeNull();
        function.UserData.Should().BeNull();
        block.UserData.Should().BeNull();
        identifier.UserData.Should().BeNull();
        literal.UserData.Should().BeNull();

        new Engine().Evaluate(prepared).AsNumber().Should().Be(2);

        // Executing publishes back exactly the engine-neutral part, which is what the next engine reuses.
        function.UserData.Should().BeOfType<JintFunctionDefinition.State>();
        block.UserData.Should().BeOfType<JintBlockStatement.BlockState>();
        literal.UserData.Should().Be(JsNumber.Create(1));

        // ...and only that part. A binding name is built from the parse's own string instance and a determined
        // member property is a JsValue, both fine to share, but nothing publishes them lazily, so they stay
        // per engine and the node stays clean.
        prepared.Program.UserData.Should().BeNull();
        identifier.UserData.Should().BeNull();
    }

    [Fact]
    public void ParseOnlyPreparationPublishesNothingOntoAModuleAst()
    {
        const string Code = "export function f(x) { { let y = x + 1; return y; } } export const answer = f(1);";
        var prepared = Engine.PrepareModule(Code, options: ParseOnlyModule);

        var export = prepared.Program.Body[0].Should().BeOfType<ExportNamedDeclaration>().Which;
        var function = export.Declaration.Should().BeOfType<FunctionDeclaration>().Which;
        var block = ((FunctionBody) function.Body).Body[0].Should().BeOfType<NestedBlockStatement>().Which;
        var init = ((VariableDeclaration) block.Body[0]).Declarations[0].Init.Should().BeOfType<NonLogicalBinaryExpression>().Which;
        var identifier = init.Left.Should().BeOfType<Identifier>().Which;
        var literal = init.Right.Should().BeOfType<NumericLiteral>().Which;

        function.UserData.Should().BeNull();
        block.UserData.Should().BeNull();
        identifier.UserData.Should().BeNull();
        literal.UserData.Should().BeNull();

        var engine = new Engine();
        engine.Modules.Add("main", x => x.AddModule(prepared));
        engine.Modules.Import("main").Get("answer").AsNumber().Should().Be(2);

        function.UserData.Should().BeOfType<JintFunctionDefinition.State>();
        block.UserData.Should().BeOfType<JintBlockStatement.BlockState>();
        literal.UserData.Should().Be(JsNumber.Create(1));
        identifier.UserData.Should().BeNull();
    }

    [Fact]
    public void ParseOnlyPreparationStillCollectsReferencedGlobalsForAScript()
    {
        // The collector is a visitor of its own rather than a flag inside the analyzer's, so skipping the
        // analysis leaves it perfectly able to run alone.
        var prepared = Engine.PrepareScript(
            "function f(x) { return x + globalA; } f(globalB);",
            options: new ScriptPreparationOptions { StaticAnalysis = false, CollectReferencedGlobals = true });

        prepared.ReferencedGlobals.Should().BeEquivalentTo(["globalA", "globalB"]);
        prepared.Program.UserData.Should().BeNull();
        prepared.Program.Body[0].UserData.Should().BeNull();
    }

    [Fact]
    public void ParseOnlyPreparationStillCollectsReferencedGlobalsForAModule()
    {
        var prepared = Engine.PrepareModule(
            "export function f(x) { return x + globalA; } export const v = f(globalB);",
            options: new ModulePreparationOptions { StaticAnalysis = false, CollectReferencedGlobals = true });

        prepared.ReferencedGlobals.Should().BeEquivalentTo(["globalA", "globalB"]);
        prepared.Program.Body[0].UserData.Should().BeNull();
    }

    [Fact]
    public void ParseOnlyPreparationRetainsFunctionSourceTextWhenAsked()
    {
        // Retained source text is recorded by the parser's own node handler, which the analyzer used to displace
        // (and compensate for). With no analyzer that handler has to survive untouched.
        const string Code = "function f(x) { return x + 1; } f.toString();";

        Evaluate(false).Should().Be("function f(x) { return x + 1; }");

        // ...including when the referenced-globals collector has a visitor of its own to install as well.
        Evaluate(true).Should().Be("function f(x) { return x + 1; }");

        static string Evaluate(bool collectReferencedGlobals)
        {
            var options = new ScriptPreparationOptions
            {
                StaticAnalysis = false,
                CollectReferencedGlobals = collectReferencedGlobals,
                ParsingOptions = new ScriptParsingOptions { RetainFunctionSourceText = true },
            };

            return new Engine().Evaluate(Engine.PrepareScript(Code, options: options)).AsString();
        }
    }

    [Fact]
    public void ParseOnlyPreparationRetainsFunctionSourceTextForAModuleWhenAsked()
    {
        var options = new ModulePreparationOptions
        {
            StaticAnalysis = false,
            ParsingOptions = new ModuleParsingOptions { RetainFunctionSourceText = true },
        };

        var engine = new Engine();
        var code = "export function f(x) { return x + 1; }\nexport const source = f.toString();";
        engine.Modules.Add("main", x => x.AddModule(Engine.PrepareModule(code, options: options)));

        engine.Modules.Import("main").Get("source").AsString().Should().Be("function f(x) { return x + 1; }");
    }

    [Fact]
    public void PreparationOptionsStayValueTypedAndWithSafeAcrossParserOptionDerivation()
    {
        // The derived ParserOptions is memoized per parsing-options instance, deliberately outside the record:
        // a private field would join the compiler's synthesized equality (warming it would flip ==) and would be
        // copied stale into every `with` clone.
        var a = new ScriptPreparationOptions { StaticAnalysis = false };
        var b = new ScriptPreparationOptions { StaticAnalysis = false };

        Engine.PrepareScript("1", options: a);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());

        // A clone that changes the parsing options must not answer from the original's derivation: this one
        // retains source text and the memo of the original does not.
        var retaining = a with { ParsingOptions = new ScriptParsingOptions { RetainFunctionSourceText = true } };
        new Engine().Evaluate(Engine.PrepareScript("function f() { return 1; } f.toString();", options: retaining))
            .AsString().Should().Be("function f() { return 1; }");

        // ...and in the other direction, a clone that only changes an analysis flag keeps the non-retaining one.
        new Engine().Evaluate(Engine.PrepareScript("function f() { return 1; } f.toString();", options: retaining with { StaticAnalysis = true }))
            .AsString().Should().Be("function f() { return 1; }");
        new Engine().Evaluate(Engine.PrepareScript("function f() { return 1; } f.toString();", options: a with { StaticAnalysis = true }))
            .AsString().Should().Be("function f() { [native code] }");
    }
}
