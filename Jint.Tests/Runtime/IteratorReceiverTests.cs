namespace Jint.Tests.Runtime;

/// <summary>
/// GetIterator (https://tc39.es/ecma262/#sec-getiterator) resolves @@iterator with GetMethod, which
/// is GetV: the lookup goes through ToObject, but the [[Get]] receiver -- and the this value
/// GetIteratorFromMethod (https://tc39.es/ecma262/#sec-getiteratorfrommethod) then calls it with --
/// is the original value. A primitive therefore reaches a strict-mode @@iterator as a primitive,
/// and only a sloppy-mode one sees the wrapper (because its own this-binding boxes it).
/// </summary>
public class IteratorReceiverTests
{
    [Fact]
    public void EveryConsumerCallsAPrimitivesIteratorMethodWithThePrimitive()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const seen = [];
            Object.defineProperty(Number.prototype, Symbol.iterator, {
                configurable: true,
                value() { 'use strict'; seen.push(typeof this + ':' + this); return [].values(); }
            });

            new Map(1);
            new Set(2);
            Array.from(3);
            const spread = [...4];
            for (const x of 5) { }
            const [first] = 6;

            JSON.stringify(seen);
            """).AsString();

        result.Should().Be("""["number:1","number:2","number:3","number:4","number:5","number:6"]""");
    }

    [Fact]
    public void ASloppyIteratorMethodStillSeesTheBoxedReceiver()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            let seen;
            Object.defineProperty(Number.prototype, Symbol.iterator, {
                configurable: true,
                value: function () { seen = typeof this; return [].values(); }
            });
            [...7];
            seen;
            """).AsString();

        result.Should().Be("object");
    }

    [Fact]
    public void AnIteratorAccessorAlsoReceivesThePrimitive()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            let seen;
            Object.defineProperty(Boolean.prototype, Symbol.iterator, {
                configurable: true,
                get() { 'use strict'; seen = typeof this + ':' + this; return () => [].values(); }
            });
            [...true];
            seen;
            """).AsString();

        result.Should().Be("boolean:true");
    }

    [Fact]
    public void AStringReachesAReplacedIteratorMethodAsAPrimitive()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            let seen;
            Object.defineProperty(String.prototype, Symbol.iterator, {
                configurable: true,
                value() { 'use strict'; seen = typeof this + ':' + this; return [].values(); }
            });
            [...'ab'];
            seen;
            """).AsString();

        result.Should().Be("string:ab");
    }
}
