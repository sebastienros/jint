#nullable enable
using System.Collections.ObjectModel;
using System.Dynamic;
using Jint.Native;

namespace Jint.Tests.Runtime.Interop;

/// <summary>
/// The descriptor a wrapped dictionary reports for one of its entries: a data property whose value is the entry's,
/// enumerable, and writable and configurable exactly when the host lets script change the entry - the dictionary
/// accepts writes and <see cref="Options.InteropOptions.AllowWrite"/> is on. Whatever the key type, a string-keyed
/// <c>Dictionary&lt;string, T&gt;</c> and a <c>Dictionary&lt;int, T&gt;</c> answer alike, as an ordinary object
/// holding the same key does in Node: <c>Object.getOwnPropertyDescriptor({5: 'a'}, 5)</c> is
/// <c>{ value: 'a', writable: true, enumerable: true, configurable: true }</c>
/// (https://tc39.es/ecma262/#sec-frompropertydescriptor).
/// </summary>
public class DictionaryEntryDescriptorTests
{
    private const string WritableEntry = """{"value":"a","writable":true,"enumerable":true,"configurable":true}""";
    private const string FixedEntry = """{"value":"a","writable":false,"enumerable":true,"configurable":false}""";

    private static Engine CreateEngine(object host, bool allowWrite = true)
    {
        var engine = new Engine(options => options.Interop.AllowWrite = allowWrite);
        engine.SetValue("x", host);
        return engine;
    }

    /// <summary>
    /// Whether the host's keys are what the wrapper lists. A non-string-keyed dictionary that is not a non-generic
    /// IDictionary lists its CLR members instead, which is the key listing's business rather than the descriptor's.
    /// </summary>
    private static bool ListsItsKeys(string name) => name != "IReadOnlyDictionary<int, string> only";

    private static string Describe(Engine engine, string key)
        => engine.Evaluate($"JSON.stringify(Object.getOwnPropertyDescriptor(x, {key}))").ToString();

