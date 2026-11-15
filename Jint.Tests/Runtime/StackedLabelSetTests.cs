#nullable enable

using Jint.Native;
using Jint.Native.Promise;

namespace Jint.Tests.Runtime;

/// <summary>
/// A loop's label set is every label stacked directly on it: <c>a: b: for (...)</c> evaluates the loop with {a, b},
/// because LabelledEvaluation appends each label on its way down
/// (https://tc39.es/ecma262/#sec-runtime-semantics-labelledevaluation), and LoopContinues keeps
/// the loop iterating for a <c>continue</c> naming any label in that set
/// (https://tc39.es/ecma262/#sec-loopcontinues). Every loop compared the target with the innermost label alone, so
/// <c>continue a</c> left the loop as a Continue completion nothing consumed: a function skipped the rest of its body
/// and returned <c>undefined</c>, and an async generator's promise never settled. While and do-while compared an
/// unlabelled <c>continue</c> with their label too, so <c>b: while (...) { continue; }</c> left the same way.
/// <para>
/// Every expected value was cross-checked against Node 26.
/// </para>
/// </summary>
public class StackedLabelSetTests
{
    private static readonly Dictionary<string, (string Setup, string Template)> Loops = new(StringComparer.Ordinal)
    {
        ["for"] = ("", "for (var i = 0; i < 3; i++) BODY"),
        ["for-let"] = ("", "for (let i = 0; i < 3; i++) BODY"),
        ["for-in"] = ("", "for (var k in { p: 1, q: 2, r: 3 }) BODY"),
        ["for-of"] = ("", "for (var x of [1, 2, 3]) BODY"),
        ["while"] = ("var w = 0; ", "while (w++ < 3) BODY"),
        ["do-while"] = ("var w = 0; ", "do BODY while (++w < 3);"),
        ["for-await"] = ("", "for await (var x of [1, 2, 3]) BODY"),
    };

    private static readonly string[] Contexts = ["plain", "generator", "async", "async-generator"];

    private static readonly string[][] Stacks = [["a"], ["a", "b"], ["a", "b", "c"]];

