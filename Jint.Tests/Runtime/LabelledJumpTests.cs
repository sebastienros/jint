namespace Jint.Tests.Runtime;

/// <summary>
/// Guards the target of a labelled <c>break</c>/<c>continue</c> against code that runs while the
/// jump is unwinding. The label used to live in a mutable slot on the evaluation context that
/// <c>PrepareFor</c> cleared on every statement, so any statement executed between the jump and the
/// loop that consumes it — a non-empty <c>finally</c>, an iterator's <c>return()</c>, a
/// <c>Symbol.dispose</c> body — silently turned <c>break outer</c> into an unlabelled break of the
/// innermost loop. It now rides on the completion record itself (<c>Completion.Target</c>), which
/// nothing in between can touch.
/// <para>
/// An <em>empty</em> finalizer never reproduced the bug (a zero-statement list never reaches
/// PrepareFor), which is why the controls below matter as much as the regressions.
/// </para>
/// </summary>
public class LabelledJumpTests
{
    private readonly Engine _engine = new();

    private string Run(string source) => _engine.Evaluate(source).AsString();

    [Fact]
    public void LabelledBreakSurvivesANonEmptyFinally()
    {
        Run("""
            var log = [];
            outer: for (var i = 0; i < 2; i++) {
              for (var j = 0; j < 2; j++) {
                try { break outer; } finally { log.push('f'); }
              }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("f");
    }

    [Fact]
    public void LabelledContinueSurvivesANonEmptyFinally()
    {
        Run("""
            var log = [];
            outer: for (var i = 0; i < 2; i++) {
              for (var j = 0; j < 2; j++) {
                try { continue outer; } finally { log.push('f'); }
              }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("f,f");
    }

    [Fact]
    public void BreakOutOfALabelledBlockSurvivesANonEmptyFinally()
    {
        Run("""
            var log = [];
            outer: {
              for (var j = 0; j < 2; j++) {
                try { break outer; } finally { log.push('f'); }
              }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("f");
    }

    [Fact]
    public void LabelledBreakSurvivesAFinallyThatCallsAFunction()
    {
        Run("""
            var log = [];
            function note(x) { log.push(x); }
            outer: for (var i = 0; i < 2; i++) {
              for (var j = 0; j < 2; j++) {
                try { break outer; } finally { note('f'); }
              }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("f");
    }

    [Fact]
    public void LabelledBreakSurvivesAnIteratorReturnThatRunsStatements()
    {
        Run("""
            var log = [];
            var iter = {};
            iter[Symbol.iterator] = function () {
              var n = 0;
              return {
                next: function () { return { value: n++, done: false }; },
                return: function () { log.push('r'); return { done: true }; }
              };
            };
            outer: for (var i = 0; i < 2; i++) {
              for (var x of iter) { break outer; }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("r");
    }

    [Fact]
    public void LabelledBreakSurvivesAGeneratorCloseOnTheWayOut()
    {
        Run("""
            var log = [];
            function* values() { try { yield 1; yield 2; } finally { log.push('c'); } }
            var it = values();
            outer: for (var i = 0; i < 2; i++) {
              for (var x of it) { break outer; }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("c");
    }

    [Fact]
    public void LabelledBreakSurvivesADisposeBody()
    {
        Run("""
            var log = [];
            outer: for (var i = 0; i < 2; i++) {
              for (var j = 0; j < 2; j++) {
                {
                  using res = { [Symbol.dispose]() { log.push('d'); } };
                  break outer;
                }
              }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("d");
    }

    [Fact]
    public void LabelledBreakOutOfASwitchInsideALabelledLoop()
    {
        Run("""
            var log = [];
            outer: for (var i = 0; i < 2; i++) {
              switch (i) {
                case 0:
                  try { break outer; } finally { log.push('f'); }
              }
              log.push('after-switch');
            }
            log.join(',');
            """).Should().Be("f");
    }

    [Fact]
    public void UnlabelledBreakInsideASwitchStillBreaksTheSwitchOnly()
    {
        Run("""
            var log = [];
            for (var i = 0; i < 2; i++) {
              switch (i) {
                case 0:
                  try { break; } finally { log.push('f'); }
              }
              log.push('after-switch');
            }
            log.join(',');
            """).Should().Be("f,after-switch,after-switch");
    }

    [Fact]
    public void LabelledBreakAcrossThreeLevels()
    {
        Run("""
            var log = [];
            a: for (var i = 0; i < 2; i++) {
              b: for (var j = 0; j < 2; j++) {
                for (var k = 0; k < 2; k++) {
                  try { break a; } finally { log.push('f'); }
                }
                log.push('after-k');
              }
              log.push('after-j');
            }
            log.join(',');
            """).Should().Be("f");
    }

    [Fact]
    public void LabelledContinueTargetingAnOuterLoopFromAWhile()
    {
        Run("""
            var log = [];
            outer: for (var i = 0; i < 2; i++) {
              var j = 0;
              while (j < 2) {
                j++;
                try { continue outer; } finally { log.push('f'); }
              }
              log.push('after-while');
            }
            log.join(',');
            """).Should().Be("f,f");
    }

    [Fact]
    public void LabelledContinueTargetingAnOuterLoopFromADoWhile()
    {
        Run("""
            var log = [];
            outer: for (var i = 0; i < 2; i++) {
              var j = 0;
              do {
                j++;
                try { continue outer; } finally { log.push('f'); }
              } while (j < 2);
              log.push('after-do');
            }
            log.join(',');
            """).Should().Be("f,f");
    }

    [Fact]
    public void LabelledJumpsInsideAGeneratorBody()
    {
        Run("""
            var log = [];
            function* g() {
              outer: for (var i = 0; i < 2; i++) {
                for (var j = 0; j < 2; j++) {
                  yield i;
                  try { break outer; } finally { log.push('f'); }
                }
                log.push('after-inner');
              }
            }
            var it = g();
            it.next(); it.next();
            log.join(',');
            """).Should().Be("f");
    }

    // Controls: an empty finalizer never reproduced the bug, and neither did the plain shapes.
    // They pin that the fix did not change the cases that already worked.

    [Fact]
    public void LabelledBreakThroughAnEmptyFinally()
    {
        Run("""
            var log = [];
            outer: for (var i = 0; i < 2; i++) {
              for (var j = 0; j < 2; j++) {
                try { break outer; } finally { }
              }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("");
    }

    [Fact]
    public void PlainLabelledBreakAndContinueAreUnchanged()
    {
        Run("""
            var log = [];
            outer: for (var i = 0; i < 2; i++) {
              for (var j = 0; j < 2; j++) { break outer; }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("");

        Run("""
            var log = [];
            outer: for (var i = 0; i < 3; i++) {
              for (var j = 0; j < 2; j++) { continue outer; }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("");
    }

    [Fact]
    public void UnlabelledBreakStillBreaksTheInnermostLoop()
    {
        Run("""
            var log = [];
            for (var i = 0; i < 2; i++) {
              for (var j = 0; j < 2; j++) {
                try { break; } finally { log.push('f'); }
              }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("f,after-inner,f,after-inner");
    }

    [Fact]
    public void UnlabelledContinueStillContinuesTheInnermostLoop()
    {
        Run("""
            var log = [];
            for (var i = 0; i < 2; i++) {
              for (var j = 0; j < 2; j++) {
                try { continue; } finally { log.push('f'); }
              }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("f,f,after-inner,f,f,after-inner");
    }

    [Fact]
    public void ABreakTargetingAnInnerLabelIsNotConsumedByAnOuterLoop()
    {
        Run("""
            var log = [];
            for (var i = 0; i < 2; i++) {
              inner: for (var j = 0; j < 2; j++) {
                try { break inner; } finally { log.push('f'); }
              }
              log.push('after-inner');
            }
            log.join(',');
            """).Should().Be("f,after-inner,f,after-inner");
    }
}
