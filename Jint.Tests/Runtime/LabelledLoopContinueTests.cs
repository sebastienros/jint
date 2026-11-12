#nullable enable

using Jint.Native;
using Jint.Native.Promise;

namespace Jint.Tests.Runtime;

/// <summary>
/// An unlabelled <c>continue</c> inside a labelled loop continues that loop, and a later <c>break</c> naming the label
/// leaves it with the code after it still to run (https://github.com/sebastienros/jint/issues/4568).
/// LoopContinues keeps a loop iterating for a <c>continue</c> with an empty target as well as for one whose target is in
/// the loop's label set (https://tc39.es/ecma262/#sec-loopcontinues), and LabelledEvaluation turns the matching break
/// into a normal completion of the labelled statement (https://tc39.es/ecma262/#sec-runtime-semantics-labelledevaluation).
/// <para>
/// While and do-while compared a <c>continue</c>'s target with their label even when it had none, so an unlabelled
/// <c>continue</c> in <c>a: while (...) { ... }</c> counted as abrupt and left the loop as a Continue completion nothing
/// consumed: a function returned <c>undefined</c> instead of reaching its <c>return</c>, and a script skipped every
/// statement after the loop. The for loops compared correctly, which is why the report names them as the control. Pull
/// request #4557 fixed it for a stack of labels; these tests pin the shapes the report and the TypeScript comment scanner
/// that found it use, in a function, at script level, and across a <c>yield</c> and an <c>await</c>.
/// </para>
/// <para>
/// Every expected value was taken from Node 26.13.
/// </para>
/// </summary>
public class LabelledLoopContinueTests
{
    private static string Run(string script)
    {
        // An engine with the bug can leave a loop spinning forever (a `continue` that should leave it does not), so every
        // script runs under a statement budget far above what any of these needs: a regression fails instead of hanging.
        // A lone statement limit is charged inline, so every loop still takes the path a default engine takes.
        var engine = new Engine(options => options.LimitStatements(100_000));
        var value = engine.Evaluate(script);

        // Evaluate drains the event loop before it returns, so a promise still pending here never settles.
        if (value is JsPromise { State: PromiseState.Pending })
        {
            return "never settled";
        }

        return value.UnwrapIfPromise().ToString();
    }

    // ---- the report, verbatim -------------------------------------------------------------------------------------

    private const string IssueFunctions = """
        function whileLoop() { var i = 0; a: while (i < 3) { if (i < 1) { i++; continue; } break a; } return 'ok'; }
        function forLoop()   { a: for (var i = 0; i < 3; i++) { if (i < 1) continue; break a; } return 'ok'; }
        function doWhile()   { var i = 0; a: do { if (i < 1) { i++; continue; } break a; } while (i < 3); return 'ok'; }
        function forOf()     { a: for (var x of [0, 1, 2]) { if (x < 1) continue; break a; } return 'ok'; }
        function labeledContinue() { var i = 0; a: while (i < 3) { if (i < 1) { i++; continue a; } break a; } return 'ok'; }
        """;

    [Test]
    public void TheReportedScriptReturnsOkFromEveryFunction()
    {
        Run(IssueFunctions + "\nJSON.stringify([whileLoop(), forLoop(), doWhile(), forOf(), labeledContinue()].map(String));")
            .Should().Be("""["ok","ok","ok","ok","ok"]""");
    }

    [TestCase("whileLoop")]
    [TestCase("forLoop")]
    [TestCase("doWhile")]
    [TestCase("forOf")]
    [TestCase("labeledContinue")]
    public void EachReportedFunctionReachesItsReturn(string function)
    {
        Run(IssueFunctions + "\n" + function + "()").Should().Be("ok");
    }

    [Test]
    public void TheReportedScriptLevelLoopRunsTheCodeAfterIt()
    {
        Run("""
            var log = [];
            var j = 0;
            b: while (j < 3) { if (j < 1) { j++; continue; } break b; }
            log.push('after');
            log.join();
            """).Should().Be("after");
    }

    [Test]
    public void ALabelledDoWhileAtScriptLevelRunsTheCodeAfterIt()
    {
        Run("""
            var log = [];
            var j = 0;
            b: do { if (j < 1) { j++; continue; } break b; } while (j < 3);
            log.push('after');
            log.join();
            """).Should().Be("after");
    }

    [Test]
    public void AStackedLabelledWhileAtScriptLevelRunsTheCodeAfterIt()
    {
        Run("""
            var log = [];
            var j = 0;
            a: b: while (j < 3) { if (j < 1) { j++; continue; } break a; }
            log.push('after');
            log.join();
            """).Should().Be("after");
    }

