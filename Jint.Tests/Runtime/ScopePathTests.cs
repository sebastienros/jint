namespace Jint.Tests.Runtime;

/// <summary>
/// Identifier reads remember where a slot-stored binding was found by the layout of the environments on the
/// way (<c>ScopePath</c>), so fresh activations of the same function reuse the location. These pin the cases
/// where the layout alone would describe the wrong binding. The injection cases do not reach a path today —
/// a direct eval anywhere in a function keeps it, and every scope it encloses, off fixed slots — so they pin
/// the behaviour the injection mark guarantees whatever the slot-eligibility rules become.
/// </summary>
public class ScopePathTests
{
    private static string Run(string script) => new Engine().Evaluate(script).ToString();

    [Test]
    public void FreshActivationsReadTheirOwnBindings()
    {
        Run("""
            function outer(a) { const inner = (b) => a + b; return inner(1); }
            let r = '';
            for (let i = 0; i < 5; i++) r += outer(i * 10) + ',';
            r
            """).Should().Be("1,11,21,31,41,");
    }

    [Test]
    public void LoopIterationsReadTheirOwnBindings()
    {
        Run("""
            const fns = [];
            for (let i = 0; i < 4; i++) { const j = i * 2; fns.push(() => i + j); }
            fns.map(f => f()).join()
            """).Should().Be("0,3,6,9");
    }

    [Test]
    public void BindingInjectedBeforeThePathWasBuiltShadows()
    {
        // `a` closes over an activation of mid into which eval injected `x`; `b` over one without. Reading
        // through b first builds a path saying mid's layout does not own x; a must still see its own x.
        Run("""
            function outer() {
              var x = 'outer';
              // A function expression, not a declaration: a declaration would keep outer off fixed slots.
              const mid = function (code) { eval(code); return function () { return x; }; };
              const a = mid('var x = "mid"');
              const b = mid('');
              const first = b();
              return first + ',' + a() + ',' + b() + ',' + a();
            }
            outer()
            """).Should().Be("outer,mid,outer,mid");
    }

    [Test]
    public void BindingInjectedAfterThePathWasBuiltShadows()
    {
        Run("""
            function outer() {
              var x = 'outer';
              const mid = function (code) { const read = function () { return x; }; const before = read(); eval(code); return before + '/' + read(); };
              return mid('') + ',' + mid('var x = "mid"') + ',' + mid('');
            }
            outer()
            """).Should().Be("outer/outer,outer/mid,outer/outer");
    }

    [Test]
    public void AnnexBFunctionCopyShadows()
    {
        Run("""
            function outer() {
              var f = () => 'outer';
              const mid = function (declare) {
                const read = () => typeof f === 'function' ? f() : 'none';
                const before = read();
                if (declare) { function f() { return 'block'; } }
                return before + '/' + read();
              };
              return mid(false) + ',' + mid(true) + ',' + mid(false);
            }
            outer()
            """).Should().Be("none/none,none/block,none/none");
    }

    [Test]
    public void ReadsThroughDifferentDefinitionsDoNotShareAPath()
    {
        Run("""
            function make(kind) {
              if (kind) { return function (v) { return () => v + 'A'; }; }
              return function (w) { const v = 'shadow'; return () => v + 'B'; };
            }
            const reader = (fn, arg) => fn(arg)();
            reader(make(true), '1') + reader(make(false), '2') + reader(make(true), '3')
            """).Should().Be("1AshadowB3A");
    }

    [Test]
    public void WithObjectBetweenReaderAndBindingIsHonoured()
    {
        Run("""
            function outer(o) { var x = 'outer'; with (o) { return (() => x)(); } }
            outer({}) + ',' + outer({ x: 'with' }) + ',' + outer({})
            """).Should().Be("outer,with,outer");
    }

    [Test]
    public void APathHitPinsTheChainOfTheActivationItFound()
    {
        // A path hit re-pins the identity cache and memoizes the chain it validated, so the later reads in the
        // same activation take the identity lanes; they must still land on that activation's bindings, and the
        // next activation's first read must move them.
        Run("""
            function outer(n) {
              let total = 0;
              const add = () => { for (let i = 0; i < 3; i++) { total += n; } };
              add();
              add();
              return total;
            }
            [1, 2, 3, 4].map(outer).join()
            """).Should().Be("6,12,18,24");
    }

    [Test]
    public void ASiteThatCannotBuildAPathResolvesPastTheRetryCap()
    {
        // A with object between the reader and the binding means no path can be built; after a few attempts the
        // site stops trying, and the ordinary walk keeps answering — including for the with object's own x.
        Run("""
            function outer(o) { var x = 'o'; with (o) { return (() => x)(); } }
            let r = '';
            for (let i = 0; i < 10; i++) r += outer(i % 3 === 0 ? { x: 'w' } : {});
            r
            """).Should().Be("woowoowoow");
    }
}
