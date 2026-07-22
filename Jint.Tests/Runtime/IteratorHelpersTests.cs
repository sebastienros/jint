namespace Jint.Tests.Runtime;

public class IteratorHelpersTests
{
    [Fact]
    public void ToArrayCollectsAllValues()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() { yield 1; yield 2; yield 3; }
            JSON.stringify([
                [1, 2, 3].values().toArray(),
                gen().toArray(),
                new Set(['a', 'b']).values().toArray()
            ]);
            """).AsString();

        result.Should().Be("[[1,2,3],[1,2,3],[\"a\",\"b\"]]");
    }

    [Fact]
    public void ToArrayWorksThroughHelperChain()
    {
        var engine = new Engine();
        var result = engine.Evaluate("JSON.stringify([1, 2, 3, 4, 5].values().drop(1).take(3).map(x => x * 10).toArray())").AsString();

        result.Should().Be("[20,30,40]");
    }

    [Fact]
    public void ToArrayReturnsPlainArray()
    {
        var engine = new Engine();
        var result = engine.Evaluate("Array.isArray([].values().toArray()) && [].values().toArray().length === 0").AsBoolean();

        result.Should().BeTrue();
    }
}