    [Test]
    public void ALabelledWhileThatOnlyContinuesRunsTheCodeAfterIt()
    {
        Run("""
            var log = [];
            var j = 0;
            b: while (j < 3) { j++; continue; }
            log.push('after:' + j);
            log.join();
            """).Should().Be("after:3");
    }

    // ---- the TypeScript comment scanner: scan: while (...) { switch (ch) { ... continue; ... break scan; } } -------

    private static string Scanner(string head, string tail) => $$"""
        function scanComments(text) {
          var pos = 0, out = [];
          {{head}}
            var ch = text.charCodeAt(pos);
            switch (ch) {
              case 32:
              case 9:
                pos++;
                continue;
              case 47:
                if (text.charCodeAt(pos + 1) === 47) {
                  var start = pos;
                  pos += 2;
                  while (pos < text.length && text.charCodeAt(pos) !== 10) pos++;
                  out.push(text.slice(start, pos));
                  continue;
                }
                break scan;
              case 10:
                pos++;
                break;
              default:
                break scan;
            }
            out.push('.');
          {{tail}}
          return out.join('|') + '@' + pos;
        }
        scanComments('  // one\n// two\n  x // three');
        """;

    // The scanner finds both comments and stops at the first token, at offset 18.
    private const string ScannedComments = "// one|.|// two|.@18";

    [Test]
    public void ALabelledWhileSwitchContinuingAndBreakingTheLabelScansTheWholeText()
    {
        Run(Scanner("scan: while (pos < text.length) {", "}")).Should().Be(ScannedComments);
    }

    [Test]
    public void ALabelledDoWhileSwitchContinuingAndBreakingTheLabelScansTheWholeText()
    {
        Run(Scanner("scan: do {", "} while (pos < text.length);")).Should().Be(ScannedComments);
    }

    [Test]
    public void AStackedLabelledWhileSwitchContinuingAndBreakingTheLabelScansTheWholeText()
    {
        Run(Scanner("outer: scan: while (pos < text.length) {", "}")).Should().Be(ScannedComments);
    }

    [Test]
    public void ALabelledForSwitchContinuingAndBreakingTheLabelScansTheWholeText()
    {
        Run(Scanner("scan: for (; pos < text.length; ) {", "}")).Should().Be(ScannedComments);
    }

    [Test]
    public void ALabelledSwitchInsideALabelledWhileKeepsEveryJumpApart()
    {
        // continue leaves the switch and continues the loop, break s only leaves the switch, continue a does what continue
        // does, and break a leaves the loop.
        Run("""
            function f() {
              var i = 0, log = [];
              a: while (i < 6) {
                i++;
                s: switch (i) {
                  case 1: continue;
                  case 2: break s;
                  case 3: continue a;
                  case 4: log.push('four'); break;
                  default: break a;
                }
                log.push('after-switch-' + i);
              }
              log.push('end');
              return log.join();
            }
            f()
            """).Should().Be("after-switch-2,four,after-switch-4,end");
    }

    // ---- nested loops: an inner unlabelled continue continues only the inner loop ------------------------------------

    private static readonly (string Head, string Tail) OuterWhile = ("while (true) { o++;", "}");
    private static readonly (string Head, string Tail) OuterDoWhile = ("do { o++;", "} while (true);");
    private static readonly (string Head, string Tail) OuterFor = ("for (; true; ) { o++;", "}");
    private static readonly (string Head, string Tail) InnerWhile = ("while (j < 4) { j++;", "}");
    private static readonly (string Head, string Tail) InnerDoWhile = ("do { j++;", "} while (j < 4);");

    public static TestCases<string, string> NestedLoops => new()
    {
        { "while", "while" },
        { "while", "do-while" },
        { "do-while", "while" },
        { "do-while", "do-while" },
        { "for", "while" },
        { "for", "do-while" },
    };

    [TestCaseSource(nameof(NestedLoops))]
    public void AnUnlabelledContinueInALabelledInnerLoopContinuesOnlyTheInnerLoop(string outerKind, string innerKind)
    {
        var outer = outerKind switch
        {
            "while" => OuterWhile,
            "do-while" => OuterDoWhile,
            _ => OuterFor,
        };
        var inner = innerKind == "while" ? InnerWhile : InnerDoWhile;

        // Were the continue to leave the inner loop at j === 2 it would reach the outer one instead, which would skip
        // 'after-inner' and never push 'i3' or 'i4'.
        Run($$"""
            (function () {
              var log = [], o = 0;
              outer: {{outer.Head}}
                var j = 0;
                log.push('o' + o);
                inner: {{inner.Head}}
                  if (j === 2) continue;
                  log.push('i' + j);
                {{inner.Tail}}
                log.push('after-inner');
                if (o >= 2) break outer;
              {{outer.Tail}}
              log.push('end');
              return log.join();
            })()
            """).Should().Be("o1,i1,i3,i4,after-inner,o2,i1,i3,i4,after-inner,end");
    }

