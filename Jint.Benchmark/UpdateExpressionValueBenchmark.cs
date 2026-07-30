using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Value-producing update expressions on function-local (declarative-slot) counters: the
/// increment/decrement result is consumed by the surrounding expression (<c>arr[i++]</c>,
/// <c>acc += i++</c>, <c>while (n--)</c>, <c>++i</c>), so evaluation cannot take the discard-mode
/// fast path and instead resolves the counter through the identifier slot cache. Complements
/// <see cref="FunctionLocalNumberLoopBenchmark"/>, whose <c>i++</c> updates are all discarded.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all four row
/// scripts, so each row was measured on an engine carrying the other three rows' globals (every one of
/// them declares <c>f</c>, so they collide outright), their handler-tree entries and their
/// identifier-slot and environment-reuse caches — which is exactly the state these rows exist to
/// measure. That makes a row's number depend on which siblings exist and on what a change did to
/// <em>them</em>. The rows still measure warm updates, and engine construction and warm-up stay in
/// <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class UpdateExpressionValueBenchmark
{
    private IsolatedScript _postfixAccumulate;
    private IsolatedScript _prefixAccumulate;
    private IsolatedScript _arrayIndexPostInc;
    private IsolatedScript _whileCountdown;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // postfix i++ consumed as a value every iteration (acc += i++)
        _postfixAccumulate = IsolatedScript.Warm("""
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
        _prefixAccumulate = IsolatedScript.Warm("""
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
        _arrayIndexPostInc = IsolatedScript.Warm("""
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
        _whileCountdown = IsolatedScript.Warm("""
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
    }

    [Benchmark]
    public JsValue PostfixAccumulate() => _postfixAccumulate.Run();

    [Benchmark]
    public JsValue PrefixAccumulate() => _prefixAccumulate.Run();

    [Benchmark]
    public JsValue ArrayIndexPostInc() => _arrayIndexPostInc.Run();

    [Benchmark]
    public JsValue WhileCountdown() => _whileCountdown.Run();
}
