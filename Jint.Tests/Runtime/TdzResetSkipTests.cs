namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the semantics of the flattened for-loop body TDZ reset skip: when the body block's
/// conservative scan (BlockState.AllBodySlotsInitBeforeUse) proves every slot binding is
/// initialized by its declaration before any reference to it can evaluate, the per-iteration
/// re-TDZ of the body slot range is skipped in both the generic and tight flattened arms.
/// Shapes the scan rejects — any identifier occurrence of a slot name textually before its
/// declaring statement, including inside its own initializer — must keep the per-iteration
/// reset and throw ReferenceError on every iteration, not just the first.
/// </summary>
public class TdzResetSkipTests
{
    [Fact]
    public void EligibleConstBodyComputesCorrectValuesAcrossIterations()
    {
        var engine = new Engine();
        // same body twice through the tight arm (expression/declaration statements only) and once
        // through the generic arm (the never-taken break disqualifies the tight shape but not
        // flattening); repeated calls also pin that loop entry re-establishes the full TDZ
        var result = engine.Evaluate("""
            (function () {
                function tightShape() {
                    var acc = [];
                    for (let i = 0; i < 3; i++) {
                        const z = i * 2;
                        acc.push(z);
                    }
                    return acc.join(',');
                }
                function genericShape() {
                    var acc = [];
                    for (let i = 0; i < 3; i++) {
                        const z = i * 2;
                        acc.push(z);
                        if (i > 100) break;
                    }
                    return acc.join(',');
                }
                return tightShape() + '|' + tightShape() + '|' + genericShape();
            })()
            """).AsString();

        result.Should().Be("0,2,4|0,2,4|0,2,4");
    }

    [Fact]
    public void ChainedConstDeclarationsComputeCorrectly()
    {
        var engine = new Engine();
        // the stopwatch-modern shape: several const slots per iteration, each initializer only
        // referencing already-declared names — scan accepts, tight arm runs without any re-TDZ
        var result = engine.Evaluate("""
            (function () {
                let acc = 0;
                for (let i = 0; i < 50; i++) {
                    const x = i | 0;
                    const y = i * 3;
                    const z = x ^ y;
                    acc = (acc + z) | 0;
                }
                return acc;
            })()
            """).AsNumber();

        var expected = 0;
        for (var i = 0; i < 50; i++)
        {
            expected = expected + (i ^ (i * 3));
        }

        result.Should().Be(expected);
    }

    [Fact]
    public void ReadBeforeDeclarationThrowsOnEveryIteration()
    {
        var engine = new Engine();
        // the scan rejects this shape (z referenced textually before its declaration), so the
        // per-iteration reset must still run: the read throws on all three iterations — a wrongly
        // applied skip would leave iteration 1's value in the slot and only throw once
        var result = engine.Evaluate("""
            (function () {
                let threw = 0;
                for (let i = 0; i < 3; i++) {
                    try { z; } catch (e) { if (e instanceof ReferenceError) threw++; }
                    const z = i;
                }
                return threw;
            })()
            """).AsNumber();

        result.Should().Be(3);
    }

    [Fact]
    public void WriteBeforeDeclarationStillThrowsOnLaterIterations()
    {
        var engine = new Engine();
        // assignment is a reference too: iteration 1's write lands in the TDZ re-established by
        // the retained reset (tight arm), never on iteration 0's stale initialized binding
        var result = engine.Evaluate("""
            (function () {
                try {
                    for (let i = 0; i < 3; i++) {
                        if (i === 1) { z = 5; }
                        let z = i;
                    }
                } catch (e) {
                    return e instanceof ReferenceError ? 'tdz-write' : 'other';
                }
                return 'no-throw';
            })()
            """).AsString();

        result.Should().Be("tdz-write");
    }

    [Fact]
    public void ContinueOverInitializationKeepsCorrectValues()
    {
        var engine = new Engine();
        // continue skips the declaration for odd iterations, leaving a stale value in the slot;
        // it is unobservable because every read is lexically after the declaration re-initializes
        var result = engine.Evaluate("""
            (function () {
                let sum = 0, visits = 0;
                for (let i = 0; i < 4; i++) {
                    visits++;
                    if (i & 1) continue;
                    const z = i;
                    sum += z;
                }
                return sum + ':' + visits;
            })()
            """).AsString();

        result.Should().Be("2:4");
    }

    [Fact]
    public void ClosureCapturingBodyLetKeepsPerIterationSemantics()
    {
        var engine = new Engine();
        // a capturing body is never slot-eligible, so flattening (and the skip) cannot engage;
        // the per-iteration block environments must keep giving each closure its own binding
        var result = engine.Evaluate("""
            (function () {
                const fns = [];
                for (let i = 0; i < 3; i++) {
                    let v = i * 10;
                    fns.push(function () { return v; });
                }
                return fns.map(function (f) { return f(); }).join(',');
            })()
            """).AsString();

        result.Should().Be("0,10,20");
    }

