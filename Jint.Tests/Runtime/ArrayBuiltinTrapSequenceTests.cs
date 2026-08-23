namespace Jint.Tests.Runtime;

/// <summary>
/// The exact sequence of internal-method calls the array built-ins are specified to perform, observed
/// through a Proxy, plus the two places Jint substituted a different operation for the one the algorithm
/// names: <c>Array.prototype.concat</c> defining <c>length</c> instead of setting it, and the array
/// iterator asking <c>[[HasProperty]]</c> before <c>[[Get]]</c>. A Proxy is the only way a script can see
/// the difference, which is why these live together.
/// </summary>
public class ArrayBuiltinTrapSequenceTests
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.concat step 6 is <c>Set(A, "length", n, true)</c>.
    /// Defining it instead bypasses the species result's own <c>[[Set]]</c>.
    /// </summary>
    [Fact]
    public void ConcatSetsTheResultLengthRatherThanDefiningIt()
    {
        var engine = new Engine();
        var log = engine.Evaluate("""
            var log = [];
            var target = { length: 0 };
            var p = new Proxy(target, {
                set(t, k, v) { log.push('set:' + String(k)); t[k] = v; return true; },
                defineProperty(t, k, d) { log.push('define:' + String(k)); return Reflect.defineProperty(t, k, d); }
            });
            var a = [1, 2];
            a.constructor = { [Symbol.species]: function () { return p; } };
            a.concat();
            log.join(',');
            """).AsString();

        log.Should().Be("define:0,define:1,set:length");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.slice step 12 is the same <c>Set</c>, and Jint
    /// performed no length write at all on the generic path.
    /// </summary>
    [Fact]
    public void SliceSetsTheResultLength()
    {
        var engine = new Engine();
        var log = engine.Evaluate("""
            var log = [];
            var target = { length: 0 };
            var p = new Proxy(target, {
                set(t, k, v) { log.push('set:' + String(k)); t[k] = v; return true; },
                defineProperty(t, k, d) { log.push('define:' + String(k)); return Reflect.defineProperty(t, k, d); }
            });
            var a = [1, 2];
            a.constructor = { [Symbol.species]: function () { return p; } };
            a.slice();
            log.join(',');
            """).AsString();

        log.Should().Be("define:0,define:1,set:length");
    }

    /// <summary>
    /// A <c>FakeArray</c> species whose <c>length</c> is an ordinary configurable data property: defining
    /// <c>length</c> with the array attributes (writable, non-configurable) on a Proxy over it violates the
    /// <c>[[DefineOwnProperty]]</c> invariant, so the previous code turned a legal concat into a TypeError.
    /// </summary>
    [Fact]
    public void ConcatIntoAProxyWithAConfigurableLengthDoesNotThrow()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function FakeArray(n) { this.length = n; }
            var a = [1, 2, 3];
            a.constructor = {
                [Symbol.species]: function (n) {
                    return new Proxy(new FakeArray(n), {
                        set() { return true; },
                        defineProperty() { return true; }
                    });
                }
            };
            var b = a.concat();
            b.constructor === FakeArray;
            """).AsBoolean();

        result.Should().BeTrue();
    }

    /// <summary>
    /// Step 5.b.iv of concat asks <c>HasProperty</c> per element and skips the <c>CreateDataPropertyOrThrow</c>
    /// for a hole. The question has to be asked afresh each time: the trap that defines element 0 here deletes
    /// element 1 of the source.
    /// </summary>
    [Fact]
    public void ConcatRechecksHasPropertyAfterAnEarlierElementIsDeleted()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var a = [1, 2, 3];
            a.constructor = {
                [Symbol.species]: function (...args) {
                    return new Proxy(new Array(...args), {
                        defineProperty(t, k, d) {
                            if (k === '0') delete a[1];
                            return Reflect.defineProperty(t, k, d);
                        }
                    });
                }
            };
            var p = a.concat();
            [0 in p, 1 in p, 2 in p].join(',');
            """).AsString();

        result.Should().Be("true,false,true");
    }

    /// <summary>
    /// A hole in the source is a hole in the result, and it still advances the result index.
    /// </summary>
    [Fact]
    public void ConcatPreservesHolesOnTheGenericPath()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var a = [1, , 3];
            a.constructor = { [Symbol.species]: Array };
            var b = a.concat();
            [b.length, 0 in b, 1 in b, 2 in b].join(',');
            """).AsString();

        result.Should().Be("3,true,false,true");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createarrayiterator steps 10.d.v-vi: a value step is a bare
    /// <c>Get</c>, and a key step reads no element at all.
    /// </summary>
    [Fact]
    public void ArrayIteratorPerformsOneGetPerElementAndNoHasProperty()
    {
        var engine = new Engine();
        var log = engine.Evaluate("""
            var log = [];
            var p = new Proxy([3, 4, 5], {
                has(t, k) { log.push('has:' + String(k)); return k in t; },
                get(t, k) { log.push('get:' + String(k)); return t[k]; }
            });
            Array.from(p);
            log.join(',');
            """).AsString();

        log.Should().Be(
            "get:Symbol(Symbol.iterator),get:length,get:0,get:length,get:1,get:length,get:2,get:length");
    }

    [Fact]
    public void ArrayIteratorKeyKindReadsNoElement()
    {
        var engine = new Engine();
        var log = engine.Evaluate("""
            var log = [];
            var p = new Proxy([3, 4, 5], {
                get(t, k) { log.push('get:' + String(k)); return t[k]; }
            });
            for (var k of Array.prototype.keys.call(p)) { }
            log.join(',');
            """).AsString();

        log.Should().Be("get:length,get:length,get:length,get:length");
    }

    /// <summary>
    /// A hole in an array-like resolves through the prototype chain, because the step is <c>Get</c> and not
    /// an own-property read.
    /// </summary>
    [Fact]
    public void ArrayLikeIteratorResolvesAHoleThroughThePrototypeChain()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var proto = { 1: 'inherited' };
            var arrayLike = Object.create(proto);
            arrayLike[0] = 'a';
            arrayLike[2] = 'c';
            arrayLike.length = 3;
            arrayLike[Symbol.iterator] = Array.prototype[Symbol.iterator];
            [...arrayLike].join(',');
            """).AsString();

        result.Should().Be("a,inherited,c");
    }
}
