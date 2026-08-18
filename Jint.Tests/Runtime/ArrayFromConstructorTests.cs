using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Array.from</c> / <c>Array.fromAsync</c> branch on the spec's IsConstructor
/// (https://tc39.es/ecma262/#sec-isconstructor), which is a question about the object's [[Construct]] slot and
/// not about which CLR interface its implementing type happens to declare. Arrow functions, generators, async
/// functions, concise methods and a bound function whose target is not constructable are all callable objects
/// with no [[Construct]], so §23.1.2.1 steps 5.a / 6.b make each of them fall back to ArrayCreate.
/// <para>
/// The other half of the same algorithm is the trailing <c>Perform ? Set(A, "length", len, true)</c> (steps
/// 5.b.iii.7 and 6.g): <c>%TypedArray%.prototype.length</c> is a getter-only accessor, so a typed array as the
/// receiver has to make that write throw a TypeError rather than silently do nothing.
/// </para>
/// </summary>
public class ArrayFromConstructorTests
{
    private const string ArrowFunction = "(() => ({}))";
    private const string GeneratorFunction = "(function* () {})";
    private const string AsyncFunction = "(async function () {})";
    private const string ConciseMethod = "({ m() {} }).m";
    private const string BoundNonConstructor = "Math.sin.bind(null)";

    [Theory]
    [InlineData(ArrowFunction)]
    [InlineData(GeneratorFunction)]
    [InlineData(AsyncFunction)]
    [InlineData(ConciseMethod)]
    [InlineData(BoundNonConstructor)]
    public void ArrayFromFallsBackToArrayCreateForANonConstructorThisOnTheIteratorPath(string nonConstructor)
    {
        var engine = new Engine();

        var result = engine.Evaluate($$"""
            var out = Array.from.call({{nonConstructor}}, [1, 2]);
            JSON.stringify([Array.isArray(out), out.length, out[0], out[1]]);
            """).AsString();

        result.Should().Be("[true,2,1,2]");
    }

    [Theory]
    [InlineData(ArrowFunction)]
    [InlineData(GeneratorFunction)]
    [InlineData(AsyncFunction)]
    [InlineData(ConciseMethod)]
    [InlineData(BoundNonConstructor)]
    public void ArrayFromFallsBackToArrayCreateForANonConstructorThisOnTheArrayLikePath(string nonConstructor)
    {
        var engine = new Engine();

        // No @@iterator, so this is the ConstructArrayFromArrayLike branch (step 6) rather than step 5.
        var result = engine.Evaluate($$"""
            var out = Array.from.call({{nonConstructor}}, { length: 2, 0: 1, 1: 2 });
            JSON.stringify([Array.isArray(out), out.length, out[0], out[1]]);
            """).AsString();

        result.Should().Be("[true,2,1,2]");
    }

    [Theory]
    [InlineData(ArrowFunction)]
    [InlineData(GeneratorFunction)]
    [InlineData(AsyncFunction)]
    [InlineData(ConciseMethod)]
    [InlineData(BoundNonConstructor)]
    public void ArrayFromAsyncFallsBackToArrayCreateForANonConstructorThis(string nonConstructor)
    {
        var engine = new Engine();

        var result = engine.Evaluate($$"""
            Array.fromAsync.call({{nonConstructor}}, [1, 2]).then(function (out) {
                return JSON.stringify([Array.isArray(out), out.length, out[0], out[1]]);
            });
            """).UnwrapIfPromise(TimeSpan.FromSeconds(5)).AsString();

        result.Should().Be("[true,2,1,2]");
    }

    [Theory]
    [InlineData(ArrowFunction)]
    [InlineData(GeneratorFunction)]
    [InlineData(AsyncFunction)]
    [InlineData(ConciseMethod)]
    [InlineData(BoundNonConstructor)]
    public void ArrayFromAsyncFallsBackToArrayCreateForANonConstructorThisOnTheArrayLikePath(string nonConstructor)
    {
        var engine = new Engine();

        var result = engine.Evaluate($$"""
            Array.fromAsync.call({{nonConstructor}}, { length: 2, 0: 1, 1: 2 }).then(function (out) {
                return JSON.stringify([Array.isArray(out), out.length, out[0], out[1]]);
            });
            """).UnwrapIfPromise(TimeSpan.FromSeconds(5)).AsString();

        result.Should().Be("[true,2,1,2]");
    }

    [Fact]
    public void ArrayOfFallsBackToArrayCreateForANonConstructorThis()
    {
        // Array.of already asked the right question; pinned here so the two stay in step.
        var engine = new Engine();

        var result = engine.Evaluate($$"""
            var out = Array.of.call({{BoundNonConstructor}}, 1, 2, 3);
            JSON.stringify([Array.isArray(out), out.length, out[0], out[2]]);
            """).AsString();

        result.Should().Be("[true,3,1,3]");
    }

    [Fact]
    public void ABoundNonConstructorIsStillCallableAndStillNotConstructable()
    {
        var engine = new Engine();

        engine.Evaluate($"typeof ({BoundNonConstructor})(0)").AsString().Should().Be("number");
        engine.Evaluate($$"""
            try { new ({{BoundNonConstructor}}); 'no throw'; } catch (e) { e.constructor.name; }
            """).AsString().Should().Be("TypeError");
    }

