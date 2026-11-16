#nullable enable

using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins that a wrapped CLR object lists a <em>projection</em> (a property the wrapper makes itself and its target does
/// not report) once the script has redefined it or the wrapper has stopped being extensible, so that
/// <c>Reflect.ownKeys</c>, <c>Object.freeze</c> / <c>seal</c> and <c>Object.isFrozen</c> / <c>isSealed</c> all see it
/// (#4528).
/// </summary>
/// <remarks>
/// <para>
/// The projections are the <c>length</c> forwarder of a collection view, <c>Symbol.iterator</c> on an enumerable,
/// <c>toJSON</c> on a type with a public <c>toJSON()</c>, and <c>Symbol.dispose</c> on an <see cref="IDisposable"/>.
/// They used to be unlisted in every state, which breaks two invariants of
/// <see href="https://tc39.es/ecma262/#sec-invariants-of-the-essential-internal-methods">the essential internal
/// methods</see>: <c>[[OwnPropertyKeys]]</c> must list every non-configurable own property that has been observed, and
/// on a non-extensible object it must list exactly the own properties <c>[[GetOwnProperty]]</c> answers.
/// <see href="https://tc39.es/ecma262/#sec-setintegritylevel">SetIntegrityLevel</see> and
/// <see href="https://tc39.es/ecma262/#sec-testintegritylevel">TestIntegrityLevel</see> read that list, so freeze
/// skipped a projection and isFrozen answered for it without looking.
/// </para>
/// <para>
/// The expected values are what Node gives for the same script on an ordinary array or object that holds the
/// projection as an own property. An untouched projection of an extensible wrapper stays unlisted, as before.
/// </para>
/// </remarks>
public class HostWrapperProjectionKeysTests
{
    /// <summary>
    /// The issue's first row: a non-configurable <c>Symbol.iterator</c> on a list must be listed.
    /// </summary>
    [Test]
    public void TheIssuesNonConfigurableIteratorIsListed()
    {
        var engine = CreateEngine(new List<int> { 1, 2 });

        engine.Evaluate(
            """
            Object.defineProperty(host, Symbol.iterator, { value: function* () {}, configurable: false });
            Reflect.ownKeys(host).map(String).join()
            """).AsString().Should().Be("0,1,Symbol(Symbol.iterator)");
    }

    /// <summary>
    /// Every projection a script redefines as non-configurable is listed exactly once, by the listing of its own kind.
    /// </summary>
    [TestCase("List", "Symbol.iterator")]
    [TestCase("List", "'length'")]
    [TestCase("Array", "Symbol.iterator")]
    [TestCase("Array", "'length'")]
    [TestCase("Dictionary", "Symbol.iterator")]
    [TestCase("Disposable", "Symbol.dispose")]
    [TestCase("MemoryStream", "Symbol.dispose")]
    [TestCase("JsonList", "'toJSON'")]
    [TestCase("JsonDictionary", "'toJSON'")]
    public void ANonConfigurableRedefinitionIsListed(string shape, string key)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const key = {{key}};
            const before = Reflect.ownKeys(host).includes(key);
            Object.defineProperty(host, key, { configurable: false });
            const ofItsKind = typeof key === 'symbol' ? Object.getOwnPropertySymbols(host) : Object.getOwnPropertyNames(host);
            [
              before,
              Object.getOwnPropertyDescriptor(host, key).configurable,
              Reflect.ownKeys(host).filter(k => k === key).length,
              ofItsKind.includes(key),
            ].join()
            """).AsString().Should().Be("false,false,1,true");
    }

    /// <summary>
    /// The issue's third row: a projection redefined configurably is listed, and <c>Object.freeze</c> reaches it.
    /// </summary>
    [TestCase("List", "Symbol.iterator", "function* () { yield 9; }")]
    [TestCase("List", "'length'", "7")]
    [TestCase("Array", "Symbol.iterator", "function* () { yield 9; }")]
    [TestCase("Dictionary", "Symbol.iterator", "function* () { yield 9; }")]
    [TestCase("Disposable", "Symbol.dispose", "function () { }")]
    [TestCase("MemoryStream", "Symbol.dispose", "function () { }")]
    [TestCase("JsonList", "'toJSON'", "function () { return 'script'; }")]
    [TestCase("JsonDictionary", "'toJSON'", "function () { return 'script'; }")]
    public void AConfigurableRedefinitionIsListedAndFrozen(string shape, string key, string replacement)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const key = {{key}};
            const replacement = {{replacement}};
            Object.defineProperty(host, key, { value: replacement, configurable: true, writable: true });
            const listed = Reflect.ownKeys(host).includes(key);
            Object.freeze(host);
            const descriptor = Object.getOwnPropertyDescriptor(host, key);
            [listed, Object.isFrozen(host), descriptor.configurable, descriptor.writable, delete host[key], descriptor.value === replacement].join()
            """).AsString().Should().Be("true,true,false,false,false,true");
    }

    /// <summary>
    /// The issue's fourth row and its siblings: a projection the script never touched is frozen with the wrapper,
    /// because a non-extensible wrapper lists every own property it has.
    /// </summary>
    [TestCase("List", "Symbol.iterator")]
    [TestCase("List", "'length'")]
    [TestCase("Array", "'length'")]
    [TestCase("Dictionary", "Symbol.iterator")]
    [TestCase("Disposable", "Symbol.dispose")]
    [TestCase("MemoryStream", "Symbol.dispose")]
    [TestCase("JsonList", "'toJSON'")]
    [TestCase("JsonDictionary", "'toJSON'")]
    public void FreezingReachesAProjectionTheScriptNeverTouched(string shape, string key)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const key = {{key}};
            Object.freeze(host);
            const descriptor = Object.getOwnPropertyDescriptor(host, key);
            [Object.isFrozen(host), Reflect.ownKeys(host).includes(key), descriptor.configurable, descriptor.writable === true, delete host[key], key in host].join()
            """).AsString().Should().Be("true,true,false,false,false,true");

        engine.Evaluate(Strict($"delete host[{key}]; return 'no-throw';")).AsString()
            .Should().Be("TypeError", "a strict-mode delete of a frozen projection throws");
    }

    /// <summary>
    /// <c>Object.seal</c> reaches an untouched projection too, and leaves a data projection writable.
    /// </summary>
    [TestCase("List", "Symbol.iterator", "true")]
    [TestCase("List", "'length'", "accessor")]
    [TestCase("Disposable", "Symbol.dispose", "true")]
    [TestCase("JsonList", "'toJSON'", "true")]
    public void SealingReachesAProjectionTheScriptNeverTouched(string shape, string key, string writable)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const key = {{key}};
            Object.seal(host);
            const descriptor = Object.getOwnPropertyDescriptor(host, key);
            [Object.isSealed(host), Reflect.ownKeys(host).includes(key), descriptor.configurable, 'writable' in descriptor ? descriptor.writable : 'accessor'].join()
            """).AsString().Should().Be("true,true,false," + writable);
    }

    /// <summary>
    /// <c>Object.preventExtensions</c> alone makes a wrapper list its projections, in the order an array lists
    /// <c>length</c> and an own <c>Symbol.iterator</c>. They are still configurable, so the wrapper is neither sealed
    /// nor frozen, which <c>isSealed</c> and <c>isFrozen</c> used to answer without having looked.
    /// </summary>
    [Test]
    public void ANonExtensibleWrapperListsItsProjections()
    {
        var engine = CreateEngine(new List<int> { 1, 2 });

        engine.Evaluate(
            """
            const before = Reflect.ownKeys(host).map(String).join();
            Object.preventExtensions(host);
            [before, Reflect.ownKeys(host).map(String).join(), Object.getOwnPropertyNames(host).join(), Object.isSealed(host), Object.isFrozen(host)].join(';')
            """).AsString().Should().Be("0,1;0,1,length,Symbol(Symbol.iterator);0,1,length;false;false");
    }

    /// <summary>
    /// A projection stays out of every enumeration that filters on enumerability, frozen or not: each one is
    /// non-enumerable.
    /// </summary>
    [Test]
    public void AListedProjectionIsNotEnumerable()
    {
        var engine = CreateEngine(new JsonList { 1, 2 });

        engine.Evaluate(
            """
            Object.freeze(host);
            const seen = [];
            for (const k in host) seen.push(k);
            [Object.keys(host).join(), seen.join(), Object.keys(Object.assign({}, host)).join(), JSON.stringify(host)].join(';')
            """).AsString().Should().Be("0,1;0,1;0,1;\"clr\"");
    }

    /// <summary>
    /// Projections come after the target's keys and ahead of the script's own, whichever was read first, as an
    /// array's <c>length</c> comes ahead of the properties a script adds.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void ProjectionsAreListedAheadOfTheScriptsKeys(bool readTheProjectionsFirst)
    {
        var engine = CreateEngine(new List<int> { 1, 2 });
        if (readTheProjectionsFirst)
        {
            engine.Execute("Object.getOwnPropertyDescriptor(host, 'length'); host[Symbol.iterator];");
        }

        engine.Evaluate(
            """
            Object.defineProperty(host, 'tag', { value: 1, configurable: true });
            Object.defineProperty(host, Symbol.for('s'), { value: 2, configurable: true });
            Object.freeze(host);
            Reflect.ownKeys(host).map(String).join()
            """).AsString().Should().Be("0,1,length,tag,Symbol(Symbol.iterator),Symbol(s)");
    }

    /// <summary>
    /// What an untouched, extensible wrapper lists is unchanged: reading, calling and serializing a projection adds
    /// no key.
    /// </summary>
    [TestCase("List", "Symbol.iterator")]
    [TestCase("List", "'length'")]
    [TestCase("Dictionary", "Symbol.iterator")]
    [TestCase("Disposable", "Symbol.dispose")]
    [TestCase("MemoryStream", "Symbol.dispose")]
    [TestCase("JsonList", "'toJSON'")]
    public void ReadingAProjectionAddsNoKey(string shape, string key)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const key = {{key}};
            const before = Reflect.ownKeys(host).map(String).join();
            host[key]; Object.getOwnPropertyDescriptor(host, key); key in host; Object.prototype.hasOwnProperty.call(host, key);
            if (typeof host.toJSON === 'function') { host.toJSON(); }
            if (typeof host[Symbol.iterator] === 'function') { [...host]; }
            [before === Reflect.ownKeys(host).map(String).join(), Reflect.ownKeys(host).includes(key)].join()
            """).AsString().Should().Be("true,false");
    }

    /// <summary>
    /// A listed projection the script deletes leaves the listing with its descriptor; a read that makes it again
    /// makes the wrapper's untouched projection, which is not listed.
    /// </summary>
    [Test]
    public void ADeletedRedefinitionLeavesTheListing()
    {
        var engine = CreateEngine(new List<int> { 1, 2 });

        engine.Evaluate(
            """
            Object.defineProperty(host, Symbol.iterator, { value: function* () { yield 9; }, configurable: true });
            const listed = Object.getOwnPropertySymbols(host).length;
            const deleted = delete host[Symbol.iterator];
            [listed, deleted, Object.getOwnPropertySymbols(host).length, [...host].join(), Object.getOwnPropertySymbols(host).length].join()
            """).AsString().Should().Be("1,true,0,1,2,0");
    }

    /// <summary>
    /// A data projection redefined as an accessor is listed, and is what the wrapper answers from then on.
    /// </summary>
    [Test]
    public void AProjectionRedefinedAsAnAccessorIsListed()
    {
        var engine = CreateEngine(new List<int> { 1, 2 });

        engine.Evaluate(
            """
            Object.defineProperty(host, Symbol.iterator, { get() { return function* () { yield 5; }; }, configurable: false });
            [Object.getOwnPropertySymbols(host).map(String).join(), [...host].join()].join(';')
            """).AsString().Should().Be("Symbol(Symbol.iterator);5");
    }

    /// <summary>
    /// A frozen view still iterates and destructures through its own, now frozen, <c>Symbol.iterator</c>.
    /// </summary>
    [TestCase("List")]
    [TestCase("Array")]
    public void AFrozenViewStillIterates(string shape)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            """
            Object.freeze(host);
            const [a, b] = host;
            const seen = [];
            for (const x of host) seen.push(x);
            [a, b, [...host].join(), seen.join(), host.length, JSON.stringify(host)].join(';')
            """).AsString().Should().Be("1;2;1,2;1,2;2;[1,2]");
    }

    /// <summary>
    /// A plain wrapper's target reports a public <c>toJSON</c> method by name, so redefining or freezing it lists it
    /// once, in the target's place.
    /// </summary>
    [Test]
    public void APlainWrappersToJsonIsListedOnce()
    {
        var engine = CreateEngine(new JsonRecord());

        engine.Evaluate(
            """
            const before = Reflect.ownKeys(host).join();
            Object.defineProperty(host, 'toJSON', { value() { return 'script'; }, configurable: true });
            const redefined = Reflect.ownKeys(host).join();
            Object.freeze(host);
            [before, redefined, Reflect.ownKeys(host).join(), JSON.stringify(host)].join(';')
            """).AsString().Should().Be("Value,toJSON;Value,toJSON;Value,toJSON;\"script\"");
    }

    /// <summary>
    /// A proxy's <c>ownKeys</c> trap may not hide a non-configurable property of its target
    /// (<see href="https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-ownpropertykeys">[[OwnPropertyKeys]]</see>,
    /// step 21). It could, while the wrapper did not list the projection the script had made non-configurable.
    /// </summary>
    [Test]
    public void AProxyCannotHideANonConfigurableProjection()
    {
        var engine = CreateEngine(new List<int> { 1, 2 });

        engine.Evaluate(
            """
            Object.defineProperty(host, Symbol.iterator, { value: function* () {}, configurable: false });
            let outcome;
            try { Reflect.ownKeys(new Proxy(host, { ownKeys: () => ['0', '1'] })); outcome = 'no error'; }
            catch (e) { outcome = e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; }
            outcome
            """).AsString().Should().Be("TypeError");
    }

    /// <summary>
    /// Over a non-extensible target a proxy's <c>ownKeys</c> trap must report exactly the target's keys (steps 25 to
    /// 29), which now include the projections; a forwarding proxy reports what the wrapper does.
    /// </summary>
    [Test]
    public void AProxyOverAFrozenWrapperMustReportItsProjections()
    {
        var engine = CreateEngine(new List<int> { 1, 2 });

        engine.Evaluate(
            """
            Object.freeze(host);
            let hidden;
            try { Reflect.ownKeys(new Proxy(host, { ownKeys: () => ['0', '1'] })); hidden = 'no error'; }
            catch (e) { hidden = e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; }
            const forwarded = Reflect.ownKeys(new Proxy(host, {})).map(String).join();
            const trapped = Reflect.ownKeys(new Proxy(host, { ownKeys: t => Reflect.ownKeys(t) })).map(String).join();
            [hidden, forwarded, trapped].join(';')
            """).AsString().Should().Be("TypeError;0,1,length,Symbol(Symbol.iterator);0,1,length,Symbol(Symbol.iterator)");
    }

    /// <summary>
    /// The host side reads the same list: <see cref="ObjectInstance.GetOwnPropertyKeys"/> and
    /// <see cref="ObjectInstance.GetOwnProperties"/> on a frozen wrapper include its projections.
    /// </summary>
    [Test]
    public void TheHostSeesTheSameKeys()
    {
        var engine = CreateEngine(new DisposableRecord());
        var wrapper = (ObjectInstance) engine.GetValue("host");

        wrapper.GetOwnPropertyKeys().Should().NotContain(key => key.IsSymbol());

        engine.Execute("Object.freeze(host);");

        wrapper.GetOwnPropertyKeys().Select(key => key.ToString()).Should().Contain("Symbol(Symbol.dispose)");
        wrapper.GetOwnProperties().Select(pair => pair.Key.ToString()).Should().Contain("Symbol(Symbol.dispose)");
    }

    private static Engine CreateEngine(object host)
    {
        var engine = new Engine(options => options.Interop.ArrayConversion = ArrayConversionMode.LiveView);
        engine.SetValue("host", host);
        return engine;
    }

    private static string Strict(string body)
        => "(function () { 'use strict'; try {\n" + body + "\n} catch (e) { return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; } })()";

    private static object CreateShape(string kind) => kind switch
    {
        "List" => new List<int> { 1, 2 },
        "Array" => new[] { 1, 2 },
        "Dictionary" => new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1 },
        "Disposable" => new DisposableRecord(),
        "MemoryStream" => new MemoryStream(),
        "JsonList" => new JsonList { 1, 2 },
        "JsonDictionary" => new JsonDictionary { ["a"] = 1 },
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
    /// A list with a public <c>toJSON</c> method: a view, whose target reports positions only, so <c>toJSON</c> is a
    /// projection.
    /// </summary>
    public sealed class JsonList : List<int>
    {
        public string toJSON() => "clr";
    }

    /// <summary>
    /// A dictionary with a public <c>toJSON</c> method: its target reports its entries only.
    /// </summary>
    public sealed class JsonDictionary : Dictionary<string, int>
    {
        public JsonDictionary() : base(StringComparer.Ordinal)
        {
        }

        public string toJSON() => "clr";
    }
}