    [Test]
    public void AContinueNamingTheOuterLoopLeavesALabelledInnerLoop()
    {
        Run("""
            (function () {
              var log = [], o = 0;
              outer: while (o < 3) {
                o++;
                var j = 0;
                inner: while (j < 3) {
                  j++;
                  if (j === 2) continue outer;
                  log.push('o' + o + 'j' + j);
                }
                log.push('never');
              }
              return log.join();
            })()
            """).Should().Be("o1j1,o2j1,o3j1");
    }

    [Test]
    public void AnInnerUnlabelledContinueAndAnOuterLabelledBreakInLabelledDoWhileLoops()
    {
        Run("""
            (function () {
              var log = [], o = 0;
              outer: do {
                o++;
                var j = 0;
                inner: do {
                  j++;
                  if (j < 3) continue;
                  if (o === 2) break outer;
                  break inner;
                } while (j < 5);
                log.push('o' + o + 'j' + j);
              } while (o < 5);
              log.push('end' + o + j);
              return log.join();
            })()
            """).Should().Be("o1j3,end23");
    }

    // ---- the completion value of the loop at script level ----------------------------------------------------------

    // An unlabelled continue carries no value, so the loop's value is the last one the body produced (UpdateEmpty).
    // A break after an if is undefined, because an if statement completes with undefined when its condition is false.
    // The rows that give the continue branch a value of its own also tell the fix apart: a continue escaping the loop
    // would carry that 10 out as the script's value.
    public static TestCases<string, string> CompletionValues => new()
    {
        { "var i = 0; a: while (i < 3) { i++; i * 10; continue; }", "30" },
        { "var i = 0; a: do { i++; i * 10; continue; } while (i < 3);", "30" },
        { "var i = 0; a: b: while (i < 3) { i++; i * 10; continue; }", "30" },
        { "var i = 0; a: b: do { i++; i * 10; continue a; } while (i < 3);", "30" },
        { "1; var i = 0; a: while (i < 3) { i++; continue; }", "2" },
        { "1; var i = 0; a: do { i++; continue; } while (i < 3);", "2" },
        { "var i = 0; a: while (i < 3) { i++; i * 10; break a; }", "10" },
        { "var i = 0; a: do { i++; i * 10; break a; } while (i < 3);", "10" },
        { "var i = 0; a: b: while (i < 3) { i++; i * 10; break a; }", "10" },
        { "var i = 0; a: while (i < 3) { i++; i * 10; if (i < 2) continue; break a; }", "undefined" },
        { "var i = 0; a: do { i++; i * 10; if (i < 2) continue; break a; } while (i < 3);", "undefined" },
        { "var i = 0; a: b: while (i < 3) { i++; i * 10; if (i < 2) continue; break a; }", "undefined" },
        { "var i = 0; a: while (i < 3) { i++; if (i < 3) { i * 10; continue; } else { i * 100; break a; } }", "300" },
        { "var i = 0; a: while (i < 3) { i++; i * 10; if (i < 2) continue; break a; } 'after'", "after" },
        { """eval("var i = 0; a: while (i < 3) { i++; i * 10; if (i < 2) continue; break a; }")""", "undefined" },
        { "var i = 0; a: while (i < 3) { i++; if (i < 2) { i * 10; continue; } break a; }", "undefined" },
        { "var i = 0; a: do { i++; if (i < 2) { i * 10; continue; } break a; } while (i < 3);", "undefined" },
        { "var i = 0; a: b: while (i < 3) { i++; if (i < 2) { i * 10; continue; } break a; }", "undefined" },
        { """eval("var i = 0; a: while (i < 3) { i++; if (i < 2) { i * 10; continue; } break a; }")""", "undefined" },
    };

    [TestCaseSource(nameof(CompletionValues))]
    public void TheLoopCompletesWithTheValueTheSpecGives(string script, string expected)
    {
        Run(script).Should().Be(expected);
    }

    // ---- unwinding through a finally, and block and case-clause positions -------------------------------------------