    [Fact]
    public void ArrayFromStillConstructsAGenuineConstructorThis()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            function C() { this.constructed = true; }
            var out = Array.from.call(C, [1, 2]);
            JSON.stringify([Array.isArray(out), out instanceof C, out.constructed, out.length, out[0], out[1]]);
            """).AsString();

        result.Should().Be("[false,true,true,2,1,2]");
    }

    [Fact]
    public void ArrayFromPassesTheLengthToAGenuineConstructorOnTheArrayLikePath()
    {
        var engine = new Engine();

        // Step 6.b: Construct(C, « 𝔽(len) »).
        var result = engine.Evaluate("""
            function C(n) { this.argCount = arguments.length; this.arg = n; }
            var out = Array.from.call(C, { length: 2, 0: 'a', 1: 'b' });
            JSON.stringify([out instanceof C, out.argCount, out.arg, out.length, out[0], out[1]]);
            """).AsString();

        result.Should().Be("[true,1,2,2,\"a\",\"b\"]");
    }

    [Fact]
    public void ArrayFromConstructsAClassAndABoundConstructor()
    {
        var engine = new Engine();

        engine.Evaluate("""
            class C {}
            var out = Array.from.call(C, [1]);
            JSON.stringify([out instanceof C, out.length, out[0]]);
            """).AsString().Should().Be("[true,1,1]");

        engine.Evaluate("""
            function D() {}
            var Bound = D.bind(null);
            var out = Array.from.call(Bound, [1]);
            JSON.stringify([out instanceof D, out.length, out[0]]);
            """).AsString().Should().Be("[true,1,1]");
    }

    [Fact]
    public void TypedArrayFromIsUnaffected()
    {
        var engine = new Engine();

        // %TypedArray%.from is its own algorithm and never performs the Set(A, "length", ...).
        engine.Evaluate("""
            var out = Uint8Array.from([]);
            JSON.stringify([out instanceof Uint8Array, out.length]);
            """).AsString().Should().Be("[true,0]");

        engine.Evaluate("""
            var out = Uint8Array.from([1, 2, 3]);
            JSON.stringify([out instanceof Uint8Array, out.length, out[0], out[2]]);
            """).AsString().Should().Be("[true,3,1,3]");
    }

    [Fact]
    public void ArrayFromWithATypedArrayConstructorThrowsOnTheUnwritableLength()
    {
        var engine = new Engine();

        // Iterator path (step 5.b.iii.7).
        engine.Evaluate("""
            try { Array.from.call(Uint8Array, []); 'no throw'; } catch (e) { e.constructor.name; }
            """).AsString().Should().Be("TypeError");

        // Array-like path (step 6.g).
        engine.Evaluate("""
            try { Array.from.call(Uint8Array, { length: 0 }); 'no throw'; } catch (e) { e.constructor.name; }
            """).AsString().Should().Be("TypeError");

        // The exact shape staging/sm/Array/from_errors.js uses.
        engine.Evaluate("""
            Uint8Array.from = Array.from;
            try { Uint8Array.from([]); 'no throw'; } catch (e) { e.constructor.name; }
            """).AsString().Should().Be("TypeError");
    }

    [Fact]
    public void ArrayFromThrowsWhenTheConstructedObjectHasAnUnwritableLength()
    {
        var engine = new Engine();

        // Same Set(A, "length", ...) obligation reached through an ordinary object; the typed-array lane
        // above is only a second way to own a length that refuses to be written.
        engine.Evaluate("""
            function C() { Object.defineProperty(this, 'length', { configurable: true, writable: false, value: 4 }); }
            try { Array.from.call(C, []); 'no throw'; } catch (e) { e.constructor.name; }
            """).AsString().Should().Be("TypeError");

        engine.Evaluate("""
            function C() { Object.defineProperty(this, 'length', { configurable: true, get: function () { return 4; } }); }
            try { Array.from.call(C, [0, 10]); 'no throw'; } catch (e) { e.constructor.name; }
            """).AsString().Should().Be("TypeError");
    }

    [Fact]
    public void ArrayPrototypeGenericsThrowOnATypedArraysUnwritableLength()
    {
        var engine = new Engine();

        // Every one of these is specified to Set(O, "length", ...) with throw = true on the receiver.
        foreach (var script in new[]
                 {
                     "Array.prototype.push.call(new Uint8Array(3))",
                     "Array.prototype.pop.call(new Uint8Array(0))",
                     "Array.prototype.shift.call(new Uint8Array(0))",
                     "Array.prototype.unshift.call(new Uint8Array(3))",
                     "Array.prototype.splice.call(new Uint8Array(3), 0, 0)",
                 })
        {
            engine.Evaluate($$"""
                try { {{script}}; 'no throw'; } catch (e) { e.constructor.name; }
                """).AsString().Should().Be("TypeError", because: script);
        }
    }

    [Fact]
    public void FilterDoesNotWriteALengthTheSpecNeverWrites()
    {
        var engine = new Engine();

        // Array.prototype.filter has no Set(A, "length", ...) step at all, so a typed-array species must come
        // back untouched rather than tripping over the length accessor the trailing bookkeeping write used to
        // aim at.
        var result = engine.Evaluate("""
            var a = [1, 2, 3];
            a.constructor = { [Symbol.species]: Uint8Array };
            var out = a.filter(function () { return false; });
            JSON.stringify([out instanceof Uint8Array, out.length]);
            """).AsString();

        result.Should().Be("[true,0]");
    }

    [Fact]
    public void FilterStillFixesUpTheLengthOfARealArrayResult()
    {
        var engine = new Engine();

        engine.Evaluate("""
            var a = [1, 2, 3, 4];
            a.constructor = Array;
            var out = a.filter(function (x) { return x % 2 === 0; });
            JSON.stringify([Array.isArray(out), out.length, out[0], out[1]]);
            """).AsString().Should().Be("[true,2,2,4]");

        engine.Evaluate("""
            class MyArray extends Array {}
            var a = new MyArray();
            a.push(1, 2, 3, 4, 5);
            var out = a.filter(function (x) { return x > 3; });
            JSON.stringify([out instanceof MyArray, out.length, out[0], out[1]]);
            """).AsString().Should().Be("[true,2,4,5]");
    }
}
