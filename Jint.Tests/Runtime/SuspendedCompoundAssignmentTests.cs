#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A compound assignment reads its left-hand side before it evaluates the right-hand side
/// (https://tc39.es/ecma262/#sec-assignment-operators-runtime-semantics-evaluation, <c>lval = GetValue(lref)</c>
/// ahead of the right operand), so when the right-hand side suspends on a <c>yield</c> or an <c>await</c>, the
/// value the resume combines is the one read before the suspension, not whatever the binding holds by then.
/// <para>
/// A left-hand side that is an identifier used to be read a second time on resume, so a write made while the
/// function was suspended leaked into the result: <c>n += (yield inc)</c> answered 16 where V8 answers 6, and a
/// logical assignment re-tested the new value, short-circuiting where it should have assigned
/// (sebastienros/jint#4268). Every row here was checked against Node 24, and a suspendable frame takes the
/// materialized lane whatever the binding, so each binding kind is one test over every operator.
/// </para>
/// </summary>
public class SuspendedCompoundAssignmentTests
{
    private static string Run(string script) => new Engine().Evaluate(script).AsString();

    private static string RunAsync(string script) => new Engine().Evaluate(script).UnwrapIfPromise().AsString();

    /// <summary>
    /// The operator, the binding's value before the suspension, the value written to it while suspended, and the
    /// result once the right-hand side resumes with 3. Each written value would change the result if it were read.
    /// </summary>
    public static TestCases<string, string, string, string> Operators() => new()
    {
        { "+=", "7", "12", "10" },
        { "-=", "7", "12", "4" },
        { "*=", "7", "12", "21" },
        { "/=", "7", "12", "2.3333333333333335" },
        { "%=", "7", "12", "1" },
        { "**=", "7", "12", "343" },
        { "<<=", "7", "12", "56" },
        { ">>=", "-7", "12", "-1" },
        { ">>>=", "-7", "12", "536870911" },
        { "&=", "7", "12", "3" },
        { "|=", "7", "12", "7" },
        { "^=", "7", "12", "4" },

        // The logical forms test the value read before the suspension and assign; re-testing the written value
        // would short-circuit and leave it standing.
        { "??=", "null", "12", "3" },
        { "||=", "0", "12", "3" },
        { "&&=", "7", "0", "3" },
    };

    // ------------------------------------------------------------------ yield

