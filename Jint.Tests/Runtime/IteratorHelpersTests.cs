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

    [Fact]
    public void JoinConcatenatesUsingSeparator()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() { yield 'a'; yield 'b'; }
            JSON.stringify([
                [].values().join(),
                ['one'].values().join(),
                ['one', 'two', 'three'].values().join(),
                ['one', 'two', 'three'].values().join('&&'),
                ['one', 'two', 'three'].values().join(''),
                gen().join('-'),
                [1, 2, 3, 4, 5].values().drop(1).take(3).map(x => x * 10).join('/')
            ]);
            """).AsString();

        result.Should().Be("""["","one","one,two,three","one&&two&&three","onetwothree","a-b","20/30/40"]""");
    }

    [Fact]
    public void JoinFormatsNullishValuesAsEmptyString()
    {
        var engine = new Engine();
        var result = engine.Evaluate("['one', null, 'two', undefined, 'three'].values().join()").AsString();

        result.Should().Be("one,,two,,three");
    }

    [Fact]
    public void JoinCoercesSeparatorBeforeReadingNext()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const effects = [];
            const separator = { toString() { effects.push('toString'); return '&&'; } };
            let n = 0;
            const it = {
                get next() {
                    effects.push('get next');
                    return () => ++n <= 2 ? { done: false, value: n === 1 ? 'one' : 'two' } : { done: true };
                }
            };
            Iterator.prototype.join.call(it, separator) + '|' + effects.join(',');
            """).AsString();

        result.Should().Be("one&&two|toString,get next");
    }

    [Fact]
    public void JoinClosesIteratorWhenCoercionThrows()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const throwy = { toString() { throw new Error('nope'); } };
            function makeIterator(value) {
                return {
                    closed: false,
                    next() { return this.done ? { done: true } : (this.done = true, { done: false, value }); },
                    return() { this.closed = true; }
                };
            }

            const onSeparator = makeIterator('x');
            try { Iterator.prototype.join.call(onSeparator, throwy); } catch { }

            const onContents = makeIterator(throwy);
            try { Iterator.prototype.join.call(onContents); } catch { }

            // an iterator that simply runs out must NOT be closed
            const onExhaustion = makeIterator('x');
            Iterator.prototype.join.call(onExhaustion);

            JSON.stringify([onSeparator.closed, onContents.closed, onExhaustion.closed]);
            """).AsString();

        result.Should().Be("[true,true,false]");
    }

    [Fact]
    public void JoinThrowsOnNonObjectReceiver()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            [undefined, null, false, 0, 0n, '', Symbol()].every(receiver => {
                try {
                    Iterator.prototype.join.call(receiver);
                    return false;
                } catch (e) {
                    return e instanceof TypeError;
                }
            });
            """).AsBoolean();

        result.Should().BeTrue();
    }
}
