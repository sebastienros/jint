using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ClassBenchmark
{
    private Engine _engine;

    [IterationSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute("""
                        class A { x = 1; }; 
                        class B extends A { y = 2; };
                        class C extends B { z = 3; }; 
                        class D extends C { x2 = 1; };
                        class E extends D { x3 = 1; };
                        class F extends E { x4 = 1; }
                        """);
        _engine.Execute("const target = new F();");
    }

    [Benchmark]
    public void ConstructSimple()
    {
        var script = Engine.PrepareScript("new A();");
        for (var i = 0; i < 400_000; ++i)
        {
            _engine.Evaluate(script);
        }
    }

    [Benchmark]
    public void ConstructDeepInheritance()
    {
        var script = Engine.PrepareScript("new F();");
        for (var i = 0; i < 80_000; ++i)
        {
            _engine.Evaluate(script);
        }
    }

    [Benchmark]
    public void GetSet()
    {
        var script = Engine.PrepareScript("target.x4 = 42; target.x4;");
        for (var i = 0; i < 500_000; ++i)
        {
            _engine.Evaluate(script);
        }
    }

    [Benchmark]
    public void ReEvaluateClassDeclarations()
    {
        // the dom.js / class-factory shape: one engine re-evaluates the same prepared script that
        // declares classes; member definitions come from the per-engine cache while class
        // identities, prototypes, private state and static-block effects stay per-evaluation
        var script = Engine.PrepareScript("""
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
            """);
        for (var i = 0; i < 40_000; ++i)
        {
            _engine.Evaluate(script);
        }
    }
}
