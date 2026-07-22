namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the semantics of the per-engine function-definition cache applied to class evaluation:
/// re-evaluating the same class AST (factory functions, re-run prepared scripts) reuses the
/// members' interpreter definitions, while closures, home objects, environments and private
/// state stay strictly per evaluation.
/// </summary>
public class ClassReEvaluationTests
{
    [Fact]
    public void ClassFactoriesProduceIndependentClasses()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                function make(tag) {
                    return class {
                        static origin = tag;
                        #secret = tag + '!';
                        constructor() { this.made = tag; }
                        get secret() { return this.#secret; }
                        describe() { return tag + ':' + this.made; }
                        static { this.stamped = tag.toUpperCase(); }
                    };
                }
                const A = make('a'), B = make('b');
                const a = new A(), b = new B();
                return [
                    A !== B, A.prototype !== B.prototype,
                    A.prototype.describe !== B.prototype.describe,
                    a.describe(), b.describe(),
                    a.secret, b.secret,
                    A.origin, B.origin, A.stamped, B.stamped,
                    a instanceof A, a instanceof B
                ].join(',');
            })()
            """).AsString();

        result.Should().Be("true,true,true,a:a,b:b,a!,b!,a,b,A,B,true,false");
    }

    [Fact]
    public void SuperAndHomeObjectStayPerEvaluation()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                function make(base) {
                    return class extends base { hello() { return 'sub-' + super.hello(); } };
                }
                class B1 { hello() { return 'one'; } }
                class B2 { hello() { return 'two'; } }
                const S1 = make(B1), S2 = make(B2);
                return new S1().hello() + '|' + new S2().hello();
            })()
            """).AsString();

        result.Should().Be("sub-one|sub-two");
    }

    [Fact]
    public void RepeatedPreparedScriptEvaluationStaysCorrect()
    {
        var engine = new Engine();
        // top-level `class` is a lexical declaration and correctly throws on re-declaration, so the
        // re-evaluation pattern wraps it — the class AST (and its cached member definitions) still
        // repeats across evaluations
        var prepared = Engine.PrepareScript("""
            (function () {
                class Point { #x; constructor(x) { this.#x = x; } get x() { return this.#x; } double() { return this.x * 2; } }
                return new Point(21).double();
            })();
            """);

        engine.Evaluate(prepared).AsNumber().Should().Be(42);
        engine.Evaluate(prepared).AsNumber().Should().Be(42);
        engine.Evaluate(prepared).AsNumber().Should().Be(42);
    }
}
