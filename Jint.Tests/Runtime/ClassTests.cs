using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class ClassTests
{
    [Test]
    public void IsBlockScoped()
    {
        const string Script = @"
            class C {}
            var c1 = C;
            {
              class C {}
              var c2 = C;
            }
            return C === c1;";

        var engine = new Engine();
        engine.Evaluate(Script).AsBoolean().Should().BeTrue();
    }

    [Test]
    public void CanDestructureNestedMembers()
    {
        const string Script = @"
            class Board {
                constructor () {
                    this.grid = {width: 10, height: 10}
                }

                get width () {
                    const {grid} = this
                    return grid.width
                }

                get doubleWidth () {
                    const {width} = this
                    return width * 2
                }
            }";

        var engine = new Engine();
        engine.Execute(Script);

        engine.Evaluate("var board = new Board()");
        engine.Evaluate("board.width").Should().Be(10);
        engine.Evaluate("board.doubleWidth ").Should().Be(20);
    }

    [Test]
    public void PrivateMemberAccessOutsideOfClass()
    {
        var ex = Invoking(() => new Engine().Evaluate
        (
            """
            class A { }
            new A().#nonexistent = 1;
            """
        )).Should().ThrowExactly<JavaScriptException>().Which;

        ex.Message.Should().Be("Private field '#nonexistent' must be declared in an enclosing class (<anonymous>:2:9)");
    }

    [Test]
    public void PrivateMemberAccessAgainstUnknownMemberInConstructor()
    {
        var ex = Invoking(() => new Engine().Evaluate
        (
            """
            class A { constructor() { #nonexistent = 2; } }
            new A();
            """
        )).Should().ThrowExactly<JavaScriptException>().Which;

        ex.Message.Should().Be("Unexpected identifier '#nonexistent' (<anonymous>:1:27)");
    }

    /// <summary>
    /// The name a class was defined under is the one a reader recognizes, and it lives on the constructor's
    /// own "name" property - the constructor *method's* definition has no name of its own. The nameless
    /// phrasing is what V8 answers for a class that never acquired a name.
    /// </summary>
    [TestCase("class C {}; [1].map(C);", "Class constructor C cannot be invoked without 'new'")]
    [TestCase("[1].map(class C {});", "Class constructor C cannot be invoked without 'new'")]
    [TestCase("[1].map(class {});", "Class constructors cannot be invoked without 'new'")]
    [TestCase("const D = class {}; [1].map(D);", "Class constructor D cannot be invoked without 'new'")]
    [TestCase("class E { constructor() {} }; E();", "Class constructor E cannot be invoked without 'new'")]
    [TestCase("class B {}; class F extends B {}; F();", "Class constructor F cannot be invoked without 'new'")]
    [TestCase("const o = {}; o.h = class {}; o.h();", "Class constructors cannot be invoked without 'new'")]
    [TestCase("const o = { g: class {} }; o.g();", "Class constructor g cannot be invoked without 'new'")]
    public void ClassConstructorCalledWithoutNewNamesTheClass(string script, string expected)
    {
        var ex = Invoking(() => new Engine().Evaluate(script))
            .Should().ThrowExactly<JavaScriptException>().Which;

        ex.Error.Get("message").AsString().Should().Be(expected);
    }

    /// <summary>
    /// <see href="https://tc39.es/ecma262/#sec-super-keyword-runtime-semantics-evaluation">
    /// SuperProperty : super [ Expression ]</see> evaluates Expression (steps 3 and 4) and only then calls
    /// MakeSuperPropertyReference, whose <c>GetSuperBase</c> reads <c>[[HomeObject]].[[Prototype]]</c>. So an
    /// Expression that re-points the home object's prototype is observed by the very lookup it is part of.
    /// Jint resolved the super base first, and the read went to the old prototype. test262 covers it in
    /// <c>staging/sm/class/superPropOrdering.js</c>.
    /// </summary>
    [Test]
    public void ComputedSuperPropertyResolvesItsBaseAfterThePropertyExpression()
    {
        const string Script = """
            class Base { constructor() {} }
            class Derived extends Base {
                read() { return super[detach()]; }
            }
            function detach() { Object.setPrototypeOf(Derived.prototype, null); return 'x'; }

            try { new Derived().read(); return 'did not throw'; }
            catch (e) { return e.constructor.name; }
            """;

        new Engine().Evaluate($"(function () {{ {Script} }})()").AsString().Should().Be("TypeError");
    }

    /// <summary>
    /// A non-computed <c>super.name</c> has no such expression, so its base is resolved before the argument
    /// list — the same re-pointing during an argument leaves the already-resolved method alone.
    /// </summary>
    [Test]
    public void NonComputedSuperPropertyResolvesItsBaseBeforeTheArguments()
    {
        const string Script = """
            class Base { constructor() {} method() { return 'called'; } }
            class Derived extends Base {
                call() { return super.method(detach()); }
            }
            function detach() { Object.setPrototypeOf(Derived.prototype, null); return 0; }

            new Derived().call();
            """;

        new Engine().Evaluate(Script).AsString().Should().Be("called");
    }

    /// <summary>
    /// Deferring the base must not disturb the ordinary computed cases: the property expression's value is
    /// still taken before the right-hand side runs, and the reference still writes through the super base's
    /// [[Set]] onto the receiver.
    /// </summary>
    [Test]
    public void ComputedSuperPropertyStillReadsAndWritesNormally()
    {
        const string Script = """
            class Base { constructor() {} }
            Base.prototype.value = 'from base';
            class Derived extends Base {
                read(key) { return super[key]; }
                write() {
                    let key = 'first';
                    super[key] = (() => (key = 'second', 42))();
                    return this.first + ' ' + this.second;
                }
            }

            var instance = new Derived();
            instance.read('value') + ' / ' + instance.write();
            """;

        new Engine().Evaluate(Script).AsString().Should().Be("from base / 42 undefined");
    }

    /// <summary>
    /// And it must survive suspension: <c>super[await key]</c> resumes into the same deferred base rather
    /// than into whatever evaluating the <c>super</c> keyword on its own would produce.
    /// </summary>
    [Test]
    public void ComputedSuperPropertySurvivesAnAwaitInThePropertyExpression()
    {
        const string Script = """
            class Base { constructor() {} }
            Base.prototype.value = 'from base';
            class Derived extends Base {
                async read() { return super[await Promise.resolve('value')]; }
            }

            var result = 'pending';
            new Derived().read().then(v => result = v);
            result;
            """;

        var engine = new Engine();
        engine.Evaluate(Script);
        engine.Evaluate("result").AsString().Should().Be("from base");
    }
}
