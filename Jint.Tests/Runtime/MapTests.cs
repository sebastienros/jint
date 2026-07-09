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
    public void KeysIteratorToleratesDeletionDuringIteration()
    {
        const string Script = @"
            var map = new Map([['a', 1], ['b', 2], ['c', 3]]);
            var count = 0;
            for (var key of map.keys()) { map.delete(key); count++; }
            return count + '/' + map.size;";

        Assert.Equal("3/0", new Engine().Evaluate(Script).AsString());
    }

    [Fact]
    public void ValuesIteratorToleratesDeletionDuringIteration()
    {
        const string Script = @"
            var map = new Map([['a', 1], ['b', 2], ['c', 3]]);
            var keys = ['a', 'b', 'c'];
            var count = 0;
            for (var value of map.values()) { map.delete(keys[count]); count++; }
            return count + '/' + map.size;";

        Assert.Equal("3/0", new Engine().Evaluate(Script).AsString());
    }

    [Fact]
    public void KeysIteratorSeesEntryAddedDuringIteration()
    {
        const string Script = @"
            var map = new Map([['a', 1]]);
            var seen = [];
            for (var key of map.keys()) { seen.push(key); if (key === 'a') map.set('b', 2); }
            return seen.join(',');";

        Assert.Equal("a,b", new Engine().Evaluate(Script).AsString());
    }

    [Fact]
    public void ValuesIteratorSkipsEntryDeletedAhead()
    {
        const string Script = @"
            var map = new Map([['a', 1], ['b', 2], ['c', 3]]);
            var seen = [];
            for (var value of map.values()) { seen.push(value); if (value === 1) map.delete('b'); }
            return seen.join(',');";

        Assert.Equal("1,3", new Engine().Evaluate(Script).AsString());
    }

    [Fact]
    public void KeysAndValuesIteratorsProduceExpectedSequence()
    {
        var engine = new Engine();
        engine.Execute("var map = new Map([['a', 1], ['b', 2], ['c', 3]]);");
        Assert.Equal("a,b,c", engine.Evaluate("[...map.keys()].join(',')").AsString());
        Assert.Equal("1,2,3", engine.Evaluate("[...map.values()].join(',')").AsString());
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