    /// <summary>Hosts holding the value <c>'a'</c> under a key script names <c>5</c>, and whether they accept writes.</summary>
    private static TestCases<string, Func<object>, bool> Hosts() => new()
    {
        { "Dictionary<int, string>", () => new Dictionary<int, string> { [5] = "a" }, true },
        { "Dictionary<long, string>", () => new Dictionary<long, string> { [5] = "a" }, true },
        { "SortedDictionary<int, string>", () => new SortedDictionary<int, string> { [5] = "a" }, true },
        { "Dictionary<string, string>", () => new Dictionary<string, string> { ["5"] = "a" }, true },
        { "ExpandoObject", () => { IDictionary<string, object?> e = new ExpandoObject(); e["5"] = "a"; return e; }, true },
        { "ReadOnlyDictionary<int, string>", () => new ReadOnlyDictionary<int, string>(new Dictionary<int, string> { [5] = "a" }), false },
        { "ReadOnlyDictionary<string, string>", () => new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["5"] = "a" }), false },
        { "IReadOnlyDictionary<int, string> only", () => new ReadOnlyContractDictionary<int>(5), false },
        { "IReadOnlyDictionary<string, string> only", () => new ReadOnlyContractDictionary<string>("5"), false },
    };

    [TestCaseSource(nameof(Hosts))]
    public void AnEntryIsADataPropertyWritableWhereTheHostAllowsIt(string name, Func<object> host, bool acceptsWrites)
    {
        var engine = CreateEngine(host());

        var expected = acceptsWrites ? WritableEntry : FixedEntry;
        Describe(engine, "5").Should().Be(expected, name);
        Describe(engine, "'5'").Should().Be(expected, name);
        engine.Evaluate("JSON.stringify(Reflect.getOwnPropertyDescriptor(x, 5))").ToString().Should().Be(expected, name);
        if (ListsItsKeys(name))
        {
            engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptors(x)[5])").ToString().Should().Be(expected, name);
        }
    }

    [TestCaseSource(nameof(Hosts))]
    public void WithoutAllowWriteAnEntryIsFixed(string name, Func<object> host, bool acceptsWrites)
    {
        _ = acceptsWrites;
        var engine = CreateEngine(host(), allowWrite: false);

        Describe(engine, "5").Should().Be(FixedEntry, name);
    }

    [TestCaseSource(nameof(Hosts))]
    public void TheEntryReadsAndEnumeratesAsBefore(string name, Func<object> host, bool acceptsWrites)
    {
        _ = acceptsWrites;
        var engine = CreateEngine(host());

        engine.Evaluate("x[5]").Should().Be("a", name);
        engine.Evaluate("x['5']").Should().Be("a", name);
        engine.Evaluate("5 in x").Should().Be(true, name);
        engine.Evaluate("Object.prototype.hasOwnProperty.call(x, 5)").Should().Be(true, name);
        if (ListsItsKeys(name))
        {
            engine.Evaluate("Object.keys(x).join()").Should().Be("5", name);
        }

        engine.Evaluate("Object.getOwnPropertyDescriptor(x, 7)").Should().Be(JsValue.Undefined, name);
        engine.Evaluate("x[7]").Should().Be(JsValue.Undefined, name);
    }

    [TestCaseSource(nameof(Hosts))]
    public void FreezingFixesEveryEntryAndIsFrozenAgrees(string name, Func<object> host, bool acceptsWrites)
    {
        _ = acceptsWrites;
        var target = host();
        var engine = CreateEngine(target);

        // https://tc39.es/ecma262/#sec-setintegritylevel, then https://tc39.es/ecma262/#sec-testintegritylevel
        engine.Evaluate("Object.isFrozen(Object.freeze(x))").Should().Be(true, name);
        Describe(engine, "5").Should().Be(FixedEntry, name);

        engine.Evaluate("x[5] = 'z'; x[5]").Should().Be("a", name);
        engine.Evaluate("delete x[5]").Should().Be(false, name);
        engine.Evaluate("x[5]").Should().Be("a", name);
        engine.Evaluate("(function () { 'use strict'; try { x[5] = 'z'; return 'written'; } catch (e) { return e.constructor.name; } })()")
            .Should().Be("TypeError", name);
    }

    [Test]
    public void AWritableEntryCanBeDeletedAndTheDictionaryLosesIt()
    {
        var dictionary = new Dictionary<int, string> { [5] = "a", [6] = "b" };
        var engine = CreateEngine(dictionary);

        // https://tc39.es/ecma262/#sec-ordinarydelete: a configurable property is removed and the delete answers true
        engine.Evaluate("delete x[5]").Should().Be(true);
        dictionary.ContainsKey(5).Should().BeFalse();
        engine.Evaluate("5 in x").Should().Be(false);
        engine.Evaluate("Object.getOwnPropertyDescriptor(x, 5)").Should().Be(JsValue.Undefined);

        engine.Evaluate("delete x['6']").Should().Be(true);
        dictionary.Should().BeEmpty();
    }

    [Test]
    public void AFixedEntryRefusesDeletion()
    {
        var dictionary = new Dictionary<int, string> { [5] = "a" };
        var engine = CreateEngine(new ReadOnlyDictionary<int, string>(dictionary));

        engine.Evaluate("delete x[5]").Should().Be(false);
        engine.Evaluate("(function () { 'use strict'; try { delete x[5]; return 'deleted'; } catch (e) { return e.constructor.name; } })()")
            .Should().Be("TypeError");
        dictionary.ContainsKey(5).Should().BeTrue();
    }

    [Test]
    public void AnEntryTheHostRemovedIsNoLongerAnOwnProperty()
    {
        // the entry's descriptor is the dictionary's answer at the time it is asked, never one kept from an earlier read
        var dictionary = new Dictionary<int, string> { [5] = "a" };
        var engine = CreateEngine(dictionary);

        engine.Evaluate("x[5]").Should().Be("a");
        Describe(engine, "5").Should().Be(WritableEntry);

        dictionary.Remove(5);
        engine.Evaluate("5 in x").Should().Be(false);
        engine.Evaluate("Object.prototype.hasOwnProperty.call(x, 5)").Should().Be(false);
        engine.Evaluate("Object.getOwnPropertyDescriptor(x, 5)").Should().Be(JsValue.Undefined);

        dictionary[5] = "b";
        engine.Evaluate("x[5]").Should().Be("b");
        engine.Evaluate("Object.getOwnPropertyDescriptor(x, 5).value").Should().Be("b");
    }

    [Test]
    public void AWriteThroughTheWrapperReachesTheDictionaryAndTheDescriptor()
    {
        var dictionary = new Dictionary<int, string> { [5] = "a" };
        var engine = CreateEngine(dictionary);

        engine.Evaluate("x[5] = 'b'; x[6] = 'c'");
        dictionary[5].Should().Be("b");
        dictionary[6].Should().Be("c");
        engine.Evaluate("Object.getOwnPropertyDescriptor(x, 5).value").Should().Be("b");
        engine.Evaluate("Object.getOwnPropertyDescriptor(x, 6).value").Should().Be("c");
    }

    [Test]
    public void DefinePropertyIsValidatedAgainstTheEntry()
    {
        // https://tc39.es/ecma262/#sec-validateandapplypropertydescriptor: a non-configurable, non-writable entry
        // refuses a new value and accepts its own
        var fixedEngine = CreateEngine(new ReadOnlyDictionary<int, string>(new Dictionary<int, string> { [5] = "a" }));
        fixedEngine.Evaluate("try { Object.defineProperty(x, 5, { value: 'dp' }); 'defined' } catch (e) { e.constructor.name }")
            .Should().Be("TypeError");
        fixedEngine.Evaluate("Object.defineProperty(x, 5, { value: 'a' }) === x").Should().Be(true);
        fixedEngine.Evaluate("x[5]").Should().Be("a");

        // a writable, configurable one accepts the redefinition, as a string-keyed dictionary's entry does
        foreach (var host in new object[] { new Dictionary<int, string> { [5] = "a" }, new Dictionary<string, string> { ["5"] = "a" } })
        {
            var engine = CreateEngine(host);
            engine.Evaluate("Object.defineProperty(x, 5, { value: 'dp' }); x[5]").Should().Be("dp");
            engine.Evaluate("Object.getOwnPropertyDescriptor(x, 5).value").Should().Be("dp");
        }
    }

    [Test]
    public void AnEntryNamedByAKeyTheNameConvertsToIsADataProperty()
    {
        var dictionary = new Dictionary<DayOfWeek, string> { [DayOfWeek.Monday] = "a" };
        var engine = CreateEngine(dictionary);

        Describe(engine, "'Monday'").Should().Be(WritableEntry);
        engine.Evaluate("delete x.Monday").Should().Be(true);
        dictionary.Should().BeEmpty();
        engine.Evaluate("Object.getOwnPropertyDescriptor(x, 'Monday')").Should().Be(JsValue.Undefined);
    }

    [Test]
    public void ANameTheDictionaryCannotHoldIsNotAnEntry()
    {
        // "expando" converts to no int, so it is a property of the wrapper itself (#4518), described as one
        var dictionary = new Dictionary<int, string> { [5] = "a" };
        var engine = CreateEngine(dictionary);

        engine.Evaluate("x.expando = 'e'; x.expando").Should().Be("e");
        engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptor(x, 'expando'))").ToString()
            .Should().Be("""{"value":"e","writable":true,"enumerable":true,"configurable":true}""");
        dictionary.Should().HaveCount(1);
        engine.Evaluate("delete x.expando").Should().Be(true);
        engine.Evaluate("'expando' in x").Should().Be(false);
        dictionary.Should().HaveCount(1);
    }

    [Test]
    public void TheDictionarysOwnMembersAreUnchanged()
    {
        var engine = CreateEngine(new Dictionary<int, string> { [5] = "a" });

        engine.Evaluate("x.Count").Should().Be(1);
        engine.Evaluate("x.ContainsKey(5)").Should().Be(true);
        engine.Evaluate("typeof Object.getOwnPropertyDescriptor(x, 'Count').get").Should().Be("function");
    }

    [Test]
    public void AnEntryIsReadThroughThePrototypeChain()
    {
        // OrdinarySet on an object inheriting a writable data property creates an own property on the receiver
        // (https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor); reading through the chain sees the entry
        var engine = CreateEngine(new Dictionary<int, string> { [5] = "a" });

        engine.Evaluate("Object.create(x)[5]").Should().Be("a");
        engine.Evaluate("(function () { 'use strict'; const o = Object.create(Object.freeze(x)); try { o[5] = 'z'; return 'written'; } catch (e) { return e.constructor.name; } })()")
            .Should().Be("TypeError");
    }

    /// <summary>A dictionary implementing <see cref="IReadOnlyDictionary{TKey,TValue}"/> alone: no write contract at all.</summary>
    private sealed class ReadOnlyContractDictionary<TKey>(TKey key) : IReadOnlyDictionary<TKey, string> where TKey : notnull
    {
        private readonly Dictionary<TKey, string> _inner = new() { [key] = "a" };

        public string this[TKey k] => _inner[k];

        public IEnumerable<TKey> Keys => _inner.Keys;

        public IEnumerable<string> Values => _inner.Values;

        public int Count => _inner.Count;

        public bool ContainsKey(TKey k) => _inner.ContainsKey(k);

        public bool TryGetValue(TKey k, out string value) => _inner.TryGetValue(k, out value!);

        public IEnumerator<KeyValuePair<TKey, string>> GetEnumerator() => _inner.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