    /// <summary>
    /// Every loop kind under every label stack of one to three, with an unlabelled jump and a jump naming each label,
    /// run directly, across a <c>yield</c>, across an <c>await</c>, and across both.
    /// </summary>
    public static TestCases<string, string, string, string> EveryJump
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var context in Contexts)
            {
                foreach (var loop in Loops.Keys)
                {
                    if (loop == "for-await" && context is "plain" or "generator")
                    {
                        continue;
                    }

                    foreach (var stack in Stacks)
                    {
                        foreach (var jump in Jumps(stack))
                        {
                            rows.Add(context, loop, string.Join(":", stack), jump);
                        }
                    }
                }
            }

            return rows;
        }
    }

    private static IEnumerable<string> Jumps(string[] stack)
    {
        yield return "continue";
        yield return "break";
        for (var i = 0; i < stack.Length; i++)
        {
            // Acornima rejects a continue naming a label two or more above the loop as an early error; see
            // AcornimaStillRejectsAContinueTwoLabelsAboveTheLoop.
            if (i >= stack.Length - 2)
            {
                yield return "continue " + stack[i];
            }

            yield return "break " + stack[i];
        }
    }

    private static string Script(string context, string loop, string[] stack, string jump)
    {
        var (setup, template) = Loops[loop];
        var pause = context switch
        {
            "plain" => "",
            "generator" => "yield n; ",
            "async" => "await null; ",
            _ => "yield n; await null; ",
        };

        var body = "{ n++; " + pause + jump + "; n += 100; }";
        var labelled = string.Concat(stack.Select(label => label + ": ")) + template.Replace("BODY", body);
        var inner = "var n = 0, after = 0; " + setup + labelled + " after++; return n + ':' + after;";
        return context switch
        {
            "plain" => "(function () { " + inner + " })()",
            "generator" => "(function () { function* g() { " + inner + " } var it = g(), r, yields = 0; while (!(r = it.next()).done) yields++; return yields + '|' + r.value; })()",
            "async" => "(async function () { " + inner + " })()",
            _ => "(async function () { async function* g() { " + inner + " } var it = g(), r, yields = 0; while (!(r = await it.next()).done) yields++; return yields + '|' + r.value; })()",
        };
    }

    // A continue runs all three iterations and a break one; either way the statement after the loop runs.
    private static string Expected(string context, string jump)
    {
        var iterations = jump.StartsWith("continue", StringComparison.Ordinal) ? "3" : "1";
        var value = iterations + ":1";
        return context is "generator" or "async-generator" ? iterations + "|" + value : value;
    }

    private static string Run(string script)
    {
        var engine = new Engine();
        var value = engine.Evaluate(script);

        // Evaluate drains the event loop before it returns, so a promise still pending here never settles - which is
        // what a Continue completion escaping an async generator's body did.
        if (value is JsPromise { State: PromiseState.Pending })
        {
            return "never settled";
        }

        return value.UnwrapIfPromise().ToString();
    }

    [TestCaseSource(nameof(EveryJump))]
    public void AJumpContinuesOrLeavesTheLoopItsLabelNames(string context, string loop, string labels, string jump)
    {
        Run(Script(context, loop, labels.Split(':'), jump)).Should().Be(Expected(context, jump));
    }

    [Test]
    public void AContinueNamingTheOuterOfTwoLabelsContinuesTheLoop()
    {
        Run("function f(){ var n = 0; a: b: for (var i = 0; i < 2; i++) { n++; continue a; } return n; } f()").Should().Be("2");
    }

    [Test]
    public void AnUnlabelledContinueKeepsALabelledWhileIterating()
    {
        Run("(function () { var i = 0, n = 0; b: while (i < 3) { i++; n++; continue; n += 100; } return n; })()").Should().Be("3");
    }

    [Test]
    public void AnUnlabelledContinueKeepsALabelledDoWhileIterating()
    {
        Run("(function () { var i = 0, n = 0; b: do { i++; n++; continue; n += 100; } while (i < 3); return n; })()").Should().Be("3");
    }

    [Test]
    public void AContinueNamingAnOuterLoopLeavesAnInnerStackedSet()
    {
        Run("""
            var log = [];
            outer: for (var i = 0; i < 2; i++) {
              a: b: for (var j = 0; j < 2; j++) { log.push('' + i + j); continue outer; }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("00,10");
    }

    [Test]
    public void AContinueNamingTheOuterLabelOfAnOuterLoopLeavesAnInnerStackedSet()
    {
        Run("""
            var log = [];
            x: outer: for (var i = 0; i < 2; i++) {
              a: b: for (var j = 0; j < 2; j++) { log.push('' + i + j); continue x; }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("00,10");
    }

    [Test]
    public void ALabelledBlockAboveTheStackIsNotInTheLoopsLabelSet()
    {
        // LabelledEvaluation of a block evaluates it with no label set, so the loop's set is {b, c}.
        Run("var n = 0; a: { b: c: for (var i = 0; i < 3; i++) { n++; continue b; n += 100; } n += 10; } n").Should().Be("13");
    }

    [Test]
    public void AContinueNamingALabelledBlockIsAnEarlyError()
    {
        Run("""(function () { try { eval("a: { b: for (;;) { continue a; } }"); return "parsed"; } catch (e) { return e.name; } })()""").Should().Be("SyntaxError");
        Run("""(function () { try { eval("a: b: { for (;;) { continue a; } }"); return "parsed"; } catch (e) { return e.name; } })()""").Should().Be("SyntaxError");
    }

    [Test]
    public void AContinueNamingAnOuterLabelKeepsTheIteratorOpen()
    {
        const string Iterable = """
            var log = [];
            var iterable = {};
            iterable[Symbol.iterator] = function () {
              var k = 0;
              return {
                next: function () { log.push('next'); return { value: k, done: k++ >= 2 }; },
                return: function () { log.push('return'); return {}; }
              };
            };
            """;

        Run(Iterable + "a: b: for (var x of iterable) { log.push('body' + x); continue a; } log.join(',');").Should().Be("next,body0,next,body1,next");
        Run(Iterable + "a: b: for (var x of iterable) { log.push('body' + x); break a; } log.join(',');").Should().Be("next,body0,return");
    }

    [Test]
    public void AContinueNamingAnOuterLabelKeepsTheAsyncIteratorOpen()
    {
        Run("""
            (async function () {
              var log = [];
              var iterable = {};
              iterable[Symbol.asyncIterator] = function () {
                var k = 0;
                return {
                  next: function () { log.push('next'); return Promise.resolve({ value: k, done: k++ >= 2 }); },
                  return: function () { log.push('return'); return Promise.resolve({}); }
                };
              };
              a: b: for await (var x of iterable) { log.push('body' + x); continue a; }
              return log.join(',');
            })()
            """).Should().Be("next,body0,next,body1,next");
    }

    [Test]
    public void TheLoopsCompletionValueSurvivesAContinueNamingAnOuterLabel()
    {
        Run("eval('a: b: for (var i = 0; i < 2; i++) { i; continue a; }')").Should().Be("1");
        Run("a: b: for (var i = 0; i < 2; i++) { i; continue a; }").Should().Be("1");
    }

    [Test]
    public void AStackedSetInABlockAndInACaseClause()
    {
        Run("""
            (function () {
              var n = 0;
              { a: b: for (var i = 0; i < 3; i++) { n++; continue a; } }
              switch (1) { case 1: c: d: for (var j = 0; j < 3; j++) { n++; continue c; } }
              return n;
            })()
            """).Should().Be("6");
    }

    [Test]
    public void AContinueNamingAnOuterLabelThroughAFinally()
    {
        Run("""
            (function () {
              var n = 0, log = [];
              a: b: for (var i = 0; i < 3; i++) { try { n++; continue a; } finally { log.push('f'); } }
              return n + ':' + log.join('');
            })()
            """).Should().Be("3:fff");
    }

    [Test]
    public void AContinueNamingAnOuterLabelThroughAFinallyThatYields()
    {
        Run("""
            (function () {
              function* g() {
                a: b: for (var i = 0; i < 2; i++) { try { continue a; } finally { yield 'f' + i; } }
                return 'done';
              }
              var it = g(), r, out = [];
              while (!(r = it.next()).done) out.push(r.value);
              out.push(r.value);
              return out.join(',');
            })()
            """).Should().Be("f0,f1,done");
    }

    [Test]
    public void AContinueNamingAnOuterLabelThroughAFinallyThatAwaits()
    {
        Run("""
            (async function () {
              var n = 0;
              a: b: for (var i = 0; i < 3; i++) { try { n++; continue a; } finally { await null; } }
              return n;
            })()
            """).Should().Be("3");
    }

    [Test]
    public void AContinueNamingAnOuterLabelThroughAnAsyncGeneratorFinallyThatYieldsAndAwaits()
    {
        Run("""
            (async function () {
              async function* g() {
                a: b: for (var i = 0; i < 2; i++) { try { continue a; } finally { yield 'f' + i; await null; } }
                return 'done';
              }
              var it = g(), r, out = [];
              while (!(r = await it.next()).done) out.push(r.value);
              out.push(r.value);
              return out.join(',');
            })()
            """).Should().Be("f0,f1,done");
    }

    // The dispose of an await using head awaits, so the loop resumes from it and reads the body's completion there.
    [TestCase("for")]
    [TestCase("for await")]
    public void AContinueNamingAnOuterLabelAfterAnAwaitingDispose(string head)
    {
        Run("""
            (async function () {
              var log = [], n = 0;
              function res(k) { var o = {}; o[Symbol.asyncDispose] = async function () { log.push('d' + k); await null; }; return o; }
              a: b: HEAD (await using r of [res(0), res(1), res(2)]) { n++; continue a; }
              return n + ':' + log.join(',');
            })()
            """.Replace("HEAD", head)).Should().Be("3:d0,d1,d2");
    }

    /// <summary>
    /// Pins a parser defect so that its fix is noticed. Node accepts this program and answers 3; Acornima raises the
    /// early error ContainsUndefinedContinueTarget reserves for a label that is not on an iteration statement,
    /// because its <c>ParseLabeledStatement</c> marks only the label directly above the next one as a loop label.
    /// The loop handler already holds the whole set, so when this starts failing, delete it and drop the filter in
    /// <see cref="Jumps"/>.
    /// </summary>
    [Test]
    public void AcornimaStillRejectsAContinueTwoLabelsAboveTheLoop()
    {
        Run("""
            (function () {
              try {
                return String(eval("(function () { var n = 0; a: b: c: for (var i = 0; i < 3; i++) { n++; continue a; } return n; })()"));
              } catch (e) {
                return e.name;
              }
            })()
            """).Should().Be("SyntaxError");
    }
}
