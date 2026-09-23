#nullable enable

namespace Jint.Tests.Runtime;

public class ModuloOperandLaneTests
{
    [TestCase("(() => { let sum = 0; for (let i = 0; i < 100000; i++) sum += i % 97; return sum; })()", 4799685)]
    [TestCase("(() => { const o = {n:17}; let sum = 0; for (let i = 0; i < 100000; i++) sum += o.n; return sum; })()", 1700000)]
    [TestCase("(() => { function f(n) { return n % 97; } let sum = 0; for (let i = 0; i < 100000; i++) sum += f(i); return sum; })()", 4799685)]
    public void RepresentativeScriptsPreserveResults(string source, int expected)
    {
        var engine = new Engine(options => options.LimitExecutionTime(TimeSpan.FromSeconds(10)));
        var script = Engine.PrepareScript(source);
        for (var i = 0; i < 3; i++)
        {
            engine.Evaluate(script).Should().Be(expected);
        }
    }

    [Test]
    public void NumericOperandsMatchGenericRemainder()
    {
        var engine = new Engine();
        engine.Evaluate("""
            (() => {
                const values = [0, -0, 1, -1, 97, -97, 0.5, -0.5, NaN, Infinity,
                    -Infinity, 2147483647, -2147483648, 2147483648, 1e100, 5e-324];
                function slot(a, b) { a *= 1; return a % b; }
                function generic(a, b) { return ({value:a}).value % ({value:b}).value; }
                function rightConstant(a) { a *= 1; return a % 97; }
                function leftConstant(b) { return 97 % b; }
                for (const a of values) {
                    if (!Object.is(rightConstant(a), generic(a, 97))) return false;
                    if (!Object.is(leftConstant(a), generic(97, a))) return false;
                    for (const b of values) {
                        if (!Object.is(slot(a, b), generic(a, b))) return false;
                    }
                }
                return true;
            })()
            """).Should().Be(true);
    }

    [Test]
    public void DeclinedOperandsPreserveCoercionAndErrors()
    {
        var engine = new Engine();
        engine.Evaluate("""
            (() => {
                let log = '';
                function f(a, b) { return a % b; }
                const a = {valueOf() { log += 'a'; return 11; }};
                const b = {valueOf() { log += 'b'; return 3; }};
                if (f(a, b) !== 2 || log !== 'ab') return false;
                if (f(11n, 3n) !== 2n || f('11', '3') !== 2) return false;
                try { f(11n, 3); return false; } catch(e) { if (!(e instanceof TypeError)) return false; }
                try { f(Symbol(), 3); return false; } catch(e) { if (!(e instanceof TypeError)) return false; }
                try { { const result = x % 3; let x = 11; } return false; }
                catch(e) { if (!(e instanceof ReferenceError)) return false; }
                return true;
            })()
            """).Should().Be(true);
    }

    [Test]
    public void GetterAndSuspensionKeepEvaluationOrder()
    {
        var engine = new Engine();
        engine.Evaluate("""
            (() => {
                let reads = 0;
                const obj = { get n() { reads++; return 11; }};
                function f() { with (obj) { return n % 3; } }
                if (f() !== 2 || f() !== 2 || reads !== 2) return false;
                let a = 11;
                function* g() { return a % (yield 3); }
                const it = g();
                if (it.next().value !== 3) return false;
                a = 99;
                return it.next(3).value === 2;
            })()
            """).Should().Be(true);
    }

    [Test]
    public void ReboundClosuresAndGlobalAccessorsInvalidateNumericReads()
    {
        var engine = new Engine();
        engine.Evaluate("""
            (() => {
                function make(n) { return function() { return n % 3; }; }
                const a = make(11), b = make(12);
                for (let i = 0; i < 5; i++) if (a() !== 2 || b() !== 0) return false;
                return true;
            })()
            """).Should().Be(true);
        engine.Evaluate("""
            globalThis.dividend = 11;
            function globalRemainder() { return dividend % 3; }
            globalRemainder(); globalRemainder(); globalRemainder();
            let reads = 0;
            Object.defineProperty(globalThis, 'dividend', { configurable: true,
                get() { reads++; return 12; } });
            globalRemainder() === 0 && globalRemainder() === 0 && reads === 2
            """).Should().Be(true);
    }

    [Test]
    public void SharedPreparationAndBindingChangesRemainIndependent()
    {
        var script = Engine.PrepareScript("""
            (() => { let sum = 0; for (let i = 0; i < 100000; i++) sum += i % 97; return sum; })()
            """);
        var first = new Engine();
        var second = new Engine();
        for (var i = 0; i < 3; i++)
        {
            first.Evaluate(script).Should().Be(4799685);
            second.Evaluate(script).Should().Be(4799685);
        }
        first.Evaluate("""
            function remainder(a, b) { return a % b; }
            remainder(11, 3); remainder(11, 3);
            remainder('12', 5) === 2 && remainder(12n, 5n) === 2n
            """).Should().Be(true);
    }
}
