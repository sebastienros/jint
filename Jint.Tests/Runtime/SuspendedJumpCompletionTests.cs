namespace Jint.Tests.Runtime;

/// <summary>
/// A <c>break</c> or <c>continue</c> whose <c>finally</c> block suspends — on a <c>yield</c> or an
/// <c>await</c> — must still perform its jump when the block resumes and completes normally, exactly
/// as the non-suspending case does.
/// <para>
/// Only Throw and Return used to be parked across the suspension, so the jump was discarded: the
/// resume found no pending completion, returned a normal one, and the enclosing loops ran their
/// remaining iterations while the statements the jump was meant to skip executed. Parking the whole
/// completion record — the jump statement included, since that is where
/// <see cref="Jint.Runtime.Completion.Target"/> reads the label from — is what carries the target
/// across the suspension.
/// </para>
/// <para>
/// <see cref="LabelledJumpTests"/> is the non-suspending sibling: the same jumps through a non-empty
/// finalizer with no generator or async function in sight.
/// </para>
/// </summary>
public class SuspendedJumpCompletionTests
{
    // Every drain is bounded so a regression that never terminates fails with RUNAWAY in the
    // produced sequence rather than hanging the run.
    private const string Drain = """
        function drain(it) {
          var out = [], guard = 0;
          var r = it.next();
          while (!r.done) {
            out.push(r.value);
            if (++guard > 12) { out.push('RUNAWAY'); break; }
            r = it.next();
          }
          return out;
        }
        """;

    private static string Run(string source) => new Engine().Evaluate(source).AsString();

    private static string RunAsync(string source) => new Engine().Evaluate(source).UnwrapIfPromise().AsString();

    // ---------------------------------------------------------------- generators

    [Fact]
    public void UnlabelledBreakThroughAYieldingFinally()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              for (var j = 0; j < 2; j++) {
                try { break; } finally { yield 'f'; }
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f|end");
    }

    [Fact]
    public void LabelledBreakThroughAYieldingFinally()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              outer: for (var i = 0; i < 2; i++) {
                for (var j = 0; j < 2; j++) {
                  try { break outer; } finally { yield 'f'; }
                }
                log.push('after-inner');
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f|end");
    }

    [Fact]
    public void UnlabelledContinueThroughAYieldingFinally()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              for (var j = 0; j < 2; j++) {
                try { continue; } finally { yield 'f'; }
                log.push('never');
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f,f|end");
    }

    [Fact]
    public void LabelledContinueThroughAYieldingFinally()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              outer: for (var i = 0; i < 2; i++) {
                for (var j = 0; j < 2; j++) {
                  try { continue outer; } finally { yield 'f'; }
                  log.push('never-inner');
                }
                log.push('never-outer');
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f,f|end");
    }

    [Fact]
    public void BreakOutOfALabelledBlockThroughAYieldingFinally()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              outer: {
                for (var j = 0; j < 2; j++) {
                  try { break outer; } finally { yield 'f'; }
                }
                log.push('after-inner');
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f|end");
    }