    [Test]
    public void AnUnlabelledContinueThroughAFinallyContinuesALabelledWhile()
    {
        Run("""
            (function () {
              var log = [], i = 0;
              a: while (i < 3) {
                i++;
                try { if (i < 3) continue; break a; } finally { log.push('f' + i); }
                log.push('never');
              }
              log.push('end');
              return log.join();
            })()
            """).Should().Be("f1,f2,f3,end");
    }

    [Test]
    public void AnUnlabelledContinueThroughAFinallyContinuesALabelledDoWhile()
    {
        Run("""
            (function () {
              var log = [], i = 0;
              a: do {
                i++;
                try { if (i < 3) continue; break a; } finally { log.push('f' + i); }
                log.push('never');
              } while (i < 5);
              log.push('end');
              return log.join();
            })()
            """).Should().Be("f1,f2,f3,end");
    }

    [Test]
    public void ALabelledWhileInABlockAndALabelledDoWhileInACaseClauseRunTheCodeAfterThem()
    {
        Run("""
            (function () {
              var log = [], i = 0, j = 0;
              { a: while (i < 3) { if (i < 1) { i++; continue; } break a; } }
              switch (1) { case 1: b: do { if (j < 1) { j++; continue; } break b; } while (j < 3); }
              log.push('after:' + i + j);
              return log.join();
            })()
            """).Should().Be("after:11");
    }

    // ---- every loop kind, every label stack, in a function, a generator, an async function and an async generator -----

    private static readonly Dictionary<string, (string Setup, string Head, string Tail, string Step)> Loops = new(StringComparer.Ordinal)
    {
        ["while"] = ("var i = 0; ", "while (i < 5) ", "", "i++; "),
        ["do-while"] = ("var i = 0; ", "do ", " while (i < 5);", "i++; "),
        ["for"] = ("", "for (var i = 0; i < 5; i++) ", "", ""),
        ["for-of"] = ("", "for (var i of [0, 1, 2, 3, 4]) ", "", ""),
    };

    private static readonly string[] Contexts = ["plain", "generator", "async", "async-generator"];

    // A continue and the break that follows it, per label stack. Acornima rejects a continue naming a label two or more above
    // the loop, which no stack here has.
    private static readonly Dictionary<string, string[]> Jumps = new(StringComparer.Ordinal)
    {
        ["a"] = ["continue + break a", "continue a + break a", "continue + break"],
        ["a:b"] =
        [
            "continue + break a",
            "continue + break b",
            "continue a + break a",
            "continue b + break b",
            "continue a + break b",
            "continue b + break a",
        ],
    };

    public static TestCases<string, string, string, string> EveryContinueThenBreak
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var context in Contexts)
            {
                foreach (var loop in Loops.Keys)
                {
                    foreach (var stack in Jumps)
                    {
                        foreach (var jump in stack.Value)
                        {
                            rows.Add(context, loop, stack.Key, jump);
                        }
                    }
                }
            }

            return rows;
        }
    }

    private static string Script(string context, string loopKind, string stack, string jump)
    {
        var (setup, head, tail, step) = Loops[loopKind];
        var parts = jump.Split(new[] { " + " }, StringSplitOptions.None);
        var pause = context switch
        {
            "plain" => "",
            "generator" => "yield i; ",
            "async" => "await null; ",
            _ => "yield i; await null; ",
        };

        // Two iterations continue and the third breaks, with the loop suspended in the middle of each one in the
        // generator and async contexts.
        var body = "{ log.push(i); " + pause + "if (i < 2) { " + step + parts[0] + "; } " + parts[1] + "; log.push('never'); }";
        var labels = string.Concat(stack.Split(':').Select(label => label + ": "));
        var inner = "var log = []; " + setup + labels + head + body + tail + " log.push('after'); return log.join();";
        return context switch
        {
            "plain" => "(function () { " + inner + " })()",
            "generator" => "(function () { function* g() { " + inner + " } var it = g(), r, yields = 0; while (!(r = it.next()).done) yields++; return yields + '|' + r.value; })()",
            "async" => "(async function () { " + inner + " })()",
            _ => "(async function () { async function* g() { " + inner + " } var it = g(), r, yields = 0; while (!(r = await it.next()).done) yields++; return yields + '|' + r.value; })()",
        };
    }

    [TestCaseSource(nameof(EveryContinueThenBreak))]
    public void AContinueThenABreakNamingTheLabelRunsTheCodeAfterTheLoop(string context, string loop, string stack, string jump)
    {
        // Three iterations, and then the statement after the loop. The generators yield once per iteration.
        var expected = context is "generator" or "async-generator" ? "3|0,1,2,after" : "0,1,2,after";

        Run(Script(context, loop, stack, jump)).Should().Be(expected);
    }
}
