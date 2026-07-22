namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the loop-body lexical flattening (JintForStatement): an eligible body block's let/const
/// bindings fold into the pooled loop environment, and the semantics must be indistinguishable
/// from the per-iteration block environment.
/// </summary>
public class LoopBodyFlatteningTests
{
    [Fact]
    public void StopwatchModernShapeComputesCorrectly()
    {
        var engine = new Engine();
        // the stopwatch-modern inner-loop shape: let header, const body bindings, if/else chain
        var result = engine.Evaluate("""
            (function () {
                let hits = 0;
                for (let x = 0; x < 8; x++) {
                    const z = x ^ 1;
                    const doubled = z * 2;
                    if (z % 2 == 0) { hits += doubled; }
                    else if (z % 3 == 0) { hits += 100; }
                }
                return hits;
            })()
            """).AsNumber();

        // z values: 1,0,3,2,5,4,7,6 → even z (0,2,4,6) add 2z = 0+4+8+12 = 24; z=3 adds 100
        result.Should().Be(124);
    }

    [Fact]
    public void ConstIsFreshPerIterationAndTdzReestablished()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var seen = [];
                for (let i = 0; i < 3; i++) {
                    try { touch; } catch (e) { seen.push('tdz' + i); }
                    const touch = i * 10;
                    seen.push(touch);
                }
                return seen.join(',');
            })()
            """).AsString();

        result.Should().Be("tdz0,0,tdz1,10,tdz2,20");
    }

    [Fact]
    public void ContinueReestablishesTdzForSkippedDeclarations()
    {
        var engine = new Engine();
        // continue jumps before `late` is initialized; the next iteration must still see TDZ,
        // not the previous iteration's value
        var result = engine.Evaluate("""
            (function () {
                var seen = [];
                for (let i = 0; i < 4; i++) {
                    const early = i;
                    if (i % 2 == 0) { continue; }
                    try { seen.push(late); } catch (e) { seen.push('tdz'); }
                    const late = 'v' + i;
                    seen.push(late, early);
                }
                return seen.join(',');
            })()
            """).AsString();

        result.Should().Be("tdz,v1,1,tdz,v3,3");
    }

    [Fact]
    public void ShadowingHeaderNameKeepsBlockScoping()
    {
        var engine = new Engine();
        // body const shadows the header let: names overlap, flattening must decline and the
        // block env must keep proper shadowing semantics
        var result = engine.Evaluate("""
            (function () {
                var seen = [];
                for (let v = 0; v < 3; v++) {
                    seen.push(v);
                    {
                        const v = 'inner';
                        seen.push(v);
                    }
                }
                var direct = [];
                for (let w = 0; w < 2; w++) {
                    const w2 = w + 10;
                    direct.push(w2);
                }
                return seen.join(',') + '|' + direct.join(',');
            })()
            """).AsString();

        result.Should().Be("0,inner,1,inner,2,inner|10,11");
    }

    [Fact]
    public void CapturingBodyKeepsPerIterationSemantics()
    {
        var engine = new Engine();
        // closures capture the body const: escape analysis must exclude flattening (and env
        // reuse), so each captured binding is distinct
        var result = engine.Evaluate("""
            (function () {
                const fns = [];
                for (let i = 0; i < 3; i++) {
                    const snapshot = i * 2;
                    fns.push(() => snapshot);
                }
                return fns.map(f => f()).join(',');
            })()
            """).AsString();

        result.Should().Be("0,2,4");
    }

    [Fact]
    public void ThrowMidBodyLeavesConsistentState()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var caught = 0, total = 0;
                for (let i = 0; i < 5; i++) {
                    try {
                        const val = i;
                        if (i === 2) { throw new Error('x'); }
                        total += val;
                    } catch (e) {
                        caught++;
                    }
                }
                return total + ':' + caught;
            })()
            """).AsString();

        result.Should().Be("8:1"); // 0+1+3+4, one catch
    }

    [Fact]
    public void HeaderInitReferencingOuterNameShadowedByBodyReadsOuter()
    {
        var engine = new Engine();
        // the header init reads `r`; the body block declares its own `r`. The names do not overlap
        // the header's declared name (`l`), so the old NamesOverlap gate let flattening fold the
        // body's `r` slot into the loop env — where the init's `r` then resolved to the
        // still-uninitialized slot and threw a spurious TDZ. The init's `r` is an OUTER reference.
        var result = engine.Evaluate("""
            (function () {
                let r = 5, o = 8, seen = [];
                for (let l = r; l < o; l++) {
                    let r = l * 10;
                    seen.push(r);
                }
                return seen.join(',');
            })()
            """).AsString();

        result.Should().Be("50,60,70");
    }

    [Fact]
    public void HeaderTestReferencingOuterNameShadowedByBodyReadsOuter()
    {
        var engine = new Engine();
        // same shape, but the outer name is read from the loop test rather than the init
        var result = engine.Evaluate("""
            (function () {
                let r = 3, seen = [];
                for (let l = 0; l < r; l++) {
                    let r = l * 10;
                    seen.push(r);
                }
                return seen.join(',');
            })()
            """).AsString();

        result.Should().Be("0,10,20");
    }

    [Fact]
    public void HeaderUpdateReferencingOuterNameShadowedByBodyReadsOuter()
    {
        var engine = new Engine();
        // the update reads outer `step`; the body shadows `step`
        var result = engine.Evaluate("""
            (function () {
                let step = 2, seen = [];
                for (let l = 0; l < 6; l += step) {
                    let step = l * 10;
                    seen.push(step);
                }
                return seen.join(',');
            })()
            """).AsString();

        result.Should().Be("0,20,40");
    }

    [Fact]
    public void TurbopackChunkShapeResolvesModuleFactory()
    {
        var engine = new Engine();
        // reduced from a Turbopack runtime chunk: a header `for (let l = r; ...)` over a body that
        // block-scopes `r` and `o` while the header reads the outer `r`/`o` — the exact shape that
        // aborted the Next.js bootstrap with "r has not been initialized"
        var result = engine.Evaluate("""
            (function () {
                let e = [0, 0, 'fa', 'fb'];
                let t = new Map();
                let r = 2, o = 4, n;
                for (let l = r; l < o; l++) {
                    let r = e[l], o = t.get(r);
                    if (o) { n = o; break; }
                }
                return 'ok:' + (n === undefined);
            })()
            """).AsString();

        result.Should().Be("ok:true");
    }

    [Fact]
    public void UsingDeclarationsKeepBlockDisposeSemantics()
    {
        var engine = new Engine();
        // using declarations need block-exit dispose per iteration: flattening must decline
        var result = engine.Evaluate("""
            (function () {
                const order = [];
                for (let i = 0; i < 2; i++) {
                    using res = { [Symbol.dispose]() { order.push('d' + i); } };
                    order.push('b' + i);
                }
                return order.join(',');
            })()
            """).AsString();

        result.Should().Be("b0,d0,b1,d1");
    }

    [Fact]
    public void ClosureInDestructuringDefaultDeclinesEnvironmentReuse()
    {
        var engine = new Engine();
        // a closure embedded in a destructuring pattern's DEFAULT value captures the loop's
        // initial per-iteration environment; reusing/pooling the environment in place makes it
        // observe the final i (or a reset binding) instead of the captured iteration's value
        var result = engine.Evaluate("""
            (function () {
                const fns = [];
                for (let i = 0, { f = () => i } = {}; i < 3; i++) { fns.push(f); }
                return fns[0]() + ',' + fns[2]();
            })()
            """).AsString();

        result.Should().Be("0,0");
    }

    [Fact]
    public void ClosureInArrayPatternDefaultDeclinesEnvironmentReuse()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                const fns = [];
                for (let i = 0, [f = () => i] = []; i < 3; i++) { fns.push(f); }
                return fns[0]() + ',' + fns[2]();
            })()
            """).AsString();

        result.Should().Be("0,0");
    }

    [Fact]
    public void ReenteringLoopWithEscapedPatternClosureKeepsBindingAlive()
    {
        var engine = new Engine();
        // re-entering the loop must not reset the binding the escaped closure captured
        // (a pooled environment would make the earlier closure throw or observe garbage)
        var result = engine.Evaluate("""
            (function () {
                function run() {
                    const fns = [];
                    for (let i = 0, { f = () => i } = {}; i < 2; i++) { fns.push(f); }
                    return fns;
                }
                const first = run();
                const second = run();
                return first[0]() + ',' + second[0]();
            })()
            """).AsString();

        result.Should().Be("0,0");
    }
}
