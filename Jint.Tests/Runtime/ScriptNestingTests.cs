#nullable enable

using System.Text;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// How deep a script's syntax tree may nest (sebastienros/jint#4356). Generated code is deep without being
/// malformed: a proxy auto-config file is one long <c>||</c> chain or one long <c>else if</c> chain, and the
/// parser builds a left-associative chain in a loop, one level of tree per operand. Those two chains are bounded by
/// the stack — probed where the tree is walked and built, and every 64 levels where it runs — and no longer count
/// against the 256-level limit, which still bounds every other kind of nesting. A tree too deep for the stack
/// fails with a <see cref="ScriptPreparationException"/>, a <c>RangeError</c> or the parser's own
/// <see cref="InsufficientExecutionStackException"/>, never with a native stack overflow, which ends the process
/// and which no <c>catch</c> survives: a regression here is a dead test host, not a red test.
/// </summary>
public class ScriptNestingTests
{
    // Every operator the chains below are made of, the three places a chain can stand, and the three entries
    // that run a script.
    private static readonly string[] Operators = ["||", "&&", "??", "+", "-"];
    private static readonly string[] Positions = ["script", "function", "eval"];
    private static readonly string[] Entries = ["Evaluate", "Execute", "PrepareScript"];

    // Stated rather than the platform default, which is 512 KB for a secondary thread on macOS and 8 MB on Linux:
    // room to build a thousand levels, and few enough frames that recursing to the bottom of it stays quick.
    private const int DeepCallStackSize = 2 * 1024 * 1024;

    // A stack no hundred-thousand-level tree fits on, whatever the platform's frame sizes: stated for the same
    // reason, so that a test meant to fail cleanly fails on every operating system and runtime.
    private const int SmallStackSize = 1024 * 1024;

    // SyntaxNesting.EvaluationProbeInterval, which the tests below align on.
    private const int SyntaxNestingProbeInterval = 64;

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

        // Which clean failure it is depends on where the chain is first walked, never on the platform. A build or
        // an analysis outside running script — the script's own statements, or a function analysed at
        // preparation — throws ScriptPreparationException. The same failure inside running script, a function
        // analysed when it is first called, is a RangeError the host sees as a JavaScriptException. A direct
        // eval's source is first walked by an Acornima visitor, which probes with the runtime's exception type.
        var expected = position switch
        {
            "eval" => typeof(InsufficientExecutionStackException),
            "function" when entry != "PrepareScript" => typeof(JavaScriptException),
            _ => typeof(ScriptPreparationException),
        };

