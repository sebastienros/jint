using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class SetTests
{
    [Fact]
    public void ShouldThrowWhenCalledWithoutNew()
    {
        var e = Invoking(() => new Engine().Execute("const m = new Set(); Set.call(m,[]);")).Should().ThrowExactly<JavaScriptException>().Which;
        e.Message.Should().Be("Constructor Set requires 'new'");
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

        new Engine().Evaluate(Script).AsBoolean().Should().BeTrue();
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
        engine.Evaluate("proto2.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("!proto1.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("!iterator.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("iterator[Symbol.iterator]() === iterator").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.issupersetof reads the receiver's size at step 4,
    /// <em>after</em> GetSetRecord at step 3. The order is observable, because GetSetRecord runs the
    /// set-like's own <c>size</c>, <c>has</c> and <c>keys</c> getters and those may add to the receiver:
    /// a receiver grown from one to two elements is a superset of a two-element set-like, and reading
    /// its size first answered false.
    /// </summary>
    [Fact]
    public void IsSupersetOfReadsTheReceiverSizeAfterBuildingTheSetRecord()
    {
        const string Script = @"
            var s = new Set([1]);
            var log = [];
            var setLike = {
              get size() { log.push('size'); s.add(2); return 2; },
              get has() { log.push('has'); return function () { throw new Error('unexpected has'); }; },
              get keys() { log.push('keys'); return function () { return [1, 2][Symbol.iterator](); }; }
            };
            return String(s.isSupersetOf(setLike)) + '|' + log.join(',') + '|' + [...s].join(',');";

        new Engine().Evaluate(Script).AsString().Should().Be("true|size,has,keys|1,2");
    }

    /// <summary>The sibling case, where the receiver really is too small, must still be false.</summary>
    [Fact]
    public void IsSupersetOfIsFalseWhenTheReceiverStaysSmaller()
    {
        const string Script = @"
            var s = new Set([1]);
            var setLike = {
              size: 2,
              has: function () { throw new Error('unexpected has'); },
              keys: function () { throw new Error('unexpected keys'); }
            };
            return s.isSupersetOf(setLike);";

        new Engine().Evaluate(Script).AsBoolean().Should().BeFalse();
    }
}
