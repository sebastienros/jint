using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class SetTests
{
    [Fact]
    public void ShouldThrowWhenCalledWithoutNew()
    {
        var e = Assert.Throws<JavaScriptException>(() => new Engine().Execute("const m = new Set(); Set.call(m,[]);"));
        Assert.Equal("Constructor Set requires 'new'", e.Message);
    }

    [Fact]
    public void NegativeZeroKeyConvertsToPositiveZero()
    {
        const string Script = @"
            var set = new Set();
            set.add(-0);
            var k;
            set.forEach(function (value) {
              k = 1 / value;
            });
            return k === Infinity && set.has(+0);";

        Assert.True(new Engine().Evaluate(Script).AsBoolean());
    }

    [Fact]
    public void HasProperIteratorPrototypeChain()
    {
        const string Script = @"
            // Iterator instance
            var iterator = new Set()[Symbol.iterator]();
            // %SetIteratorPrototype%
            var proto1 = Object.getPrototypeOf(iterator);
            // %IteratorPrototype%
            var proto2 = Object.getPrototypeOf(proto1);";

        var engine = new Engine();
        engine.Execute(Script);
        Assert.True(engine.Evaluate("proto2.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("!proto1.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("!iterator.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("iterator[Symbol.iterator]() === iterator").AsBoolean());
    }
}
