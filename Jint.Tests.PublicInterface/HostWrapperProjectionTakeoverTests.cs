#nullable enable

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins that once the script has taken a projection over, a wrapped CLR object answers with the script's property,
/// or with none, and never re-derives the projection from the CLR side: a <c>length</c> the script redefined is what
/// reads and writes reach, and a projection deleted from a non-extensible wrapper stays deleted.
/// </summary>
/// <remarks>
/// <para>
/// The projections are what a wrapper makes itself and its target does not report: the <c>length</c> forwarder of a
/// collection, <c>Symbol.iterator</c> on an enumerable, <c>toJSON</c> on a type with a public <c>toJSON()</c>, and
/// <c>Symbol.dispose</c> on an <see cref="IDisposable"/>. Two lanes ignored a takeover. A read of <c>length</c> on an
/// <see cref="System.Collections.ICollection"/> answered <c>Count</c> before looking at the property the script had
/// stored, so <c>[[Get]]</c> disagreed with <c>[[GetOwnProperty]]</c>
/// (<see href="https://tc39.es/ecma262/#sec-ordinaryget">OrdinaryGet</see>). And a read after a <c>delete</c> made the
/// projection again, which a non-extensible object may not do: it must not gain a property
/// (<see href="https://tc39.es/ecma262/#sec-invariants-of-the-essential-internal-methods">the invariants of the
/// essential internal methods</see>).
/// </para>
/// <para>
/// The expected values are what Node gives for the analogous sequence on an ordinary object holding the projection
/// as an own property: <c>({ get length() { return 2; } })</c> redefined to <c>{ value: 7 }</c> reads 7, and
/// <c>Object.preventExtensions</c> then <c>delete</c> leaves no own property, so a read reaches the prototype.
/// </para>
/// </remarks>
public class HostWrapperProjectionTakeoverTests
{
    /// <summary>
    /// A <c>length</c> the script redefined is what every read reaches, whichever lane the read takes.
    /// </summary>
    [TestCase("List")]
    [TestCase("Array")]
    [TestCase("Queue")]
    [TestCase("ReadOnlyCollection")]
    public void AReadOfARedefinedLengthAgreesWithItsDescriptor(string shape)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            """
            Object.defineProperty(host, 'length', { value: 7 });
            const name = 'len' + 'gth';
            [host.length, host[name], Reflect.get(host, 'length'), Object.getOwnPropertyDescriptor(host, 'length').value].join()
            """).AsString().Should().Be("7,7,7,7");
    }

    /// <summary>
    /// An accessor the script puts in place of the forwarder is called on a read.
    /// </summary>
    [TestCase("List")]
    [TestCase("Array")]
    [TestCase("Queue")]
    public void ARedefinedLengthAccessorIsCalled(string shape)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            """
            let calls = 0;
            Object.defineProperty(host, 'length', { get() { calls++; return 42; }, configurable: true });
            [host.length, host.length, calls].join()
            """).AsString().Should().Be("42,42,2");
    }

    /// <summary>
    /// A frozen redefinition keeps reading what was frozen.
    /// </summary>
    [TestCase("List")]
    [TestCase("Array")]
    public void AFrozenRedefinedLengthReadsWhatWasFrozen(string shape)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            """
            Object.defineProperty(host, 'length', { value: 7, configurable: true, writable: true });
            Object.freeze(host);
            [host.length, Object.isFrozen(host)].join()
            """).AsString().Should().Be("7,true");
    }

    /// <summary>
    /// Freezing or sealing an untouched collection redefines its <c>length</c> forwarder without replacing it, so
    /// <c>length</c> goes on reading the live count.
    /// </summary>
    [TestCase("freeze")]
    [TestCase("seal")]
    public void AFrozenForwarderStillReadsTheLiveCount(string integrityLevel)
    {
        var list = new List<int> { 1, 2 };
        var engine = CreateEngine(list);

        engine.Execute($"Object.{integrityLevel}(host);");
        list.Add(3);

        engine.Evaluate("[host.length, Object.getOwnPropertyDescriptor(host, 'length').get.call(host)].join()")
            .AsString().Should().Be("3,3");
    }

    /// <summary>
    /// A <c>length</c> the script defines after deleting the forwarder of a collection that has no <c>Length</c>
    /// member is the script's own, and reads as such.
    /// </summary>
    [Test]
    public void ALengthDefinedAfterADeleteIsTheScripts()
    {
        var engine = CreateEngine(new List<int> { 1, 2 });

        engine.Evaluate(
            """
            delete host.length;
            Object.defineProperty(host, 'length', { value: 5, configurable: true });
            [host.length, Object.getOwnPropertyDescriptor(host, 'length').value].join()
            """).AsString().Should().Be("5,5");
    }

    /// <summary>
    /// Deleting a redefined <c>length</c> from an extensible wrapper gives the count back, as deleting the untouched
    /// forwarder does.
    /// </summary>
    [TestCase("List")]
    [TestCase("Queue")]
    public void DeletingARedefinedLengthReadsTheCountAgain(string shape)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            """
            Object.defineProperty(host, 'length', { value: 7, configurable: true });
            const redefined = host.length;
            [redefined, delete host.length, host.length].join()
            """).AsString().Should().Be("7,true,2");
    }

    /// <summary>
    /// A write to a <c>length</c> the script redefined as a writable data property writes that property, as on an
    /// ordinary object, and leaves the collection alone; only the forwarder resizes it.
    /// </summary>
    [Test]
    public void AWriteToARedefinedLengthWritesTheProperty()
    {
        var list = new List<int> { 1, 2, 3 };
        var engine = CreateEngine(list, allowWrite: true);

        engine.Evaluate(
            """
            Object.defineProperty(host, 'length', { value: 7, writable: true, configurable: true });
            host.length = 1;
            [host.length, Object.getOwnPropertyDescriptor(host, 'length').value, host[2]].join()
            """).AsString().Should().Be("1,1,3");

        list.Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// The forwarder itself still resizes the collection on a write, and reads the live count.
    /// </summary>
    [Test]
    public void TheUntouchedForwarderStillResizesAndReadsTheCount()
    {
        var list = new List<int> { 1, 2, 3 };
        var engine = CreateEngine(list, allowWrite: true);

        engine.Evaluate(
            """
            const before = host.length;
            Object.getOwnPropertyDescriptor(host, 'length');
            host.length = 1;
            [before, host.length].join()
            """).AsString().Should().Be("3,1");

        list.Should().Equal(1);
    }

    /// <summary>
    /// A projection deleted from a non-extensible wrapper stays deleted: the read that follows reaches the prototype,
    /// and no own property comes back.
    /// </summary>
    [TestCase("List", "Symbol.iterator")]
    [TestCase("List", "'length'")]
    [TestCase("Array", "Symbol.iterator")]
    [TestCase("Array", "'length'")]
    [TestCase("Queue", "'length'")]
    [TestCase("Queue", "Symbol.iterator")]
    [TestCase("Dictionary", "Symbol.iterator")]
    [TestCase("Disposable", "Symbol.dispose")]
    [TestCase("JsonList", "'toJSON'")]
    [TestCase("JsonDictionary", "'toJSON'")]
    [TestCase("JsonRecord", "'toJSON'")]
    public void AProjectionDeletedFromANonExtensibleWrapperStaysDeleted(string shape, string key)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const key = {{key}};
            Object.preventExtensions(host);
            const deleted = delete host[key];
            const read = host[key] === Object.getPrototypeOf(host)[key];
            [
              deleted,
              read,
              Object.getOwnPropertyDescriptor(host, key) === undefined,
              Object.prototype.hasOwnProperty.call(host, key),
              Reflect.ownKeys(host).includes(key),
              host[key] === Object.getPrototypeOf(host)[key],
            ].join()
            """).AsString().Should().Be("true,true,true,false,false,true");
    }

    /// <summary>
    /// A dictionary whose <c>Symbol.iterator</c> was deleted after it stopped being extensible is no longer iterable,
    /// as an ordinary object without one is not.
    /// </summary>
    [Test]
    public void ANonExtensibleDictionaryWithoutItsIteratorIsNotIterable()
    {
        var engine = CreateEngine(CreateShape("Dictionary"));

        engine.Evaluate(
            """
            Object.preventExtensions(host);
            delete host[Symbol.iterator];
            let outcome;
            try { [...host]; outcome = 'iterated'; }
            catch (e) { outcome = e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; }
            outcome
            """).AsString().Should().Be("TypeError");
    }

    /// <summary>
    /// A non-extensible view whose <c>Symbol.iterator</c> was deleted iterates and destructures through the one it
    /// inherits from <c>Array.prototype</c>, the same answer on every lane.
    /// </summary>
    [TestCase("List")]
    [TestCase("Array")]
    public void ANonExtensibleViewWithoutItsIteratorUsesTheInheritedOne(string shape)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            """
            Object.preventExtensions(host);
            delete host[Symbol.iterator];
            const [a, b] = host;
            [a, b, [...host].join(), host[Symbol.iterator] === Array.prototype[Symbol.iterator]].join(';')
            """).AsString().Should().Be("1;2;1,2;true");
    }

    /// <summary>
    /// On an extensible wrapper a deleted projection is made again on the next read, as before.
    /// </summary>
    [TestCase("List", "Symbol.iterator")]
    [TestCase("Array", "'length'")]
    [TestCase("JsonList", "'toJSON'")]
    public void AnExtensibleWrapperStillMakesADeletedProjectionAgain(string shape, string key)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const key = {{key}};
            const deleted = delete host[key];
            host[key];
            [deleted, Object.getOwnPropertyDescriptor(host, key) !== undefined].join()
            """).AsString().Should().Be("true,true");
    }

    private static Engine CreateEngine(object host, bool allowWrite = false)
    {
        var engine = new Engine(options =>
        {
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
            options.Interop.AllowWrite = allowWrite;
        });
        engine.SetValue("host", host);
        return engine;
    }

    private static object CreateShape(string kind) => kind switch
    {
        "List" => new List<int> { 1, 2 },
        "Array" => new[] { 1, 2 },
        "Queue" => new Queue<int>([1, 2]),
        "ReadOnlyCollection" => new System.Collections.ObjectModel.ReadOnlyCollection<int>([1, 2]),
        "Dictionary" => new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1 },
        "Disposable" => new DisposableRecord(),
        "JsonList" => new JsonList { 1, 2 },
        "JsonDictionary" => new JsonDictionary { ["a"] = 1 },
        "JsonRecord" => new JsonRecord(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown shape"),
    };

    /// <summary>
    /// A disposable host object, which its wrapper gives a <c>Symbol.dispose</c> projection.
    /// </summary>
    public sealed class DisposableRecord : IDisposable
    {
        public int Value { get; set; } = 1;

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// A plain host object with a public <c>toJSON</c> method, which its target reports by name.
    /// </summary>
    public sealed class JsonRecord
    {
        public int Value { get; set; } = 1;

        public string toJSON() => "clr";
    }

    /// <summary>
    /// A list with a public <c>toJSON</c> method, which its view projects.
    /// </summary>
    public sealed class JsonList : List<int>
    {
        public string toJSON() => "clr";
    }

    /// <summary>
    /// A dictionary with a public <c>toJSON</c> method, which its wrapper projects.
    /// </summary>
    public sealed class JsonDictionary : Dictionary<string, int>
    {
        public JsonDictionary() : base(StringComparer.Ordinal)
        {
        }

        public string toJSON() => "clr";
    }
}
