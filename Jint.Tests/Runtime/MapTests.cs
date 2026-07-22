using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class MapTests
{
    [Fact]
    public void ShouldThrowWhenCalledWithoutNew()
    {
        var e = Invoking(() => new Engine().Execute("const m = new Map(); Map.call(m,[]);")).Should().ThrowExactly<JavaScriptException>().Which;
        e.Message.Should().Be("Constructor Map requires 'new'");
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

        new Engine().Evaluate(Script).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void KeysIteratorToleratesDeletionDuringIteration()
    {
        const string Script = @"
            var map = new Map([['a', 1], ['b', 2], ['c', 3]]);
            var count = 0;
            for (var key of map.keys()) { map.delete(key); count++; }
            return count + '/' + map.size;";

        new Engine().Evaluate(Script).AsString().Should().Be("3/0");
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

        new Engine().Evaluate(Script).AsString().Should().Be("3/0");
    }

    [Fact]
    public void KeysIteratorSeesEntryAddedDuringIteration()
    {
        const string Script = @"
            var map = new Map([['a', 1]]);
            var seen = [];
            for (var key of map.keys()) { seen.push(key); if (key === 'a') map.set('b', 2); }
            return seen.join(',');";

        new Engine().Evaluate(Script).AsString().Should().Be("a,b");
    }

    [Fact]
    public void ValuesIteratorSkipsEntryDeletedAhead()
    {
        const string Script = @"
            var map = new Map([['a', 1], ['b', 2], ['c', 3]]);
            var seen = [];
            for (var value of map.values()) { seen.push(value); if (value === 1) map.delete('b'); }
            return seen.join(',');";

        new Engine().Evaluate(Script).AsString().Should().Be("1,3");
    }

    [Fact]
    public void KeysAndValuesIteratorsProduceExpectedSequence()
    {
        var engine = new Engine();
        engine.Execute("var map = new Map([['a', 1], ['b', 2], ['c', 3]]);");
        engine.Evaluate("[...map.keys()].join(',')").AsString().Should().Be("a,b,c");
        engine.Evaluate("[...map.values()].join(',')").AsString().Should().Be("1,2,3");
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
        engine.Evaluate("proto2.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("!proto1.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("!iterator.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("iterator[Symbol.iterator]() === iterator").AsBoolean().Should().BeTrue();
    }
}
