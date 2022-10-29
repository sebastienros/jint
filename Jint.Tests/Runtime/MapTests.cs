using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class MapTests
{
    [Fact]
    public void ShouldThrowWhenCalledWithoutNew()
    {
        var e = Assert.Throws<JavaScriptException>(() => new Engine().Execute("const m = new Map(); Map.call(m,[]);"));
        Assert.Equal("Constructor Map requires 'new'", e.Message);
    }

    [Fact]
    public void NegativeZeroKeyConvertsToPositiveZero()
    {
        const string Script = @"
            var map = new Map();
            map.set(-0, ""foo"");
            var k;
            map.forEach(function (value, key) {
              k = 1 / key;
            });
            return k === Infinity && map.get(+0) === ""foo"";";

        Assert.True(new Engine().Evaluate(Script).AsBoolean());
    }

    [Fact]
    public void HasProperIteratorPrototypeChain()
    {
        const string Script = @"
        // Iterator instance
        var iterator = new Map()[Symbol.iterator]();
        // %MapIteratorPrototype%
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