    [TestCaseSource(nameof(Operators))]
    public void GeneratorLocalVar(string op, string initial, string written, string expected)
    {
        Run($$"""
            function* g() {
              var n = {{initial}};
              const mutate = () => { n = {{written}}; };
              var r = (n {{op}} (yield mutate));
              return r + '|' + n;
            }
            var it = g();
            it.next().value();
            it.next(3).value;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void GeneratorLocalLet(string op, string initial, string written, string expected)
    {
        Run($$"""
            function* g() {
              let n = {{initial}};
              const mutate = () => { n = {{written}}; };
              let r = (n {{op}} (yield mutate));
              return r + '|' + n;
            }
            var it = g();
            it.next().value();
            it.next(3).value;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void GeneratorLocalInStatementPosition(string op, string initial, string written, string expected)
    {
        Run($$"""
            function* g() {
              var n = {{initial}};
              const mutate = () => { n = {{written}}; };
              n {{op}} (yield mutate);
              return n + '|' + n;
            }
            var it = g();
            it.next().value();
            it.next(3).value;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void GeneratorEnclosingFunctionVar(string op, string initial, string written, string expected)
    {
        Run($$"""
            function outer() {
              var n = {{initial}};
              function* g() { var r = (n {{op}} (yield)); return r + '|' + n; }
              var it = g();
              it.next();
              n = {{written}};
              return it.next(3).value;
            }
            outer();
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void GeneratorGlobalVar(string op, string initial, string written, string expected)
    {
        Run($$"""
            var n = {{initial}};
            function* g() { var r = (n {{op}} (yield)); return r + '|' + n; }
            var it = g();
            it.next();
            n = {{written}};
            it.next(3).value;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void GeneratorGlobalVarInStatementPosition(string op, string initial, string written, string expected)
    {
        Run($$"""
            var n = {{initial}};
            function* g() { n {{op}} (yield); return n + '|' + n; }
            var it = g();
            it.next();
            n = {{written}};
            it.next(3).value;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void GeneratorImplicitGlobal(string op, string initial, string written, string expected)
    {
        Run($$"""
            n = {{initial}};
            function* g() { var r = (n {{op}} (yield)); return r + '|' + n; }
            var it = g();
            it.next();
            n = {{written}};
            it.next(3).value;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void GeneratorGlobalLet(string op, string initial, string written, string expected)
    {
        Run($$"""
            let n = {{initial}};
            function* g() { var r = (n {{op}} (yield)); return r + '|' + n; }
            var it = g();
            it.next();
            n = {{written}};
            it.next(3).value;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void GeneratorWithScopeBinding(string op, string initial, string written, string expected)
    {
        Run($$"""
            var o = { n: {{initial}} };
            function* g() { with (o) { var r = (n {{op}} (yield)); } return r + '|' + o.n; }
            var it = g();
            it.next();
            o.n = {{written}};
            it.next(3).value;
            """).Should().Be(expected + "|" + expected);
    }

    // ------------------------------------------------------------------ await

    [TestCaseSource(nameof(Operators))]
    public void AsyncLocalVar(string op, string initial, string written, string expected)
    {
        RunAsync($$"""
            var mutate;
            async function f() {
              var n = {{initial}};
              mutate = () => { n = {{written}}; };
              var r = (n {{op}} (await 3));
              return r + '|' + n;
            }
            var p = f();
            mutate();
            p;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void AsyncLocalLet(string op, string initial, string written, string expected)
    {
        RunAsync($$"""
            var mutate;
            async function f() {
              let n = {{initial}};
              mutate = () => { n = {{written}}; };
              let r = (n {{op}} (await 3));
              return r + '|' + n;
            }
            var p = f();
            mutate();
            p;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void AsyncLocalInStatementPosition(string op, string initial, string written, string expected)
    {
        RunAsync($$"""
            var mutate;
            async function f() {
              var n = {{initial}};
              mutate = () => { n = {{written}}; };
              n {{op}} (await 3);
              return n + '|' + n;
            }
            var p = f();
            mutate();
            p;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void AsyncEnclosingFunctionVar(string op, string initial, string written, string expected)
    {
        RunAsync($$"""
            function outer() {
              var n = {{initial}};
              async function f() { var r = (n {{op}} (await 3)); return r + '|' + n; }
              var p = f();
              n = {{written}};
              return p;
            }
            outer();
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void AsyncGlobalVar(string op, string initial, string written, string expected)
    {
        RunAsync($$"""
            var n = {{initial}};
            async function f() { var r = (n {{op}} (await 3)); return r + '|' + n; }
            var p = f();
            n = {{written}};
            p;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void AsyncGlobalVarInStatementPosition(string op, string initial, string written, string expected)
    {
        RunAsync($$"""
            var n = {{initial}};
            async function f() { n {{op}} (await 3); return n + '|' + n; }
            var p = f();
            n = {{written}};
            p;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void AsyncImplicitGlobal(string op, string initial, string written, string expected)
    {
        RunAsync($$"""
            n = {{initial}};
            async function f() { var r = (n {{op}} (await 3)); return r + '|' + n; }
            var p = f();
            n = {{written}};
            p;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void AsyncGlobalLet(string op, string initial, string written, string expected)
    {
        RunAsync($$"""
            let n = {{initial}};
            async function f() { var r = (n {{op}} (await 3)); return r + '|' + n; }
            var p = f();
            n = {{written}};
            p;
            """).Should().Be(expected + "|" + expected);
    }

    [TestCaseSource(nameof(Operators))]
    public void AsyncWithScopeBinding(string op, string initial, string written, string expected)
    {
        RunAsync($$"""
            var o = { n: {{initial}} };
            async function f() { with (o) { var r = (n {{op}} (await 3)); } return r + '|' + o.n; }
            var p = f();
            o.n = {{written}};
            p;
            """).Should().Be(expected + "|" + expected);
    }

    // The resume stores through the Reference resolved before the suspension, not a fresh resolution, so a `with`
    // binding deleted while suspended is set on the object again (PutValue on an object Environment Record) and the
    // outer variable is left alone. This is the answer test262's S11.13.2_A5.*_T1 holds a non-suspending compound
    // assignment to, which Jint passes; V8 is the outlier here, re-resolving the name and answering "10|undefined|10".

    [Test]
    public void GeneratorWithScopeBindingDeletedWhileSuspended()
    {
        Run("""
            var n = 'outer';
            var o = { n: 7 };
            function* g() { with (o) { var r = (n += (yield)); } return r; }
            var it = g();
            it.next();
            delete o.n;
            var r = it.next(3).value;
            r + '|' + o.n + '|' + n;
            """).Should().Be("10|10|outer");
    }

    [Test]
    public void AsyncWithScopeBindingDeletedWhileSuspended()
    {
        RunAsync("""
            var n = 'outer';
            var o = { n: 7 };
            async function f() { with (o) { var r = (n += (await 3)); } return r; }
            var p = f();
            delete o.n;
            p.then(r => r + '|' + o.n + '|' + n);
            """).Should().Be("10|10|outer");
    }

    // ------------------------------------------------------------------ other suspendable frames

    [Test]
    public void AsyncArrow()
    {
        RunAsync("""
            var mutate;
            var f = async () => { var n = 7; mutate = () => { n = 12; }; var r = (n += (await 3)); return r + '|' + n; };
            var p = f();
            mutate();
            p;
            """).Should().Be("10|10");
    }

    [Test]
    public void AsyncGeneratorAwait()
    {
        RunAsync("""
            var mutate;
            async function* g() { var n = 7; mutate = () => { n = 12; }; var r = (n += (await 3)); yield r + '|' + n; }
            var it = g();
            var p = it.next();
            mutate();
            p.then(x => x.value);
            """).Should().Be("10|10");
    }

    [Test]
    public void AsyncGeneratorYield()
    {
        RunAsync("""
            var mutate;
            async function* g() { var n = 7; mutate = () => { n = 12; }; var r = (n += (yield 'first')); return r + '|' + n; }
            (async () => { var it = g(); await it.next(); mutate(); return (await it.next(3)).value; })();
            """).Should().Be("10|10");
    }

    [Test]
    public void ModuleTopLevelAwait()
    {
        // A module with a top-level await runs its body as an async function, so it suspends the same way.
        var engine = new Engine();
        engine.Modules.Add("main", """
            let n = 7;
            Promise.resolve().then(() => { n = 12; });
            const r = (n += await 3);
            export const result = r + '|' + n;
            """);

        engine.Modules.Import("main").Get("result").AsString().Should().Be("10|10");
    }

    [Test]
    public void DelegatingYield()
    {
        Run("""
            var mutate;
            function* inner() { yield 'a'; return 3; }
            function* g() { var n = 7; mutate = () => { n = 12; }; var r = (n += yield* inner()); return r + '|' + n; }
            var it = g();
            it.next();
            mutate();
            it.next().value;
            """).Should().Be("10|10");
    }

    [Test]
    public void RightHandSideSuspendingTwice()
    {
        // The resume after the first yield suspends again at the second; the value read before the first is kept
        // across both.
        Run("""
            var mutate;
            function* g() { var n = 7; mutate = (v) => { n = v; }; var r = (n += (yield 'a') + (yield 'b')); return r + '|' + n; }
            var it = g();
            it.next();
            mutate(12);
            it.next(1);
            mutate(20);
            it.next(2).value;
            """).Should().Be("10|10");
    }

    // ------------------------------------------------------------------ string append lanes

    // A long string, so that the appends made while the frame is suspended — by a closure, or at the top level,
    // neither of which is suspendable — take the in-place lane that extends a string its binding owns.

    [Test]
    public void StringAppendedInPlaceByAClosureWhileSuspendedAtAYield()
    {
        Run("""
            function* g() {
              let s = '';
              for (let i = 0; i < 100; i++) s += 'abcdefghij';
              const mutate = () => { for (let i = 0; i < 10; i++) s += 'X'; };
              var r = (s += (yield mutate));
              return r.length + '|' + s.length + '|' + s.indexOf('X') + '|' + s.slice(-2);
            }
            var it = g();
            it.next().value();
            it.next('Y').value;
            """).Should().Be("1001|1001|-1|jY");
    }

    [Test]
    public void StringTheBindingOwnedWhenReadIsNotExtendedWhileSuspended()
    {
        // `grow` leaves `s` owned by its binding, extensible in place. The compound assignment's read is what ends
        // that ownership, so the appends made while suspended allocate rather than extend the value it read.
        Run("""
            function* g() {
              let s = '';
              const grow = (k) => { for (let i = 0; i < k; i++) s += 'abcdefghij'; };
              grow(100);
              var r = (s += (yield grow));
              return r.length + '|' + s.length + '|' + s.slice(-2);
            }
            var it = g();
            it.next().value(1);
            it.next('Y').value;
            """).Should().Be("1001|1001|jY");
    }

    [Test]
    public void GlobalStringAppendedInPlaceWhileSuspendedAtAYield()
    {
        Run("""
            var s = '';
            for (var i = 0; i < 100; i++) s += 'abcdefghij';
            function* g() { var r = (s += (yield)); return r.length + '|' + s.length + '|' + s.indexOf('X') + '|' + s.slice(-2); }
            var it = g();
            it.next();
            for (var i = 0; i < 10; i++) s += 'X';
            it.next('Y').value;
            """).Should().Be("1001|1001|-1|jY");
    }

    [Test]
    public void GlobalStringAppendedInPlaceWhileSuspendedAtAYieldInStatementPosition()
    {
        Run("""
            var s = '';
            for (var i = 0; i < 100; i++) s += 'abcdefghij';
            function* g() { s += (yield); return s.length + '|' + s.indexOf('X') + '|' + s.slice(-2); }
            var it = g();
            it.next();
            for (var i = 0; i < 10; i++) s += 'X';
            it.next('Y').value;
            """).Should().Be("1001|-1|jY");
    }

    [Test]
    public void StringAppendedInPlaceByAClosureWhileSuspendedAtAnAwait()
    {
        RunAsync("""
            var mutate;
            async function f() {
              let s = '';
              for (let i = 0; i < 100; i++) s += 'abcdefghij';
              mutate = () => { for (let i = 0; i < 10; i++) s += 'X'; };
              var r = (s += (await 'Y'));
              return r.length + '|' + s.length + '|' + s.indexOf('X') + '|' + s.slice(-2);
            }
            var p = f();
            mutate();
            p;
            """).Should().Be("1001|1001|-1|jY");
    }

    [Test]
    public void GlobalStringAppendedInPlaceWhileSuspendedAtAnAwait()
    {
        RunAsync("""
            var s = '';
            for (var i = 0; i < 100; i++) s += 'abcdefghij';
            async function f() { var r = (s += (await 'Y')); return r.length + '|' + s.length + '|' + s.indexOf('X') + '|' + s.slice(-2); }
            var p = f();
            for (var i = 0; i < 10; i++) s += 'X';
            p;
            """).Should().Be("1001|1001|-1|jY");
    }
}
