using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Value-producing update expressions on function-local (declarative-slot) counters: the
/// increment/decrement result is consumed by the surrounding expression (<c>arr[i++]</c>,
/// <c>acc += i++</c>, <c>while (n--)</c>, <c>++i</c>), so evaluation cannot take the discard-mode
/// fast path and instead resolves the counter through the identifier slot cache. Complements
/// <see cref="FunctionLocalNumberLoopBenchmark"/>, whose <c>i++</c> updates are all discarded.
/// </summary>
[MemoryDiagnoser]
public class UpdateExpressionValueBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _postfixAccumulate;
    private Prepared<Script> _prefixAccumulate;
    private Prepared<Script> _arrayIndexPostInc;
    private Prepared<Script> _whileCountdown;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // postfix i++ consumed as a value every iteration (acc += i++)
        _postfixAccumulate = Engine.PrepareScript("""
            function f() {
                var i = 0;
                var acc = 0;
                while (i < 100000) {
                    acc += i++;
                }
                return acc;
            }
            f();
            """);

        // prefix ++i consumed as a value every iteration (acc += ++i)
        _prefixAccumulate = Engine.PrepareScript("""
            function f() {
                var i = 0;
                var acc = 0;
                while (i < 100000) {
                    acc += ++i;
                }
                return acc;
            }
            f();
            """);

        // sum += arr[i++]: the post-increment result indexes the array and advances the counter
        _arrayIndexPostInc = Engine.PrepareScript("""
            function f() {
                var arr = new Array(100000);
                for (var k = 0; k < 100000; k++) { arr[k] = k & 7; }
                var sum = 0;
                var i = 0;
                while (i < 100000) {
                    sum += arr[i++];
                }
                return sum;
            }
            f();
            """);

        // while (n--): the post-decrement value drives the loop condition
        _whileCountdown = Engine.PrepareScript("""
            function f() {
                var n = 100000;
                var count = 0;
                while (n--) {
                    count++;
                }
                return count;
            }
            f();
            """);

        _engine = new Engine();
        _engine.Evaluate(_postfixAccumulate);
        _engine.Evaluate(_prefixAccumulate);
        _engine.Evaluate(_arrayIndexPostInc);
        _engine.Evaluate(_whileCountdown);
    }

    [Benchmark]
    public JsValue PostfixAccumulate() => _engine.Evaluate(_postfixAccumulate);

    [Benchmark]
    public JsValue PrefixAccumulate() => _engine.Evaluate(_prefixAccumulate);

    [Benchmark]
    public JsValue ArrayIndexPostInc() => _engine.Evaluate(_arrayIndexPostInc);

    [Benchmark]
    public JsValue WhileCountdown() => _engine.Evaluate(_whileCountdown);
}
