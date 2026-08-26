namespace Jint.Tests.Runtime;

/// <summary>
/// https://tc39.es/ecma262/#sec-ordinaryget - an inherited accessor's getter runs with the
/// ORIGINAL receiver as its <c>this</c>, not with the prototype object that happens to hold it.
/// </summary>
public class PrototypeAccessorReceiverTests
{
    private readonly Engine _engine = new();

    [Test]
    public void ArrayDestructuringPassesTheReceiverToAnInheritedLengthGetter()
    {
        // Array destructuring probes 'length' via IsArrayLike before choosing between the
        // array-like fast path and the iterator protocol. That probe walks the prototype
        // chain; running the class's `get length()` with the prototype as `this` made it
        // read an undefined backing field and throw instead of falling through to the
        // iterator.
        var result = _engine.Evaluate("""
            class List {
                constructor(items) { this._items = items; }
                get length() { return this._items.length; }
                [Symbol.iterator]() { return this._items[Symbol.iterator](); }
            }
            const [first, second] = new List(['a', 'b']);
            first + ',' + second;
            """);

        result.AsString().Should().Be("a,b");
    }

    [Test]
    public void ArrayDestructuringWorksWhenTheGetterIsInheritedFromAGrandparent()
    {
        var result = _engine.Evaluate("""
            class Base {
                get length() { return this._items.length; }
                [Symbol.iterator]() { return this._items[Symbol.iterator](); }
            }
            class Middle extends Base { }
            class Leaf extends Middle {
                constructor(items) { super(); this._items = items; }
            }
            const [a, b, c] = new Leaf([1, 2, 3]);
            [a, b, c].join(',');
            """);

        result.AsString().Should().Be("1,2,3");
    }

    [Test]
    public void ArrayPrototypeKeysPassesTheReceiverToAnInheritedLengthGetter()
    {
        // Array.prototype.keys/values used to gate on the same IsArrayLike probe; the probe moved into
        // the iterator's per-next LengthOfArrayLike (see ArrayIteratorReceiverTests), so the getter now
        // runs from next() instead of from keys() — but it still has to run with the instance as `this`.
        var result = _engine.Evaluate("""
            class List {
                constructor(items) { this._items = items; }
                get length() { return this._items.length; }
            }
            Array.from(Array.prototype.keys.call(new List(['x', 'y', 'z']))).join(',');
            """);

        result.AsString().Should().Be("0,1,2");
    }

    [Test]
    public void InheritedGetterOnAnObjectLiteralPrototypeSeesTheReceiver()
    {
        var result = _engine.Evaluate("""
            const proto = {
                get length() { return this._items.length; },
                [Symbol.iterator]() { return this._items[Symbol.iterator](); }
            };
            const instance = Object.create(proto);
            instance._items = ['p', 'q'];
            const [first, second] = instance;
            first + ',' + second;
            """);

        result.AsString().Should().Be("p,q");
    }

    [Test]
    public void OwnAccessorStillReceivesTheObjectItself()
    {
        // The receiver threading must not disturb the ordinary own-accessor case.
        var result = _engine.Evaluate("""
            const o = {
                _items: ['m', 'n'],
                get length() { return this._items.length; },
                [Symbol.iterator]() { return this._items[Symbol.iterator](); }
            };
            const [first, second] = o;
            first + ',' + second + ',' + o.length;
            """);

        result.AsString().Should().Be("m,n,2");
    }
}
