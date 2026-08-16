using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Class construction and member access on a six-deep inheritance chain.
///
/// <para>Each row gets its own engine, built in <c>[GlobalSetup]</c> and warmed with that row's own
/// script and nothing else (<see cref="IsolatedScript"/>). The class declarations are a fixture every
/// row needs, so they are evaluated on each row's engine — a shared fixture is fine, a shared
/// <em>engine</em> is not.</para>
///
/// <para>This class previously used <c>[IterationSetup]</c> to rebuild the engine, which is the one
/// thing the benchmarking rules here forbid: it forces <c>InvocationCount=1</c> and <c>UnrollFactor=1</c>,
/// which leaks tiered-JIT warm-up into the measured iterations and made identical code report 2.489 ms
/// and 9.811 ms in different runs. Nothing needed it — every row's script is idempotent with respect to
/// engine state (the constructions allocate garbage, <c>GetSet</c> rewrites the same property, and the
/// class-declaration row runs inside an IIFE), so one engine per row serves all iterations. Engine
/// construction and warm-up stay out of the measurement as a result.</para>
/// </summary>
[MemoryDiagnoser]
public class ClassBenchmark
{
    private const string ClassFixture = """
                                        class A { x = 1; };
                                        class B extends A { y = 2; };
                                        class C extends B { z = 3; };
                                        class D extends C { x2 = 1; };
                                        class E extends D { x3 = 1; };
                                        class F extends E { x4 = 1; }
                                        """;

    private IsolatedScript _constructSimple;
    private IsolatedScript _constructDeepInheritance;
    private IsolatedScript _getSet;
    private IsolatedScript _reEvaluateClassDeclarations;

    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(ClassFixture);
        engine.Execute("const target = new F();");
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _constructSimple = IsolatedScript.Warm("new A();", CreateEngine);
        _constructDeepInheritance = IsolatedScript.Warm("new F();", CreateEngine);
        _getSet = IsolatedScript.Warm("target.x4 = 42; target.x4;", CreateEngine);

        // the dom.js / class-factory shape: one engine re-evaluates the same prepared script that
        // declares classes; member definitions come from the per-engine cache while class
        // identities, prototypes, private state and static-block effects stay per-evaluation
        _reEvaluateClassDeclarations = IsolatedScript.Warm(
            """
            (function () {
                class Node {
                    #tag = 'node';
                    static VERSION = 1;
                    static { this.registry = []; }
                    constructor(name) { this.name = name; this.children = []; }
                    appendChild(child) { this.children.push(child); return child; }
                    get childCount() { return this.children.length; }
                    set alias(value) { this.name = value; }
                    describe() { return this.#tag + ':' + this.name; }
                    static create(name) { return new Node(name); }
                }
                class Element extends Node {
                    describe() { return 'element:' + super.describe(); }
                }
                const e = new Element('div');
                e.appendChild(Node.create('span'));
                e.alias = 'main';
                return e.describe() + '/' + e.childCount + '/' + Element.VERSION;
            })();
            """,
            CreateEngine);
    }

    [Benchmark]
    public void ConstructSimple()
    {
        for (var i = 0; i < 400_000; ++i)
        {
            _constructSimple.Run();
        }
    }

    [Benchmark]
    public void ConstructDeepInheritance()
    {
        for (var i = 0; i < 80_000; ++i)
        {
            _constructDeepInheritance.Run();
        }
    }

    [Benchmark]
    public void GetSet()
    {
        for (var i = 0; i < 500_000; ++i)
        {
            _getSet.Run();
        }
    }

    [Benchmark]
    public void ReEvaluateClassDeclarations()
    {
        for (var i = 0; i < 40_000; ++i)
        {
            _reEvaluateClassDeclarations.Run();
        }
    }
}
