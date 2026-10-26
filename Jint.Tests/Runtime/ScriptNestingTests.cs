#nullable enable

using System.Text;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// How deep a script's syntax tree may nest (sebastienros/jint#4356). Generated code is deep without being
/// malformed: a proxy auto-config file is one long <c>||</c> chain or one long <c>else if</c> chain, and the
/// parser builds a left-associative chain in a loop, one level of tree per operand. The bound is the stack —
/// probed where the tree is walked and built, and inside a deep operator or <c>else if</c> chain where it runs —
/// not a level count. A tree too deep for the stack fails with a <see cref="ScriptPreparationException"/> (or the
/// parser's own <see cref="InsufficientExecutionStackException"/>), never with a native stack overflow, which
/// ends the process and which no <c>catch</c> survives: a regression here is a dead test host, not a red test.
/// </summary>
public class ScriptNestingTests
{
    // Every operator the chains below are made of, the three places a chain can stand, and the three entries
    // that run a script.
    private static readonly string[] Operators = ["||", "&&", "??", "+", "-"];
    private static readonly string[] Positions = ["script", "function", "eval"];
    private static readonly string[] Entries = ["Evaluate", "Execute", "PrepareScript"];

    // Stated rather than the platform default, which is 512 KB for a secondary thread on macOS: room to build a
    // thousand levels, and few enough frames that recursing to the bottom of it stays quick.
    private const int DeepCallStackSize = 2 * 1024 * 1024;

    public static TestCases<string, string, string> EveryOperatorPositionAndEntry
    {
        get
        {
            var cases = new TestCases<string, string, string>();
            foreach (var op in Operators)
            {
                foreach (var position in Positions)
                {
                    foreach (var entry in Entries)
                    {
                        cases.Add(op, position, entry);
                    }
                }
            }

            return cases;
        }
    }

    // The issue's two shapes, which used to fail at 300 with "Script nesting exceeds maximum depth of 256
    // levels". Node runs both at 300; at 2000 it runs the || chain and throws a RangeError for the else-if
    // chain, while a large stack runs both here.

    [TestCase(300)]
    [TestCase(2000)]
    public void AProxyAutoConfigOrChainRuns(int terms) => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute(OrChainProxyAutoConfig(terms));