    [Fact]
    public void ABreakTargetingAnInnerLabelIsNotConsumedByAnOuterLoopAcrossAYield()
    {
        // The label has to survive the suspension in the other direction too: an inner target read
        // as unlabelled would break the same loop here, so only the outer loop's iteration count
        // tells the two apart.
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              for (var i = 0; i < 2; i++) {
                inner: for (var j = 0; j < 2; j++) {
                  try { break inner; } finally { yield 'f'; }
                }
                log.push('after-inner');
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f,f|after-inner,after-inner,end");
    }

    [Fact]
    public void ALabelledBreakAcrossThreeLevelsThroughAYieldingFinally()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              a: for (var i = 0; i < 2; i++) {
                b: for (var j = 0; j < 2; j++) {
                  for (var k = 0; k < 2; k++) {
                    try { break a; } finally { yield 'f'; }
                  }
                  log.push('after-k');
                }
                log.push('after-j');
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f|end");
    }

    [Fact]
    public void AJumpInTheFinallyOverridesThePendingJump()
    {
        // Step 3 keeps B only when F is a normal completion; an abrupt finalizer wins outright.
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              outer: for (var i = 0; i < 2; i++) {
                try { break outer; } finally { yield 'f'; continue outer; }
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f,f|end");
    }

    [Fact]
    public void AReturnInTheFinallyOverridesThePendingJump()
    {
        Run($$"""
            var log = [];
            function* g() {
              outer: for (var i = 0; i < 2; i++) {
                try { break outer; } finally { yield 'f'; return 'R'; }
              }
              log.push('end');
            }
            var it = g();
            var a = it.next();
            var b = it.next();
            a.value + '|' + b.value + '|' + b.done + '|' + log.join(',');
            """).Should().Be("f|R|true|");
    }

    [Fact]
    public void ABreakThroughTwoNestedYieldingFinallyBlocks()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              outer: for (var i = 0; i < 2; i++) {
                try {
                  try { break outer; } finally { yield 'inner'; }
                } finally { yield 'outer'; }
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("inner,outer|end");
    }

    [Fact]
    public void ABreakThroughAYieldingFinallyInsideASwitch()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              outer: for (var i = 0; i < 2; i++) {
                switch (i) {
                  case 0:
                    try { break outer; } finally { yield 'f'; }
                }
                log.push('after-switch');
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f|end");
    }

    [Fact]
    public void AnUnlabelledBreakInsideASwitchStillBreaksTheSwitchOnlyAcrossAYield()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              for (var i = 0; i < 2; i++) {
                switch (i) {
                  case 0:
                    try { break; } finally { yield 'f'; }
                }
                log.push('after-switch');
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f|after-switch,after-switch,end");
    }

    [Fact]
    public void AContinueThroughAYieldingFinallyInAWhileLoop()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              var j = 0;
              while (j < 2) {
                j++;
                try { continue; } finally { yield 'f'; }
                log.push('never');
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f,f|end");
    }

    [Fact]
    public void ABreakThroughAYieldingFinallyInAForOfLoop()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              outer: for (var i = 0; i < 2; i++) {
                for (var x of [1, 2]) {
                  try { break outer; } finally { yield 'f'; }
                }
                log.push('after-inner');
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("f|end");
    }

    // A finalizer may contain another try statement that parks a completion of its own before
    // suspending, so the park cannot live in one slot on the generator. These three are what a
    // single slot got wrong: the inner park consumed the outer's, which cost the outer jump its
    // target and silently swallowed a pending return or throw.

    [Fact]
    public void AFinallyContainingAnotherParkingTryKeepsItsOwnPendingBreak()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              outer: for (var i = 0; i < 2; i++) {
                try { break outer; }
                finally {
                  inner: for (var k = 0; k < 1; k++) {
                    try { break inner; } finally { yield 'x'; }
                  }
                }
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("x|end");
    }

    [Fact]
    public void AFinallyContainingAnotherParkingTryKeepsItsOwnPendingReturn()
    {
        Run("""
            function* g() {
              try { return 'R'; }
              finally {
                inner: for (var k = 0; k < 1; k++) {
                  try { break inner; } finally { yield 'x'; }
                }
              }
            }
            var it = g();
            var a = it.next();
            var b = it.next();
            a.value + '|' + b.value + '|' + b.done;
            """).Should().Be("x|R|true");
    }

    [Fact]
    public void AFinallyContainingAnotherParkingTryKeepsItsOwnPendingThrow()
    {
        Run("""
            function* g() {
              try { throw 'T'; }
              finally {
                inner: for (var k = 0; k < 1; k++) {
                  try { break inner; } finally { yield 'x'; }
                }
              }
            }
            var it = g();
            var a = it.next();
            var caught = 'none';
            try { it.next(); } catch (e) { caught = e; }
            a.value + '|' + caught;
            """).Should().Be("x|T");
    }

    // ------------------------------------------------------- async functions

    [Fact]
    public void UnlabelledBreakThroughAnAwaitingFinally()
    {
        RunAsync("""
            (async function () {
              var log = [];
              for (var j = 0; j < 2; j++) {
                try { break; } finally { await 0; log.push('f'); }
              }
              log.push('end');
              return log.join(',');
            })()
            """).Should().Be("f,end");
    }

    [Fact]
    public void LabelledBreakThroughAnAwaitingFinally()
    {
        RunAsync("""
            (async function () {
              var log = [];
              outer: for (var i = 0; i < 2; i++) {
                for (var j = 0; j < 2; j++) {
                  try { break outer; } finally { await 0; log.push('f'); }
                }
                log.push('after-inner');
              }
              log.push('end');
              return log.join(',');
            })()
            """).Should().Be("f,end");
    }

    [Fact]
    public void UnlabelledContinueThroughAnAwaitingFinally()
    {
        RunAsync("""
            (async function () {
              var log = [];
              for (var j = 0; j < 2; j++) {
                try { continue; } finally { await 0; log.push('f'); }
                log.push('never');
              }
              log.push('end');
              return log.join(',');
            })()
            """).Should().Be("f,f,end");
    }

    [Fact]
    public void LabelledContinueThroughAnAwaitingFinally()
    {
        RunAsync("""
            (async function () {
              var log = [];
              outer: for (var i = 0; i < 2; i++) {
                for (var j = 0; j < 2; j++) {
                  try { continue outer; } finally { await 0; log.push('f'); }
                  log.push('never-inner');
                }
                log.push('never-outer');
              }
              log.push('end');
              return log.join(',');
            })()
            """).Should().Be("f,f,end");
    }

    [Fact]
    public void AJumpInAnAwaitingFinallyOverridesThePendingJump()
    {
        RunAsync("""
            (async function () {
              var log = [];
              outer: for (var i = 0; i < 2; i++) {
                try { break outer; } finally { await 0; log.push('f'); continue outer; }
              }
              log.push('end');
              return log.join(',');
            })()
            """).Should().Be("f,f,end");
    }

    [Fact]
    public void ABreakThroughAnAwaitingFinallyInAForAwaitLoop()
    {
        RunAsync("""
            (async function () {
              var log = [];
              outer: for (var i = 0; i < 2; i++) {
                for await (var x of [1, 2]) {
                  try { break outer; } finally { await 0; log.push('f'); }
                }
                log.push('after-inner');
              }
              log.push('end');
              return log.join(',');
            })()
            """).Should().Be("f,end");
    }

    // ------------------------------------------------------ async generators

    [Fact]
    public void UnlabelledBreakThroughAYieldingFinallyInAnAsyncGenerator()
    {
        RunAsync("""
            (async function () {
              var log = [];
              async function* g() {
                for (var j = 0; j < 2; j++) {
                  try { break; } finally { yield 'f'; }
                }
                log.push('end');
              }
              var out = [], guard = 0;
              for await (var v of g()) {
                out.push(v);
                if (++guard > 12) { out.push('RUNAWAY'); break; }
              }
              return out.join(',') + '|' + log.join(',');
            })()
            """).Should().Be("f|end");
    }

    [Fact]
    public void LabelledBreakThroughAYieldingFinallyInAnAsyncGenerator()
    {
        RunAsync("""
            (async function () {
              var log = [];
              async function* g() {
                outer: for (var i = 0; i < 2; i++) {
                  for (var j = 0; j < 2; j++) {
                    try { break outer; } finally { yield 'f'; }
                  }
                  log.push('after-inner');
                }
                log.push('end');
              }
              var out = [], guard = 0;
              for await (var v of g()) {
                out.push(v);
                if (++guard > 12) { out.push('RUNAWAY'); break; }
              }
              return out.join(',') + '|' + log.join(',');
            })()
            """).Should().Be("f|end");
    }

    [Fact]
    public void ContinueThroughAYieldingFinallyInAnAsyncGenerator()
    {
        RunAsync("""
            (async function () {
              var log = [];
              async function* g() {
                for (var j = 0; j < 2; j++) {
                  try { continue; } finally { yield 'f'; }
                  log.push('never');
                }
                log.push('end');
              }
              var out = [], guard = 0;
              for await (var v of g()) {
                out.push(v);
                if (++guard > 12) { out.push('RUNAWAY'); break; }
              }
              return out.join(',') + '|' + log.join(',');
            })()
            """).Should().Be("f,f|end");
    }

    [Fact]
    public void AwaitInAFinallyOfAnAsyncGeneratorStillCarriesTheJump()
    {
        RunAsync("""
            (async function () {
              var log = [];
              async function* g() {
                outer: for (var i = 0; i < 2; i++) {
                  for (var j = 0; j < 2; j++) {
                    try { break outer; } finally { await 0; log.push('f'); }
                  }
                  log.push('after-inner');
                }
                log.push('end');
                yield 'done';
              }
              var out = [], guard = 0;
              for await (var v of g()) {
                out.push(v);
                if (++guard > 12) { out.push('RUNAWAY'); break; }
              }
              return out.join(',') + '|' + log.join(',');
            })()
            """).Should().Be("done|f,end");
    }

    // ------------------------------------------------------------- controls
    // Return and Throw were already parked across a suspension, and the non-suspending jump was
    // fixed separately (LabelledJumpTests). These pin that widening the park changed neither.

    [Fact]
    public void APendingReturnStillSurvivesAYieldingFinally()
    {
        Run("""
            function* g() { try { return 'R'; } finally { yield 'f'; } }
            var it = g();
            var a = it.next();
            var b = it.next();
            a.value + '|' + b.value + '|' + b.done;
            """).Should().Be("f|R|true");
    }

    [Fact]
    public void APendingThrowStillSurvivesAYieldingFinally()
    {
        Run("""
            function* g() { try { throw 'T'; } finally { yield 'f'; } }
            var it = g();
            var a = it.next();
            var caught = 'none';
            try { it.next(); } catch (e) { caught = e; }
            a.value + '|' + caught;
            """).Should().Be("f|T");
    }

    [Fact]
    public void APendingReturnStillSurvivesAnAwaitingFinally()
    {
        RunAsync("""
            (async function () {
              var log = [];
              async function f() { try { return 'R'; } finally { await 0; log.push('f'); } }
              log.push(await f());
              return log.join(',');
            })()
            """).Should().Be("f,R");
    }

    [Fact]
    public void APendingThrowStillSurvivesAnAwaitingFinally()
    {
        RunAsync("""
            (async function () {
              var log = [];
              async function f() { try { throw 'T'; } finally { await 0; log.push('f'); } }
              try { await f(); } catch (e) { log.push('caught:' + e); }
              return log.join(',');
            })()
            """).Should().Be("f,caught:T");
    }

    [Fact]
    public void AYieldInsideACatchFollowedByABreakIsUnchanged()
    {
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              for (var i = 0; i < 3; i++) {
                try { throw 'x'; } catch (e) { yield 'c'; }
                break;
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("c|end");
    }

    [Fact]
    public void TheNonSuspendingLabelledBreakThroughANonEmptyFinallyIsUnchanged()
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
    public void AYieldBeforeTheTryStatementIsUnchanged()
    {
        // The jump is not parked at all here: the suspension is outside the finalizer, so the
        // whole try/finally runs to completion on one resume.
        Run($$"""
            {{Drain}}
            var log = [];
            function* g() {
              outer: for (var i = 0; i < 2; i++) {
                for (var j = 0; j < 2; j++) {
                  yield 'y';
                  try { break outer; } finally { log.push('f'); }
                }
                log.push('after-inner');
              }
              log.push('end');
            }
            drain(g()).join(',') + '|' + log.join(',');
            """).Should().Be("y|f,end");
    }
}