        exception.Should().BeOfType(expected);
        if (exception is JavaScriptException javaScriptException)
        {
            javaScriptException.Error.AsObject().Get("name").ToString().Should().Be("RangeError");
        }
    }, maxStackSize: SmallStackSize);

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
    }, maxStackSize: SmallStackSize);

    [Test]
    public void TheHoistingWalkFailsCleanlyOnAChainDeeperThanItsStack()
    {
        // Parsed on a stack large enough for the parser, and analysed nowhere: with StaticAnalysis off the
        // hoisting walk runs when the script is evaluated, on a stack far too small for this chain. A chain
        // nested to the right is the parser's own recursion, so the walk recurses on it too.
        var chain = string.Concat(Enumerable.Repeat("0 || (", 30_000)) + "1" + new string(')', 30_000);
        Prepared<Script> prepared = default;
        DedicatedThread.Run(
            () => prepared = Engine.PrepareScript("var r = " + chain + ";", options: new ScriptPreparationOptions { StaticAnalysis = false }),
            maxStackSize: 256 * 1024 * 1024);

        DedicatedThread.Run(() =>
        {
            var exception = Caught.Exception(() => new Engine().Execute(prepared));
            exception.Should().BeOfType<ScriptPreparationException>().Which.Message.Should().Contain("too deep");
        }, maxStackSize: SmallStackSize);
    }

    [Test]
    public void AnElseIfChainLongerThanTheStackFailsCleanlyWhenItRuns()
    {
        // The walk and the build follow an `else if` in a loop, so a chain the parser accepts is built, and it is
        // running it, one frame per arm, that the stack bounds: the chain's own probes end it with a RangeError.
        Prepared<Script> prepared = default;
        DedicatedThread.Run(
            () => prepared = Engine.PrepareScript(ElseIfChainScript(50_000), options: new ScriptPreparationOptions { StaticAnalysis = false }),
            maxStackSize: 64 * 1024 * 1024);

        DedicatedThread.Run(() =>
        {
            var exception = Caught.Exception(() => new Engine().Execute(prepared));
            exception.Should().BeOfType<JavaScriptException>().Which.Message.Should().Be("Maximum call stack size exceeded");
        }, maxStackSize: SmallStackSize);
    }

    [Test]
    public void TheFuzzedUnaryChainOfIssue2339IsRefusedByTheParserOnASmallStack() => DedicatedThread.Run(() =>
    {
        // crash_003.js from sebastienros/jint#2339, which ended the process in the hoisting walk: a for loop
        // around 24 600 nested `~`. The parser recurses on a unary operator, so on a small stack it is the
        // parser that refuses, with the runtime's exception type.
        var exception = Caught.Exception(() => new Engine().Execute(FuzzedUnaryChain));
        exception.Should().BeOfType<InsufficientExecutionStackException>();
    }, maxStackSize: SmallStackSize);

    [Test]
    public void TheFuzzedUnaryChainOfIssue2339IsRefusedByTheNestingLimitOnALargeStack() => DedicatedThread.Run(() =>
    {
        // Where the parser has the stack for it, the walk refuses it: a unary chain carries no evaluation probe,
        // so it counts against the fixed limit, as it always did.
        var exception = Caught.Exception(() => new Engine().Execute(FuzzedUnaryChain));
        exception.Should().BeOfType<ScriptPreparationException>().Which.Message.Should().Contain("maximum depth of 256 levels");
    });

    // Nesting with no evaluation probe — member, call, optional and tagged-template chains, unary operators,
    // literals, call arguments, nested blocks — evaluates by a recursion nothing probes, so it keeps the limit of
    // sebastienros/jint#2340 exactly as it was: what ran before runs, and what was refused is refused, never left
    // to overflow the native stack from the bottom of a deep recursion.
    // An object literal and an if-block each nest two tree levels at a time.
    [TestCase("o.b", 200, 300)]
    [TestCase("c()", 200, 300)]
    [TestCase("o?.b", 200, 300)]
    [TestCase("t``", 200, 300)]
    [TestCase("~", 200, 300)]
    [TestCase("[", 200, 300)]
    [TestCase("g(", 200, 300)]
    [TestCase("{a:", 100, 150)]
    [TestCase("if(true){", 100, 150)]
    public void NestingWithoutAProbeKeepsTheFixedLimit(string level, int runs, int refused) => DedicatedThread.Run(() =>
    {
        new Engine().Evaluate(UnprobedNesting(level, runs));

        var exception = Caught.Exception(() => new Engine().Evaluate(UnprobedNesting(level, refused)));
        exception.Should().BeOfType<ScriptPreparationException>().Which.Message.Should().Contain("maximum depth of 256 levels");
    });

    // With MaxExecutionStackCount set, a call that finds the stack low continues on a fresh stack rather than
    // throwing, and a deep chain's probe has to defer to that lane the same way. The chain is within the old
    // limit, so this ran before the probes existed; a probe that threw instead would meet a low stack on most of
    // the calls between two hops, since it sits three intervals below the function's entry.
    [Test]
    public void TheStackHoppingLaneCarriesARecursionThroughADeepChain() => DedicatedThread.Run(() =>
    {
        var engine = new Engine(options => options.Constraints.MaxExecutionStackCount = 100_000);
        var chain = ChainWithOperandNear("||", 240, "(r(n - 1) + 1)", atOperand: 0);
        engine.Execute($"function r(n) {{ if (n === 0) return 0; return {chain}; }}");

        engine.Evaluate("r(60)").AsNumber().Should().Be(60);
    }, maxStackSize: SmallStackSize);

    // An object literal's values are built when it is first evaluated, which here is in a catch block at the
    // bottom of a recursion that spent the stack down to the guard's reserve. A build of fifty levels has always
    // run inside that reserve, and must go on running.
    [Test]
    public void ABuildThatFitsTheReserveRunsAtTheBottomOfTheStack() => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute($$"""
            function dive(d) {
                try { return dive(d + 1); }
                catch (e) { return { v: ({{Chain("||", 50)}}) }.v; }
            }
            var r = dive(0);
            """);

        engine.GetValue("r").AsNumber().Should().Be(1);
    }, maxStackSize: DeepCallStackSize);

    // A build too deep for the stack that runs while script runs fails with a RangeError the script can catch, as
    // V8's lazy compilation does. Here it is the walks over a function's body when the script first calls it.
    [Test]
    public void ABuildTooDeepForTheStackThrowsARangeErrorTheScriptCanCatch() => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute($"function f() {{ return ({Chain("||", 100_000)}); }} var caught; try {{ f(); }} catch (e) {{ caught = e instanceof RangeError; }}");

        engine.GetValue("caught").AsBoolean().Should().BeTrue();
    }, maxStackSize: SmallStackSize);

    // Where a throw from a function becomes a rejection, so does a function too deep to build: native code that runs
    // script catches a JavaScript exception, never the ScriptPreparationException underneath it.
    [Test]
    public void APromiseExecutorTooDeepToBuildRejectsThePromise() => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute($"var caught; new Promise(function (resolve) {{ resolve({Chain("||", 100_000)}); }}).catch(function (e) {{ caught = e instanceof RangeError; }});");

        engine.GetValue("caught").AsBoolean().Should().BeTrue();
    }, maxStackSize: SmallStackSize);

    [Test]
    public void AnAsyncFunctionTooDeepToBuildRejectsItsPromise() => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute($"async function f() {{ return ({Chain("||", 100_000)}); }} var caught; f().catch(function (e) {{ caught = e instanceof RangeError; }});");

        engine.GetValue("caught").AsBoolean().Should().BeTrue();
    }, maxStackSize: SmallStackSize);

    // A host callback that runs another script is a separate entry into the engine: what that script's preparation
    // throws is the host's exception, and passes through the calling script uncaught, as it always did.
    [Test]
    public void AScriptTooDeepToPrepareRunByAHostCallbackFailsToTheHost() => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        var deep = $"var r = ({Chain("-", 100_000)});";
        engine.SetValue("runDeep", new Action(() => engine.Execute(deep)));

        var exception = Caught.Exception(() => engine.Execute("var caught = false; try { runDeep(); } catch (e) { caught = true; }"));

        exception.Should().BeOfType<ScriptPreparationException>();
        engine.GetValue("caught").AsBoolean().Should().BeFalse();
    }, maxStackSize: SmallStackSize);

    // A function's body is analysed and built when the function is first called, whoever calls it: a host calling it
    // through Invoke or Call gets the RangeError a script would, as a JavaScriptException, each time it tries.
    [Test]
    public void AFunctionTooDeepToBuildThrowsARangeErrorWhenTheHostCallsIt() => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute($"function f() {{ return ({Chain("||", 100_000)}); }}");

        var invoked = Caught.Exception(() => engine.Invoke("f"));
        var called = Caught.Exception(() => engine.GetValue("f").Call());

        invoked.Should().BeOfType<JavaScriptException>().Which.Message.Should().Be("Maximum call stack size exceeded");
        called.Should().BeOfType<JavaScriptException>().Which.Message.Should().Be("Maximum call stack size exceeded");
    }, maxStackSize: SmallStackSize);

    // Reading a function's length analyses its body as a call does, and fails the same way, read by script or host.
    [Test]
    public void ReadingTheLengthOfAFunctionTooDeepToAnalyseThrowsARangeError() => DedicatedThread.Run(() =>
    {
        var engine = new Engine();
        engine.Execute($"function f(a, b) {{ return ({Chain("||", 100_000)}); }} function g(a) {{ return 1; }}");

        var exception = Caught.Exception(() => engine.GetValue("f").AsObject().Get("length"));

        exception.Should().BeOfType<JavaScriptException>().Which.Message.Should().Be("Maximum call stack size exceeded");
        engine.Evaluate("var caught; try { f.length; } catch (e) { caught = e instanceof RangeError; } caught").AsBoolean().Should().BeTrue();
        engine.Evaluate("g.length").AsNumber().Should().Be(1);
    }, maxStackSize: SmallStackSize);

    // A tree too deep for the stack reaches the host as the exception main threw for one too deep to walk: a
    // ScriptPreparationException with no inner exception, carrying the stack trace of the walk that ran out.
    [Test]
    public void AScriptTooDeepToPrepareFailsToTheHostWithTheStackTraceOfTheWalk() => DedicatedThread.Run(() =>
    {
        var exception = Caught.Exception(() => new Engine().Execute($"var r = ({Chain("-", 100_000)});"));

        var preparation = exception.Should().BeOfType<ScriptPreparationException>().Which;
        preparation.InnerException.Should().BeNull();
        SyntaxNesting.IsTooDeep(preparation).Should().BeFalse();
        preparation.StackTrace.Should().Contain(nameof(SyntaxNesting.ThrowTooDeep));
    }, maxStackSize: SmallStackSize);

    // Engine.PrepareScript and PrepareModule analyse every function up front, and wrap what fails there: the inner
    // exception is the walk's, unmarked, as main's was.
    [Test]
    public void PreparingAScriptWithAFunctionTooDeepToAnalyseFailsAsItAlwaysDid() => DedicatedThread.Run(() =>
    {
        var function = $"function f() {{ return ({Chain("||", 100_000)}); }}";

        AssertPreparationFailure(Caught.Exception(() => Engine.PrepareScript(function)));
        AssertPreparationFailure(Caught.Exception(() => Engine.PrepareModule("export " + function)));

        static void AssertPreparationFailure(Exception? exception)
        {
            var preparation = exception.Should().BeOfType<ScriptPreparationException>().Which;
            preparation.Message.Should().StartWith("Could not prepare script: Script nesting is too deep");
            var walk = preparation.InnerException.Should().BeOfType<ScriptPreparationException>().Which;
            walk.InnerException.Should().BeNull();
            SyntaxNesting.IsTooDeep(walk).Should().BeFalse();
        }
    }, maxStackSize: SmallStackSize);

    // The probe that ends a chain's first interval is not placed when that level is the chain's last, so the walk
    // counts all of the first interval as unprobed. Here the chain's last level is exactly the 64th, under and over
    // 150 levels of member reads, at every alignment the build depth can give it: what main refused is refused.
    [Test]
    public void AChainEndingOnItsFirstProbePositionCountsItsWholeFirstInterval() => DedicatedThread.Run(() =>
    {
        var below = "o" + string.Concat(Enumerable.Repeat(".b", 150));
        for (var above = 150; above < 150 + SyntaxNestingProbeInterval; above++)
        {
            var chain = below + string.Concat(Enumerable.Repeat(" || 0", SyntaxNestingProbeInterval + 1));
            var script = "var o = {}; o.b = o; var r = (" + chain + ")" + string.Concat(Enumerable.Repeat(".b", above)) + ";";

            var exception = Caught.Exception(() => new Engine().Execute(script));
            exception.Should().BeOfType<ScriptPreparationException>().Which.Message.Should().Contain("maximum depth of 256 levels");
        }
    });

    // A flattened `+` chain evaluates every operand from its first node, past where its nested form would carry
    // probes, so a chain that long carries a probe of its own. Here a deep operand sits near the start of a
    // hundred-operand chain under 180 levels of other nesting, which the walk allows. Without that probe the path down
    // to it runs about 370 nodes with no probe among them, against the 256 the walk counts on, and from the bottom of
    // a recursion that ends the process. The tree is read rather than run from the bottom of the stack, so the test
    // does not depend on the size of the frames it would recurse through.
    [Test]
    public void ALongFlattenedConcatenationKeepsEveryRunOfUnprobedNodesWithinTheLimit() => DedicatedThread.Run(() =>
    {
        var deepOperand = "o" + string.Concat(Enumerable.Repeat(".b", 185)) + ".s";
        var concat = ChainWithOperandNear("+", 100, deepOperand, atOperand: 5).Replace("1 + ", "s + ").Replace(" + 1", " + s");
        var nested = string.Concat(Enumerable.Repeat("0 ? 0 : ", 180)) + "(" + concat + ")";

        var (result, longestUnprobedRun) = RunAndMeasureTheDeepOperand(nested);

        result.Length.Should().Be(100);
        longestUnprobedRun.Should().BeLessThanOrEqualTo(SyntaxNesting.MaxUnprobedDepth);
    }, maxStackSize: DeepCallStackSize);

    // The same lane in a short chain: forty operands ending a thirty-level `||` run. The walk counts the
    // concatenation's nested additions as more levels of that run, past where the run would carry its next probe, but
    // the flattened lane has none of those levels, so the chain needs a probe of its own whenever its nested form
    // would have carried one. Where that falls depends on the depth the run starts at, so every alignment of the
    // interval is tried; without the probe, about half of them run some 340 nodes unprobed.
    [Test]
    public void AShortFlattenedConcatenationEndingALongerChainKeepsEveryRunOfUnprobedNodesWithinTheLimit() => DedicatedThread.Run(() =>
    {
        var deepOperand = "o" + string.Concat(Enumerable.Repeat(".b", 185)) + ".s";
        var run = deepOperand + string.Concat(Enumerable.Repeat(" + s", 39)) + string.Concat(Enumerable.Repeat(" || 0", 30));
        for (var conditionals = 120; conditionals < 120 + SyntaxNestingProbeInterval; conditionals++)
        {
            var nested = string.Concat(Enumerable.Repeat("0 ? 0 : ", conditionals)) + "(" + run + ")";

            var (result, longestUnprobedRun) = RunAndMeasureTheDeepOperand(nested);

            result.Length.Should().Be(40);
            longestUnprobedRun.Should().BeLessThanOrEqualTo(SyntaxNesting.MaxUnprobedDepth, $"{conditionals} conditionals");
        }
    }, maxStackSize: DeepCallStackSize);

    // A flattened chain whose first operand is itself an operator chain, a thirty-level `||`: the walk counts that
    // chain as more levels of the `+` chain it ends, so its probes have to sit where the nested tree put them. The
    // flattened lane builds its own copy of every operand when it first runs, from the top of a fresh build, so that
    // copy is built as though at the depth of the nested tree's copy. Every alignment of the interval is tried; built
    // from the top instead, the copy carried no probe at about a third of them, and some 360 nodes ran unprobed.
    [Test]
    public void AFlattenedConcatenationWhoseFirstOperandIsAnOperatorChainKeepsEveryRunOfUnprobedNodesWithinTheLimit() => DedicatedThread.Run(() =>
    {
        var operatorChain = "(o" + string.Concat(Enumerable.Repeat(".b", 185)) + ".s" + string.Concat(Enumerable.Repeat(" || 0", 30)) + ")";
        var concat = operatorChain + string.Concat(Enumerable.Repeat(" + s", 39));
        for (var conditionals = 120; conditionals < 120 + SyntaxNestingProbeInterval; conditionals++)
        {
            var nested = string.Concat(Enumerable.Repeat("0 ? 0 : ", conditionals)) + "(" + concat + ")";

            var (result, longestUnprobedRun) = RunAndMeasureTheDeepOperand(nested);

            result.Length.Should().Be(40);
            longestUnprobedRun.Should().BeLessThanOrEqualTo(SyntaxNesting.MaxUnprobedDepth, $"{conditionals} conditionals");
        }
    }, maxStackSize: DeepCallStackSize);

    /// <summary>
    /// Runs <c>f</c>, returning <paramref name="expression"/>, once, so that its tree is built; then measures every
    /// path through that tree down to a read of <c>o</c>, of which a flattened chain holds two, one per lane: the most
    /// interpreter nodes in a row on any of them with no stack probe among them, which is how far evaluating it
    /// recurses between two probes.
    /// </summary>
    private static (string Result, int LongestUnprobedRun) RunAndMeasureTheDeepOperand(string expression)
    {
        var engine = new Engine();
        engine.Execute($$"""
            var s = "x"; var o = { s: "y" }; o.b = o;
            function f() { return {{expression}}; }
            var r = f();
            """);

        var definition = ((Jint.Native.Function.ScriptFunction) engine.GetValue("f"))._functionDefinition!;
        var onPath = new HashSet<object>(new ReferenceIdentity());
        var withoutRead = new HashSet<object>(new ReferenceIdentity());
        var longest = LongestUnprobedRun(definition, unprobed: 0, longestSoFar: 0, onPath, withoutRead);
        if (longest < 0)
        {
            throw new InvalidOperationException("No read of o was found in f's tree.");
        }

        return (engine.GetValue("r").AsString(), longest);
    }

    // The longest unprobed run on any path from node to a read of o, or -1 when there is none below it. Every path is
    // taken, not the first one the fields happen to lead to; a subtree with no read in it is remembered and skipped.
    private static int LongestUnprobedRun(object node, int unprobed, int longestSoFar, HashSet<object> onPath, HashSet<object> withoutRead)
    {
        if (withoutRead.Contains(node) || !onPath.Add(node))
        {
            return -1;
        }

        if (node is Jint.Runtime.Interpreter.Expressions.JintStackProbeExpression or Jint.Runtime.Interpreter.Statements.JintStackProbeStatement)
        {
            unprobed = 0;
        }
        else if (node is Jint.Runtime.Interpreter.Expressions.JintExpression or Jint.Runtime.Interpreter.Statements.JintStatement)
        {
            unprobed++;
        }

        longestSoFar = Math.Max(longestSoFar, unprobed);
        var longest = node is Jint.Runtime.Interpreter.Expressions.JintExpression { _expression: Identifier { Name: "o" } } ? longestSoFar : -1;

        var children = new List<object>();
        CollectInterpreterNodes(node, children, structDepth: 0);
        foreach (var child in children)
        {
            longest = Math.Max(longest, LongestUnprobedRun(child, unprobed, longestSoFar, onPath, withoutRead));
        }

        onPath.Remove(node);
        if (longest < 0)
        {
            withoutRead.Add(node);
        }

        return longest;
    }

    // The interpreter's objects a node holds: its operands, statements and wrappers, whether in a field, an array or a
    // struct, and nothing of the runtime's.
    private static void CollectInterpreterNodes(object holder, List<object> found, int structDepth)
    {
        for (var type = holder.GetType(); type is not null && type != typeof(object) && type != typeof(ValueType); type = type.BaseType)
        {
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (!field.FieldType.IsPrimitive && !field.FieldType.IsPointer && !field.FieldType.IsEnum)
                {
                    CollectInterpreterNode(field.GetValue(holder), found, structDepth);
                }
            }
        }
    }

    private static void CollectInterpreterNode(object? value, List<object> found, int structDepth)
    {
        if (value is null)
        {
            return;
        }

        if (value is Array array)
        {
            foreach (var element in array)
            {
                CollectInterpreterNode(element, found, structDepth);
            }

            return;
        }

        var type = value.GetType();
        if (type.IsValueType)
        {
            if (!type.IsPrimitive && !type.IsEnum && structDepth < 4)
            {
                CollectInterpreterNodes(value, found, structDepth + 1);
            }

            return;
        }

        if (type.Namespace?.StartsWith("Jint.Runtime.Interpreter", StringComparison.Ordinal) == true)
        {
            found.Add(value);
        }
    }

    private sealed class ReferenceIdentity : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private const string FuzzedUnaryChainPrefix = "for(var i=6;i^10;i++){ii=10||1111E11111111-1;i^7011/1&&11101111?.Xar.t220n=== ";

    private static readonly string FuzzedUnaryChain = FuzzedUnaryChainPrefix + new string('~', 24_600) + "10;;}";

    /// <summary>
    /// <paramref name="levels"/> levels of one kind of nesting no evaluation probe covers, around a value.
    /// </summary>
    private static string UnprobedNesting(string level, int levels)
    {
        var prelude = "var o = {}; o.b = o; function c() { return c; } function t() { return t; } function g(x) { return x; }\n";
        var builder = new StringBuilder(prelude);
        switch (level)
        {
            case "o.b" or "c()" or "o?.b" or "t``":
                var root = level.Substring(0, 1);
                builder.Append(root);
                for (var i = 0; i < levels; i++)
                {
                    builder.Append(level, 1, level.Length - 1);
                }

                builder.Append(';');
                break;
            case "~":
                builder.Append('~', levels).Append("1;");
                break;
            case "[":
                builder.Append('[', levels).Append(']', levels).Append(';');
                break;
            case "{a:":
                builder.Append('(');
                for (var i = 0; i < levels; i++)
                {
                    builder.Append("{a:");
                }

                builder.Append('1').Append('}', levels).Append(");");
                break;
            case "g(":
                for (var i = 0; i < levels; i++)
                {
                    builder.Append("g(");
                }

                builder.Append('1').Append(')', levels).Append(';');
                break;
            case "if(true){":
                for (var i = 0; i < levels; i++)
                {
                    builder.Append("if(true){");
                }

                builder.Append('1').Append('}', levels);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }

        return builder.ToString();
    }

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