        engine.Invoke("FindProxyForURL", "http://x.example/", $"h{terms - 1}.example").AsString().Should().Be("PROXY proxy.example:8080");
        engine.Invoke("FindProxyForURL", "http://x.example/", "elsewhere.example").AsString().Should().Be("DIRECT");
    });

    [TestCase(300)]
    [TestCase(2000)]
    public void AProxyAutoConfigElseIfChainRuns(int arms) => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute(ElseIfChainProxyAutoConfig(arms));

        engine.Invoke("FindProxyForURL", "http://x.example/", $"h{arms - 1}.example").AsString().Should().Be($"PROXY p{arms - 1}.example:8080");
        engine.Invoke("FindProxyForURL", "http://x.example/", "elsewhere.example").AsString().Should().Be("DIRECT");
    });

    [TestCaseSource(nameof(EveryOperatorPositionAndEntry))]
    public void ATwoThousandOperandChainRuns(string op, string position, string entry) => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        RunThrough(engine, entry, ChainScript(op, 2000, position));

        engine.GetValue("r").AsNumber().Should().Be(ChainValue(op, 2000));
    });

    [TestCaseSource(nameof(EveryOperatorPositionAndEntry))]
    public void AHundredThousandOperandChainFailsCleanly(string op, string position, string entry) => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        var exception = Caught.Exception(() => RunThrough(engine, entry, ChainScript(op, 100_000, position)));

        // Building the chain probes the stack. The one exception is a direct eval, whose source an Acornima
        // visitor walks first; that visitor probes too, with the runtime's own exception type.
        if (position == "eval")
        {
            exception.Should().Match<Exception>(e => e is ScriptPreparationException || e is InsufficientExecutionStackException);
        }
        else
        {
            exception.Should().BeOfType<ScriptPreparationException>();
        }
    }, maxStackSize: DedicatedThread.DefaultStackSize);

    // The interpreter builds a function's nodes once, where the function first runs: at the top of the stack
    // here. It then runs again from the bottom of a recursion that spent the stack down to the guard's
    // RangeError. Evaluating a deep chain recurses once per operand, more than the stack the guard leaves, so a
    // deep chain carries probes of its own; without them this was a native stack overflow.
    [TestCase("||")]
    [TestCase("&&")]
    [TestCase("??")]
    [TestCase("+")]
    [TestCase("-")]
    public void ADeepChainCalledFromTheBottomOfTheStackThrowsARangeErrorTheScriptCanCatch(string op) => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute($$"""
            function f() { return ({{Chain(op, 1000)}}); }
            var first = f();
            var caught = 0;
            function dive(d) {
                try { return dive(d + 1); }
                catch (e) { if (e instanceof RangeError) { caught++; } return f(); }
            }
            var r = dive(0);
            """);

        engine.GetValue("first").AsNumber().Should().Be(ChainValue(op, 1000));
        engine.GetValue("r").AsNumber().Should().Be(ChainValue(op, 1000));
        engine.GetValue("caught").AsNumber().Should().BeGreaterThan(0);
    }, maxStackSize: DeepCallStackSize);

    [Test]
    public void ADeepElseIfChainCalledFromTheBottomOfTheStackThrowsARangeErrorTheScriptCanCatch() => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute(ElseIfChainProxyAutoConfig(1000) + """
            var host = "h999.example";
            var first = FindProxyForURL("u", host);
            var caught = 0;
            function dive(d) {
                try { return dive(d + 1); }
                catch (e) { if (e instanceof RangeError) { caught++; } return FindProxyForURL("u", host); }
            }
            var r = dive(0);
            """);

        engine.GetValue("first").AsString().Should().Be("PROXY p999.example:8080");
        engine.GetValue("r").AsString().Should().Be("PROXY p999.example:8080");
        engine.GetValue("caught").AsNumber().Should().BeGreaterThan(0);
    }, maxStackSize: DeepCallStackSize);

    // Generators and async functions resume by replaying the expression they suspended in, through every
    // operator node between the statement and the yield or await, including the probes a deep chain carries.

    [TestCase("||")]
    [TestCase("+")]
    public void AGeneratorResumesInsideADeepChain(string op) => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        var chain = ChainWithOperandNear(op, 300, "(yield 'paused')", atOperand: 3);
        engine.Execute($"function* g() {{ return ({chain}); }} var it = g(); var first = it.next(); var last = it.next({ResumeValue(op)});");

        engine.Evaluate("first.value").AsString().Should().Be("paused");
        engine.Evaluate("last.done").AsBoolean().Should().BeTrue();
        engine.Evaluate("last.value").AsNumber().Should().Be(ChainValue(op, 300));
    });

    [TestCase("||")]
    [TestCase("+")]
    public void AnAsyncFunctionResumesInsideADeepChain(string op) => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        var chain = ChainWithOperandNear(op, 300, $"(await Promise.resolve({ResumeValue(op)}))", atOperand: 3);
        engine.Execute($"async function a() {{ return ({chain}); }}");

        engine.Evaluate("a()").UnwrapIfPromise().AsNumber().Should().Be(ChainValue(op, 300));
    });

    [Test]
    public void AGeneratorResumesAtTheEndOfALongElseIfChain() => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute($"function* g(h) {{ {ElseIfChainEndingIn(300, "{ var x = yield 'paused'; return x + 1; }")} }}");
        engine.Execute("var it = g('h299'); var first = it.next(); var last = it.next(41);");

        engine.Evaluate("first.value").AsString().Should().Be("paused");
        engine.Evaluate("last.value").AsNumber().Should().Be(42);
    });

    [Test]
    public void AnAsyncFunctionResumesAtTheEndOfALongElseIfChain() => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute($"async function a(h) {{ {ElseIfChainEndingIn(300, "{ var x = await Promise.resolve(41); return x + 1; }")} }}");

        engine.Evaluate("a('h299')").UnwrapIfPromise().AsNumber().Should().Be(42);
    });

    // https://tc39.es/ecma262/#sec-static-semantics-varscopeddeclarations: an expression declares nothing in
    // the scope it stands in, which is why the hoisting walk no longer descends into one. What is declared
    // inside an expression belongs to the function, arrow or class it is nested in, and stays there.

    // Each of these puts the nested scope at the bottom of the chain: the leftmost operand is the deepest node.

    [Test]
    public void AVarInABlockOfAnArrowInsideAnOrChainStaysInTheArrow()
    {
        var engine = new Engine();
        var arrow = "(() => { { var inner = 'arrow'; } return inner; })()";
        engine.Execute($"var seen = {ChainWithOperandNear("||", 300, arrow, atOperand: 0)};");

        engine.Evaluate("seen").AsString().Should().Be("arrow");
        engine.Evaluate("typeof inner").AsString().Should().Be("undefined");
    }

    [Test]
    public void ABlockFunctionInAFunctionExpressionInsideAChainHoistsIntoThatFunctionOnly()
    {
        var engine = new Engine();

        // Annex B gives the block function a var binding in the function expression's scope, not in this one.
        var function = "function () { { function annexB() { return 'b'; } } return typeof annexB; }";
        engine.Execute($"var g = {ChainWithOperandNear("||", 300, function, atOperand: 0)};");

        engine.Evaluate("g()").AsString().Should().Be("function");
        engine.Evaluate("typeof annexB").AsString().Should().Be("undefined");
    }

    [Test]
    public void AStaticBlockOfAClassExpressionInsideAChainIsItsOwnVarScope()
    {
        var engine = new Engine();

        // https://tc39.es/ecma262/#sec-runtime-semantics-classstaticblockdefinitionevaluation
        var classExpression = "class { static { var fromStatic = 1; function fromStaticFn() {} } }";
        engine.Execute($"var C = {ChainWithOperandNear("||", 300, classExpression, atOperand: 0)};");

        engine.Evaluate("typeof C + ',' + typeof fromStatic + ',' + typeof fromStaticFn").AsString().Should().Be("function,undefined,undefined");
    }

    [Test]
    public void DeclarationsInStatementsGuardedByADeepChainStillHoist()
    {
        var engine = new Engine();
        engine.Execute($$"""
            var hadVar = 'late' in globalThis;
            var hadAnnexB = 'annexLate' in globalThis;
            if ({{Chain("||", 300)}}) { var late = 1; { function annexLate() { return 'late'; } } }
            """);

        // https://tc39.es/ecma262/#sec-globaldeclarationinstantiation creates both bindings before the first
        // statement runs; the Annex B one is assigned when its declaration is evaluated.
        engine.Evaluate("hadVar + ',' + hadAnnexB").AsString().Should().Be("true,true");
        engine.Evaluate("late + ',' + annexLate()").AsString().Should().Be("1,late");
    }

    [Test]
    public void AFunctionReachesItsArgumentsObjectFromTheBottomOfADeepChain()
    {
        var engine = new Engine();

        // Whether the function needs an arguments object at all is decided by a walk over its body.
        engine.Execute($"function f() {{ return {ChainWithOperandNear("||", 300, "arguments[0]", atOperand: 0)}; }}");

        engine.Evaluate("f(7)").AsNumber().Should().Be(7);
    }

    [Test]
    public void ADirectEvalAtTheBottomOfADeepChainDeclaresIntoTheFunction()
    {
        var engine = new Engine();

        // So is whether it needs an environment a direct eval can declare into.
        engine.Execute($"function f() {{ {ChainWithOperandNear("||", 300, "eval('var e = 5')", atOperand: 0)}; return e; }}");

        // https://tc39.es/ecma262/#sec-evaldeclarationinstantiation puts the var in the function's environment.
        engine.Evaluate("f()").AsNumber().Should().Be(5);
        engine.Evaluate("typeof e").AsString().Should().Be("undefined");
    }

    [Test]
    public void ALexicalDeclarationWithADeepInitializerIsInTheScriptScope()
    {
        var engine = new Engine();
        engine.Execute($"let lexical = {Chain("||", 300)}; {{ let blockOnly = 1; }}");

        engine.Evaluate("lexical + ',' + typeof blockOnly").AsString().Should().Be("1,undefined");
    }

    [Test]
    public void AModuleFindsItsExportsAndATopLevelAwaitAtTheBottomOfADeepChain()
    {
        var engine = new Engine();

        // The await is the deepest operand: finding it is what makes the module asynchronous.
        var chain = ChainWithOperandNear("||", 300, "(await Promise.resolve(0))", atOperand: 0);
        engine.Modules.Add("deep", $"export const v = {chain};");

        engine.Modules.Import("deep").Get("v").AsNumber().Should().Be(1);
    }

    [Test]
    public void AModuleWithAHundredThousandOperandChainFailsCleanly() => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Modules.Add("deep", $"export const v = ({Chain("-", 100_000)});");

        // The import and export collection used to walk every expression of a module, unprobed, and ended the
        // process on this before anything else could look at it.
        var exception = Caught.Exception(() => engine.Modules.Import("deep"));
        exception.Should().BeOfType<ScriptPreparationException>();
    }, maxStackSize: DedicatedThread.DefaultStackSize);

    [Test]
    public void TheHoistingWalkFailsCleanlyOnAStatementNestingDeeperThanItsStack()
    {
        // Parsed on a stack large enough for the parser, and analysed nowhere: with StaticAnalysis off the
        // hoisting walk runs when the script is evaluated, on a stack far too small for this nesting.
        Prepared<Script> prepared = default;
        DedicatedThread.Run(
            () => prepared = Engine.PrepareScript(ElseIfChainScript(50_000), options: new ScriptPreparationOptions { StaticAnalysis = false }),
            maxStackSize: 64 * 1024 * 1024);

        DedicatedThread.Run(() =>
        {
            var exception = Caught.Exception(() => new Engine().Execute(prepared));
            exception.Should().BeOfType<ScriptPreparationException>();
        }, maxStackSize: 4 * 1024 * 1024);
    }

    [Test]
    public void TheFuzzedUnaryChainOfIssue2339FailsCleanly() => DedicatedThread.Run(() =>
    {
        // crash_003.js from sebastienros/jint#2339, which ended the process in the hoisting walk: a for loop
        // around 24 600 nested `~`. The parser recurses on a unary operator, so on a small stack it is the
        // parser that refuses, with the runtime's exception type.
        var script = "for(var i=6;i^10;i++){ii=10||1111E11111111-1;i^7011/1&&11101111?.Xar.t220n=== " + new string('~', 24_600) + "10;;}";

        var exception = Caught.Exception(() => new Engine().Execute(script));
        exception.Should().Match<Exception>(e => e is ScriptPreparationException || e is InsufficientExecutionStackException);
    }, maxStackSize: DedicatedThread.DefaultStackSize);

    private static void RunThrough(Engine engine, string entry, string script)
    {
        switch (entry)
        {
            case "Evaluate":
                engine.Evaluate(script);
                break;
            case "Execute":
                engine.Execute(script);
                break;
            case "PrepareScript":
                engine.Execute(Engine.PrepareScript(script));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entry), entry, null);
        }
    }

    private static string ChainScript(string op, int operands, string position) => position switch
    {
        "script" => $"var r = ({Chain(op, operands)});",
        "function" => $"function f() {{ return ({Chain(op, operands)}); }} var r = f();",
        "eval" => $"var r = eval('({Chain(op, operands)})');",
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
    };

    /// <summary>
    /// A left-deep chain of <paramref name="operands"/> operands, as the parser builds it from the flat text.
    /// Every operand but the last leaves the chain undecided, so evaluation reaches the bottom of the tree.
    /// </summary>
    private static string Chain(string op, int operands) => ChainWithOperandNear(op, operands, operand: null, atOperand: -1);

    private static string ChainWithOperandNear(string op, int operands, string? operand, int atOperand)
    {
        var (filler, last) = op switch
        {
            "||" => ("0", "1"),
            "&&" => ("1", "1"),
            "??" => ("null", "1"),
            "+" or "-" => ("1", "1"),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
        };

        var builder = new StringBuilder(operands * (filler.Length + op.Length + 2));
        for (var i = 0; i < operands; i++)
        {
            if (i > 0)
            {
                builder.Append(' ').Append(op).Append(' ');
            }

            builder.Append(i == atOperand ? operand : i == operands - 1 ? last : filler);
        }

        return builder.ToString();
    }

    /// <summary>What a script hands back to stand in for a filler operand, so the chain's value is unchanged.</summary>
    private static string ResumeValue(string op) => op == "||" ? "0" : "1";

    private static double ChainValue(string op, int operands) => op switch
    {
        "||" or "&&" or "??" => 1,
        "+" => operands,
        "-" => 1 - (operands - 1),
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    private static string OrChainProxyAutoConfig(int terms)
    {
        var builder = new StringBuilder("function shExpMatch(host, pattern) { return host === pattern; }\n");
        builder.Append("function FindProxyForURL(url, host) {\n    if (");
        for (var i = 0; i < terms; i++)
        {
            if (i > 0)
            {
                builder.Append(" ||\n        ");
            }

            builder.Append("shExpMatch(host, \"h").Append(i).Append(".example\")");
        }

        builder.Append(")\n        return \"PROXY proxy.example:8080\";\n    return \"DIRECT\";\n}\n");
        return builder.ToString();
    }

    private static string ElseIfChainProxyAutoConfig(int arms)
    {
        var builder = new StringBuilder("function FindProxyForURL(url, host) {\n");
        for (var i = 0; i < arms; i++)
        {
            builder.Append(i == 0 ? "    if" : "    else if");
            builder.Append(" (host === \"h").Append(i).Append(".example\") return \"PROXY p").Append(i).Append(".example:8080\";\n");
        }

        builder.Append("    else return \"DIRECT\";\n}\n");
        return builder.ToString();
    }

    /// <summary>An <c>else if</c> chain over <c>h</c> whose last arm, and only that one, runs <paramref name="lastArm"/>.</summary>
    private static string ElseIfChainEndingIn(int arms, string lastArm)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < arms; i++)
        {
            builder.Append(i == 0 ? "if" : " else if").Append(" (h === 'h").Append(i).Append("') ");
            builder.Append(i == arms - 1 ? lastArm : "return " + i + ";");
        }

        return builder.ToString();
    }

    private static string ElseIfChainScript(int arms)
    {
        var builder = new StringBuilder("var host = 'none', r;\n");
        for (var i = 0; i < arms; i++)
        {
            builder.Append(i == 0 ? "if" : "else if").Append(" (host === 'h").Append(i).Append("') r = ").Append(i).Append(";\n");
        }

        return builder.ToString();
    }
}
