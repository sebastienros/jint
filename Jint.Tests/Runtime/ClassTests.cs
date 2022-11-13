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
        Assert.True(engine.Evaluate(Script).AsBoolean());
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
        Assert.Equal(10, engine.Evaluate("board.width"));
        Assert.Equal(20, engine.Evaluate("board.doubleWidth "));
    }
}