    [Fact]
    public void ReenteredInnerLetLoopWithConstBodyStaysCorrect()
    {
        var engine = new Engine();
        // the ForBenchmark.ReenteredInnerLetLoop shape with a body lexical: every inner() entry
        // reuses the pooled environment, whose entry reset must keep covering the first iteration
        var result = engine.Evaluate("""
            (function () {
                function inner() {
                    var s = 0;
                    for (let j = 0; j < 4; j++) {
                        const d = j * j;
                        s += d;
                    }
                    return s;
                }
                var total = 0;
                for (var k = 0; k < 5; k++) { total += inner(); }
                return total;
            })()
            """).AsNumber();

        result.Should().Be(70);
    }

    [Fact]
    public void SelfReferencingInitializerCountsAsBeforeUse()
    {
        var engine = new Engine();
        // let x = x reads x while it is still uninitialized: ReferenceError on entry and again on
        // a fresh entry (the loop environment is pooled across entries)
        var immediate = engine.Evaluate("""
            (function () {
                var hits = [];
                function run() {
                    try {
                        for (let i = 0; i < 3; i++) { let x = x; }
                    } catch (e) {
                        hits.push(e instanceof ReferenceError);
                    }
                }
                run(); run();
                return hits.join(',');
            })()
            """).AsString();
        immediate.Should().Be("true,true");

        // conditional self-reference: iteration 0 initializes z without reading it, iteration 1's
        // initializer reads z. The scan must treat the initializer as running before its own name
        // is declared (reject), keeping the reset so the read throws instead of seeing 1
        var conditional = engine.Evaluate("""
            (function () {
                try {
                    for (let i = 0; i < 3; i++) { const z = i === 0 ? 1 : z; }
                } catch (e) {
                    return e instanceof ReferenceError ? 'tdz' : 'other';
                }
                return 'no-throw';
            })()
            """).AsString();
        conditional.Should().Be("tdz");
    }

    [Fact]
    public void HeaderAndOuterShadowingStayCorrect()
    {
        var engine = new Engine();
        // header names are not body slots: reading the header binding in a slot initializer is
        // always after-use and stays eligible
        var header = engine.Evaluate("""
            (function () {
                var acc = [];
                for (let z = 0; z < 2; z++) {
                    const z2 = z * 3;
                    acc.push(z2);
                }
                return acc.join(',');
            })()
            """).AsString();
        header.Should().Be("0,3");

        // body slot shadowing an outer binding: body reads see the freshly initialized inner
        // binding every iteration and the outer binding stays untouched
        var shadow = engine.Evaluate("""
            (function () {
                let z = 'outer';
                var acc = [];
                for (let i = 0; i < 2; i++) {
                    const z = 'inner' + i;
                    acc.push(z);
                }
                acc.push(z);
                return acc.join(',');
            })()
            """).AsString();
        shadow.Should().Be("inner0,inner1,outer");

        // shadow read before the declaration, conditionally so iteration 0 completes: the body's
        // own binding covers the whole block in TDZ, so iteration 1 must throw — never fall back
        // to the outer binding or (scan rejects, reset retained) see iteration 0's stale value
        var beforeDeclaration = engine.Evaluate("""
            (function () {
                let q = 'outer';
                var seen = 'none';
                try {
                    for (let i = 0; i < 2; i++) {
                        if (i === 1) { seen = q; }
                        const q = 'inner' + i;
                    }
                } catch (e) {
                    return (e instanceof ReferenceError) + ':' + seen;
                }
                return 'no-throw:' + seen;
            })()
            """).AsString();
        beforeDeclaration.Should().Be("true:none");
    }

    [Fact]
    public void MultiDeclaratorStatementsScanPerDeclarator()
    {
        var engine = new Engine();
        // within one statement, later declarators may reference earlier ones (accepted, skip active)
        var forward = engine.Evaluate("""
            (function () {
                var acc = [];
                for (let i = 0; i < 2; i++) {
                    const a = i + 1, b = a * 10;
                    acc.push(a + ':' + b);
                }
                return acc.join('|');
            })()
            """).AsString();
        forward.Should().Be("1:10|2:20");

        // ...but an earlier declarator referencing a later one is before-use: the scan rejects,
        // the reset stays, and iteration 1's read throws instead of seeing iteration 0's value
        var backward = engine.Evaluate("""
            (function () {
                try {
                    for (let i = 0; i < 2; i++) {
                        let a = i === 0 ? 0 : b, b = i;
                    }
                } catch (e) {
                    return e instanceof ReferenceError ? 'tdz' : 'other';
                }
                return 'no-throw';
            })()
            """).AsString();
        backward.Should().Be("tdz");
    }

    [Fact]
    public void MultiSlotEligibleBodyReinitializesEverySlot()
    {
        var engine = new Engine();
        // initializer-less let re-initializes to undefined each iteration (i === 0 sets u, later
        // iterations must observe undefined again, not the stale value); a nested block shadowing
        // a declared slot name after its declaration does not affect eligibility
        var result = engine.Evaluate("""
            (function () {
                var acc = [];
                for (let i = 0; i < 3; i++) {
                    const a = i;
                    let b = a * 2;
                    b = b + 1;
                    let u;
                    if (i === 0) u = 'set';
                    { let a = 99; b += a - 99; }
                    acc.push(a + ':' + b + ':' + u);
                }
                return acc.join('|');
            })()
            """).AsString();

        result.Should().Be("0:1:set|1:3:undefined|2:5:undefined");
    }
}
