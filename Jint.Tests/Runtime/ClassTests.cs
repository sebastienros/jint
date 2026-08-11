using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class ClassTests
{
    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
    [Theory]
    [InlineData("class C {}; [1].map(C);", "Class constructor C cannot be invoked without 'new'")]
    [InlineData("[1].map(class C {});", "Class constructor C cannot be invoked without 'new'")]
    [InlineData("[1].map(class {});", "Class constructors cannot be invoked without 'new'")]
    [InlineData("const D = class {}; [1].map(D);", "Class constructor D cannot be invoked without 'new'")]
    [InlineData("class E { constructor() {} }; E();", "Class constructor E cannot be invoked without 'new'")]
    [InlineData("class B {}; class F extends B {}; F();", "Class constructor F cannot be invoked without 'new'")]
    [InlineData("const o = {}; o.h = class {}; o.h();", "Class constructors cannot be invoked without 'new'")]
    [InlineData("const o = { g: class {} }; o.g();", "Class constructor g cannot be invoked without 'new'")]
    public void ClassConstructorCalledWithoutNewNamesTheClass(string script, string expected)
    {
        var ex = Invoking(() => new Engine().Evaluate(script))
            .Should().ThrowExactly<JavaScriptException>().Which;

        ex.Error.Get("message").AsString().Should().Be(expected);
    }
}
