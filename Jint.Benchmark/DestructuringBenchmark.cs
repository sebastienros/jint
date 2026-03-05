using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class DestructuringBenchmark
{
    private Prepared<Script> _simpleObjectDestructuring;
    private Prepared<Script> _objectDestructuringWithDefaults;
    private Prepared<Script> _nestedObjectDestructuring;
    private Prepared<Script> _simpleArrayDestructuring;
    private Prepared<Script> _arrayDestructuringWithRest;
    private Prepared<Script> _forOfDestructuring;
    private Prepared<Script> _parameterDestructuring;
    private Prepared<Script> _computedKeyDestructuring;
    private Prepared<Script> _mixedNested;

    [GlobalSetup]
    public void Setup()
    {
        _simpleObjectDestructuring = Engine.PrepareScript("""
            var obj = {a: 1, b: 2, c: 3};
            for (var i = 0; i < 500000; i++) {
                var {a, b, c} = obj;
            }
            """);

        _objectDestructuringWithDefaults = Engine.PrepareScript("""
            var obj = {};
            for (var i = 0; i < 500000; i++) {
                var {a = 1, b = 2, c = 3} = obj;
            }
            """);

        _nestedObjectDestructuring = Engine.PrepareScript("""
            var obj = {a: {b: 1, c: 2}};
            for (var i = 0; i < 500000; i++) {
                var {a: {b, c}} = obj;
            }
            """);

        _simpleArrayDestructuring = Engine.PrepareScript("""
            var arr = [1, 2, 3];
            for (var i = 0; i < 500000; i++) {
                var [a, b, c] = arr;
            }
            """);

        _arrayDestructuringWithRest = Engine.PrepareScript("""
            var arr = [1, 2, 3, 4, 5];
            for (var i = 0; i < 500000; i++) {
                var [a, ...rest] = arr;
            }
            """);

        _forOfDestructuring = Engine.PrepareScript("""
            var items = [];
            for (var i = 0; i < 10000; i++) {
                items.push({a: i, b: i + 1});
            }
            var s = 0;
            for (var {a, b} of items) {
                s += a + b;
            }
            """);

        _parameterDestructuring = Engine.PrepareScript("""
            function f({a, b}) { return a + b; }
            var obj = {a: 1, b: 2};
            var s = 0;
            for (var i = 0; i < 500000; i++) {
                s += f(obj);
            }
            """);

        _computedKeyDestructuring = Engine.PrepareScript("""
            var obj = {x: 42};
            var key = 'x';
            for (var i = 0; i < 500000; i++) {
                var {[key]: val} = obj;
            }
            """);

        _mixedNested = Engine.PrepareScript("""
            var obj = {a: 1, items: [10, 20]};
            for (var i = 0; i < 500000; i++) {
                var {a, items: [x, y]} = obj;
            }
            """);
    }

    [Benchmark]
    public void SimpleObject() => new Engine().Execute(_simpleObjectDestructuring);

    [Benchmark]
    public void ObjectWithDefaults() => new Engine().Execute(_objectDestructuringWithDefaults);

    [Benchmark]
    public void NestedObject() => new Engine().Execute(_nestedObjectDestructuring);

    [Benchmark]
    public void SimpleArray() => new Engine().Execute(_simpleArrayDestructuring);

    [Benchmark]
    public void ArrayWithRest() => new Engine().Execute(_arrayDestructuringWithRest);

    [Benchmark]
    public void ForOfDestructuring() => new Engine().Execute(_forOfDestructuring);

    [Benchmark]
    public void ParameterDestructuring() => new Engine().Execute(_parameterDestructuring);

    [Benchmark]
    public void ComputedKey() => new Engine().Execute(_computedKeyDestructuring);

    [Benchmark]
    public void MixedNested() => new Engine().Execute(_mixedNested);
}
