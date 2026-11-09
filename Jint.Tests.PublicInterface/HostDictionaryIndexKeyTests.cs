#nullable enable

using System.Collections;
using System.Dynamic;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// An index-shaped key on a wrapped string-keyed dictionary whose list contract holds its entries is one more of
/// its keys (#4501). Newtonsoft's <c>JObject</c> is the reported case; System.Text.Json's <c>JsonObject</c> from
/// System.Text.Json 9 on (the package this suite references on <c>net472</c>, the runtime's own on <c>net10.0</c>,
/// so not on <c>net8.0</c>) and <c>OrderedDictionary&lt;string, TValue&gt;</c> have the same shape.
/// </summary>
/// <remarks>
/// <para>
/// Each of these is an <c>IDictionary&lt;string, _&gt;</c> and an <c>IList&lt;_&gt;</c>, and its list holds a
/// <c>JProperty</c> node or a <c>KeyValuePair</c>, never a value. Script addresses such an object by its keys, which
/// is what every key enumeration already reported, so the expected answers are an ordinary object's. For
/// <c>{ a: 1, b: 2 }</c>: <c>x[0]</c> and its descriptor are undefined
/// (<see href="https://tc39.es/ecma262/#sec-ordinaryget">OrdinaryGet</see>,
/// <see href="https://tc39.es/ecma262/#sec-object.getownpropertydescriptor">Object.getOwnPropertyDescriptor</see>),
/// <c>0 in x</c> is false (<see href="https://tc39.es/ecma262/#sec-ordinaryhasproperty">OrdinaryHasProperty</see>),
/// <c>delete x[0]</c> is true (<see href="https://tc39.es/ecma262/#sec-ordinarydelete">OrdinaryDelete</see>), and
/// <c>Reflect.ownKeys</c> and <c>for…in</c> list <c>a</c> and <c>b</c>
/// (<see href="https://tc39.es/ecma262/#sec-ordinaryownpropertykeys">OrdinaryOwnPropertyKeys</see>,
/// <see href="https://tc39.es/ecma262/#sec-enumerate-object-properties">EnumerateObjectProperties</see>). Node gives
/// the same answers for the same scripts on <c>{ a: 1, b: 2 }</c>. <c>x[0]</c> and <c>x["0"]</c> are one key
/// (<see href="https://tc39.es/ecma262/#sec-topropertykey">ToPropertyKey</see>), so a target that has a <c>"0"</c>
/// key answers it through both spellings.
/// </para>
/// <para>
/// Until #4501 the number spelling reached the list (a <c>JProperty</c> at position 0, an
/// <c>ArgumentException</c> out of a write or a delete under <c>AllowWrite</c>), and an index-shaped string the
/// dictionary did not hold reached the list's indexer through the reflected lane. The controls pin what must not
/// move: <c>JArray</c> keeps its positions, and a plain dictionary answered this way all along.
/// </para>
/// </remarks>
public class HostDictionaryIndexKeyTests
{
    /// <summary>The dictionaries whose list holds entries, each holding <c>a: 1</c> and <c>b: 2</c>.</summary>
    public static IEnumerable<string> EntryListShapes
    {
        get
        {
            var shapes = new List<string> { "JObject", "JsonObject", "ExplicitValueList" };
#if NET9_0_OR_GREATER
            shapes.Add("OrderedDictionary");
#endif
            return shapes;
        }
    }

    /// <summary>The same two keys in dictionaries that have no list at all: the answers the shapes above owe.</summary>
    public static IEnumerable<string> PlainDictionaryShapes => ["Dictionary", "ExpandoObject"];

    public static IEnumerable<string> AllDictionaryShapes => EntryListShapes.Concat(PlainDictionaryShapes);

    public static TestCases<string, bool> EntryListShapesAndSettings
    {
        get
        {
            var data = new TestCases<string, bool>();
            foreach (var shape in EntryListShapes)
            {
                data.Add(shape, false);
                data.Add(shape, true);
            }

            return data;
        }
    }

    /// <summary>Index-shaped keys none of the shapes holds: in range of the list, past it, negative, non-canonical.</summary>
    private static readonly string[] _absentKeys = ["0", "1", "'1'", "5", "-1", "'08'"];

    private const string Absent = "true,true,true,false,false,true,false,a|b,a|b";

    /// <summary>
    /// The lanes script reaches an index through, joined so a failure names every lane that disagreed: the read
    /// in both spellings and through <c>Reflect.get</c>, <c>in</c>, <c>hasOwnProperty</c>, the descriptor,
    /// <c>propertyIsEnumerable</c>, then <c>Reflect.ownKeys</c> and <c>for…in</c>.
    /// </summary>
    private static string Lanes(Engine engine, string key) => engine.Evaluate($$"""
        [
            x[{{key}}] === undefined,
            x[String({{key}})] === undefined,
            Reflect.get(x, {{key}}) === undefined,
            ({{key}} in x),
            Object.prototype.hasOwnProperty.call(x, {{key}}),
            Object.getOwnPropertyDescriptor(x, {{key}}) === undefined,
            Object.prototype.propertyIsEnumerable.call(x, {{key}}),
            Reflect.ownKeys(x).join('|'),
            (() => { const keys = []; for (const k in x) keys.push(k); return keys.join('|'); })()
        ].join(',')
        """).AsString();

    [TestCaseSource(nameof(AllDictionaryShapes))]
    public void AnIndexIsAKeyTheObjectDoesNotHave(string shape)
    {
        foreach (var key in _absentKeys)
        {
            var (engine, _) = CreateEngine(shape, allowWrite: false);

            Lanes(engine, key).Should().Be(Absent, "{0}[{1}] is a key the object does not have", shape, key);
        }
    }

    /// <summary>
    /// The issue's two reproductions. Both threw a CLR exception out of <c>Evaluate</c>.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void TheIssuesReproduction(bool allowWrite)
    {
        var jobj = JObject.Parse("""{ "a": 1 }""");
        var engine = new Engine(options => options.Interop.AllowWrite = allowWrite).SetValue("jobj", jobj);

        engine.Evaluate("Object.getOwnPropertyDescriptor(jobj, 0) === undefined").Should().Be(true);
        engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptor(jobj, 0))").Should().BeUndefined();
        engine.Evaluate("delete jobj[0]").Should().Be(true);
        jobj.ToString(Formatting.None).Should().Be("""{"a":1}""");
    }

    /// <summary>
    /// A key that spells an index and that the object does hold is read through both spellings, and every lane
    /// agrees it is there.
    /// </summary>
    [TestCaseSource(nameof(AllDictionaryShapes))]
    public void AnIndexShapedKeyTheObjectHasIsReadThroughEitherSpelling(string shape)
    {
        var (engine, _) = CreateEngine(shape, allowWrite: false, withIndexShapedKey: true);

        engine.Evaluate("""
            [
                String(x[0]),
                String(x['0']),
                String(Reflect.get(x, 0)),
                0 in x,
                Object.prototype.hasOwnProperty.call(x, 0),
                String(Object.getOwnPropertyDescriptor(x, 0).value),
                Reflect.ownKeys(x).join('|')
            ].join(',')
            """).AsString().Should().Be("7,7,7,true,true,7,0|a");
    }

    /// <summary>
    /// Deleting an index is deleting the key: true for a key that is not there, under every setting. A key that is
    /// there is removed under <c>AllowWrite</c> and refused without it, as every entry is.
    /// </summary>
    [TestCaseSource(nameof(EntryListShapesAndSettings))]
    public void DeletingAnIndexIsDeletingAKey(string shape, bool allowWrite)
    {
        var (engine, read) = CreateEngine(shape, allowWrite);
        engine.Evaluate(Strict("return [delete x[0], delete x['1'], delete x[5]].join(',');")).AsString()
            .Should().Be("true,true,true", "no key spells 0, 1 or 5");
        read().Should().Be("a=1,b=2");

        (engine, read) = CreateEngine(shape, allowWrite, withIndexShapedKey: true);
        if (allowWrite)
        {
            engine.Evaluate(Strict("return [delete x[0], 0 in x].join(',');")).AsString().Should().Be("true,false");
            read().Should().Be("a=1");
        }
        else
        {
            engine.Evaluate("delete x[0]").Should().Be(false);
            engine.Evaluate(Strict("delete x[0]; return 'no-throw';")).AsString().Should().Be("TypeError");
            read().Should().Be("0=7,a=1");
        }
    }

    /// <summary>
    /// Writing an index is writing the key, exactly as <c>x.c = 5</c> writes <c>c</c>: added under
    /// <c>AllowWrite</c>, refused without it (<see href="https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor">OrdinarySetWithOwnDescriptor</see>
    /// answers false, a <c>TypeError</c> in strict mode).
    /// </summary>
    [TestCaseSource(nameof(EntryListShapesAndSettings))]
    public void WritingAnIndexIsWritingAKey(string shape, bool allowWrite)
    {
        var (engine, read) = CreateEngine(shape, allowWrite);

        engine.Evaluate(Strict("x[0] = 5; x['1'] = 6; return [String(x[0]), String(x[1]), Reflect.set(x, 2, 7)].join(',');")).AsString()
            .Should().Be(allowWrite ? "5,6,true" : "TypeError");
        engine.Execute("x[0] = 5;");
        engine.Evaluate("Reflect.set(x, 2, 7)").Should().Be(allowWrite);

        read().Should().Be(allowWrite ? "a=1,b=2,0=5,1=6,2=7" : "a=1,b=2");
    }

    /// <summary>
    /// An index is never handed to the list, so the <c>Array.prototype</c> generics see an ordinary object with no
    /// <c>length</c> (<see href="https://tc39.es/ecma262/#sec-lengthofarraylike">LengthOfArrayLike</see> is 0) and
    /// the object is the ordinary object a dictionary is. <c>Array.prototype.slice.call(jobj)</c> threw
    /// <c>InvalidOperationException</c>; writing <c>length</c> under <c>AllowWrite</c> cleared every key.
    /// </summary>
    [TestCaseSource(nameof(EntryListShapesAndSettings))]
    public void TheObjectIsAnOrdinaryObjectToScript(string shape, bool allowWrite)
    {
        var (engine, read) = CreateEngine(shape, allowWrite);

        engine.Evaluate("""
            [
                Object.getPrototypeOf(x) === Object.prototype,
                Array.isArray(x),
                x.length === undefined,
                Array.prototype.slice.call(x).length,
                Array.prototype.map.call(x, v => v).length,
                Array.prototype.indexOf.call(x, 1)
            ].join(',')
            """).AsString().Should().Be("true,false,true,0,0,-1");

        engine.Execute("x.length = 0;");
        read().Should().StartWith("a=1,b=2", "a length write is a key write, not a truncation");
    }

    /// <summary>
    /// Wrapped as a dictionary is, the object follows OrdinaryDelete for its keys, which does not read
    /// extensibility: <c>Object.preventExtensions</c> leaves a key deletable under <c>AllowWrite</c>, while
    /// <c>Object.freeze</c> makes it non-configurable. A <c>Dictionary&lt;string, int&gt;</c> answers the same.
    /// </summary>
    [TestCase("JObject")]
    [TestCase("JsonObject")]
    [TestCase("Dictionary")]
    public void PreventExtensionsLeavesAKeyDeletableAndFreezeDoesNot(string shape)
    {
        var (engine, read) = CreateEngine(shape, allowWrite: true);
        engine.Evaluate("Object.preventExtensions(x); [delete x.a, 'a' in x, Object.isExtensible(x)].join(',')").AsString()
            .Should().Be("true,false,false");
        read().Should().Be("b=2");

        (engine, read) = CreateEngine(shape, allowWrite: true);
        engine.Evaluate(Strict("Object.freeze(x); delete x.a; return 'no-throw';")).AsString().Should().Be("TypeError");
        read().Should().Be("a=1,b=2");
    }

    /// <summary>
    /// What enumerates the keys answered them before and still does; pinned so that the classification cannot be
    /// mistaken for a change to them (<see href="https://tc39.es/ecma262/#sec-json.stringify">JSON.stringify</see>).
    /// </summary>
    [Test]
    public void TheKeyEnumerationsAreUnchanged()
    {
        var (engine, _) = CreateEngine("JObject", allowWrite: false);

        engine.Evaluate("JSON.stringify(x)").Should().Be("""{"a":1,"b":2}""");
        engine.Evaluate("JSON.stringify(Object.keys(x))").Should().Be("""["a","b"]""");
        engine.Evaluate("JSON.stringify(Object.entries(x))").Should().Be("""[["a",1],["b",2]]""");
        engine.Evaluate("JSON.stringify({ ...x })").Should().Be("""{"a":1,"b":2}""");
        engine.Evaluate("JSON.stringify([...x])").Should().Be("""[["a",1],["b",2]]""");
        engine.Evaluate("x.a + x['b']").Should().Be(3);
    }

    /// <summary>
    /// A <c>JArray</c> is a list and no dictionary, and keeps every position
    /// (<see href="https://tc39.es/ecma262/#sec-array.prototype.slice">Array.prototype.slice</see> reads them).
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AJArrayKeepsItsPositions(bool allowWrite)
    {
        var array = JArray.Parse("[1, 2]");
        var engine = new Engine(options => options.Interop.AllowWrite = allowWrite).SetValue("x", array);

        engine.Evaluate("""
            [
                x[0],
                x['1'],
                0 in x,
                5 in x,
                Object.getOwnPropertyDescriptor(x, 0) !== undefined,
                Object.getOwnPropertyDescriptor(x, 5) === undefined,
                Reflect.ownKeys(x).filter(k => typeof k === 'string').join('|'),
                Array.prototype.slice.call(x).join('|'),
                x.length,
                Object.getPrototypeOf(x) === Array.prototype,
                delete x[5]
            ].join(',')
            """).AsString().Should().Be("1,2,true,false,true,true,0|1,1|2,2,true,true");

        engine.Evaluate("Reflect.set(x, 0, 'z')").Should().Be(allowWrite);
        array.ToString(Formatting.None).Should().Be(allowWrite ? """["z",2]""" : "[1,2]");
    }

    /// <summary>
    /// A type that is a dictionary and a list of its values, and that declares an integer indexer of its own,
    /// keeps its positions as before: only a list of entries, or one reachable through the interface alone, is
    /// addressed by key. That holds for a non-generic <see cref="IList"/> too, whose element type says nothing.
    /// </summary>
    [TestCase("IList<int>")]
    [TestCase("IList")]
    public void AValueListWithAnIndexerOfItsOwnKeepsItsPositions(string contract)
    {
        object target = contract == "IList"
            ? new NonGenericIndexedValueList { { "a", 1 }, { "b", 2 } }
            : new IndexedValueList { { "a", 1 }, { "b", 2 } };
        var engine = new Engine().SetValue("x", target);

        engine.Evaluate("[x[0], x['1'], 0 in x, Reflect.ownKeys(x).join('|'), x.a].join(',')").AsString()
            .Should().Be("1,2,true,a|b,1");
    }

    private static string Strict(string body)
        => "(function () { 'use strict'; try {\n" + body + "\n} catch (e) { return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; } })()";

    /// <summary>
    /// An engine with <c>x</c> bound to <paramref name="shape"/> holding <c>a: 1, b: 2</c>, or <c>"0": 7, a: 1</c>
    /// when <paramref name="withIndexShapedKey"/>, and a reader of the host's view of its entries.
    /// </summary>
    private static (Engine Engine, Func<string> Read) CreateEngine(string shape, bool allowWrite, bool withIndexShapedKey = false)
    {
        var entries = withIndexShapedKey
            ? new[] { ("0", 7), ("a", 1) }
            : new[] { ("a", 1), ("b", 2) };

        var (target, read) = CreateTarget(shape, entries);
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = allowWrite;
#if NETFRAMEWORK
            // Jint converts a JsonValue to a primitive on .NET 8 and later only
            options.AddObjectConverter(JsonValueConverter.Instance);
#endif
        });
        engine.SetValue("x", target);
        return (engine, read);
    }

#if NETFRAMEWORK
    private sealed class JsonValueConverter : Jint.Runtime.Interop.ObjectConverter
    {
        public static readonly JsonValueConverter Instance = new();

        public override bool TryConvert(Engine engine, object value, out Jint.Native.JsValue result)
        {
            if (value is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var number))
            {
                result = Jint.Native.JsNumber.Create(number);
                return true;
            }

            if (value is JsonValue doubleValue && doubleValue.TryGetValue<double>(out var real))
            {
                result = Jint.Native.JsNumber.Create(real);
                return true;
            }

            result = Jint.Native.JsValue.Undefined;
            return false;
        }
    }
#endif

    private static (object Target, Func<string> Read) CreateTarget(string shape, (string Key, int Value)[] entries)
    {
        switch (shape)
        {
            case "JObject":
            {
                var target = new JObject();
                foreach (var (key, value) in entries)
                {
                    target[key] = value;
                }

                return (target, () => Join(target.Properties().Select(p => (p.Name, p.Value.Value<double>()))));
            }
            case "JsonObject":
            {
                var target = new JsonObject();
                foreach (var (key, value) in entries)
                {
                    target[key] = value;
                }

                return (target, () => Join(target.Select(p => (p.Key, double.Parse(p.Value!.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture)))));
            }
            case "ExplicitValueList":
            {
                var target = new ExplicitValueList();
                foreach (var (key, value) in entries)
                {
                    target.Add(key, value);
                }

                IEnumerable<KeyValuePair<string, int>> pairs = target;
                return (target, () => Join(pairs.Select(p => (p.Key, (double) p.Value))));
            }
#if NET9_0_OR_GREATER
            case "OrderedDictionary":
            {
                var target = new OrderedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var (key, value) in entries)
                {
                    target.Add(key, value);
                }

                return (target, () => Join(target.Select(p => (p.Key, (double) p.Value))));
            }
#endif
            case "Dictionary":
            {
                var target = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var (key, value) in entries)
                {
                    target.Add(key, value);
                }

                return (target, () => Join(target.Select(p => (p.Key, (double) p.Value))));
            }
            case "ExpandoObject":
            {
                IDictionary<string, object?> target = new ExpandoObject();
                foreach (var (key, value) in entries)
                {
                    target.Add(key, value);
                }

                return (target, () => Join(target.Select(p => (p.Key, Convert.ToDouble(p.Value, System.Globalization.CultureInfo.InvariantCulture)))));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown shape");
        }
    }

    private static string Join(IEnumerable<(string Key, double Value)> entries)
        => string.Join(",", entries.Select(e => e.Key + "=" + e.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>
    /// A string-keyed dictionary that is also a list of its values, with the list reachable through the interface
    /// alone: the shape of <c>JObject</c> without Newtonsoft, which <c>JContainer</c> implements explicitly.
    /// </summary>
    private sealed class ExplicitValueList : IDictionary<string, int>, IList<int>
    {
        private readonly List<KeyValuePair<string, int>> _entries = [];

        public int this[string key]
        {
            get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);
            set
            {
                var index = _entries.FindIndex(e => string.Equals(e.Key, key, StringComparison.Ordinal));
                if (index >= 0)
                {
                    _entries[index] = new(key, value);
                }
                else
                {
                    _entries.Add(new(key, value));
                }
            }
        }

        int IList<int>.this[int index]
        {
            get => _entries[index].Value;
            set => throw new NotSupportedException("positions are not writable");
        }

        public ICollection<string> Keys => _entries.Select(e => e.Key).ToList();

        public ICollection<int> Values => _entries.Select(e => e.Value).ToList();

        public int Count => _entries.Count;

        public bool IsReadOnly => false;

        public void Add(string key, int value)
        {
            if (ContainsKey(key))
            {
                throw new ArgumentException("duplicate key", nameof(key));
            }

            _entries.Add(new(key, value));
        }

        public void Add(KeyValuePair<string, int> item) => Add(item.Key, item.Value);

        void ICollection<int>.Add(int item) => throw new NotSupportedException("a value has no key");

        public void Clear() => _entries.Clear();

        public bool Contains(KeyValuePair<string, int> item) => _entries.Contains(item);

        bool ICollection<int>.Contains(int item) => _entries.Exists(e => e.Value == item);

        public bool ContainsKey(string key) => _entries.Exists(e => string.Equals(e.Key, key, StringComparison.Ordinal));

        public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex) => _entries.CopyTo(array, arrayIndex);

        void ICollection<int>.CopyTo(int[] array, int arrayIndex) => Values.CopyTo(array, arrayIndex);

        int IList<int>.IndexOf(int item) => _entries.FindIndex(e => e.Value == item);

        void IList<int>.Insert(int index, int item) => throw new NotSupportedException("a value has no key");

        public bool Remove(string key) => _entries.RemoveAll(e => string.Equals(e.Key, key, StringComparison.Ordinal)) > 0;

        public bool Remove(KeyValuePair<string, int> item) => _entries.Remove(item);

        bool ICollection<int>.Remove(int item) => throw new NotSupportedException("a value has no key");

        void IList<int>.RemoveAt(int index) => throw new NotSupportedException("positions are not removable");

        public bool TryGetValue(string key, out int value)
        {
            var index = _entries.FindIndex(e => string.Equals(e.Key, key, StringComparison.Ordinal));
            value = index >= 0 ? _entries[index].Value : 0;
            return index >= 0;
        }

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => _entries.GetEnumerator();

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A string-keyed dictionary that is also a non-generic <see cref="IList"/> of its values and declares the integer
    /// indexer itself.
    /// </summary>
    private sealed class NonGenericIndexedValueList : Dictionary<string, int>, IList
    {
        public object? this[int index] { get => Values.ElementAt(index); set => throw new NotSupportedException(); }

        bool IList.IsFixedSize => true;

        bool IList.IsReadOnly => true;

        int IList.Add(object? value) => throw new NotSupportedException();

        bool IList.Contains(object? value) => value is int item && ContainsValue(item);

        int IList.IndexOf(object? value) => throw new NotSupportedException();

        void IList.Insert(int index, object? value) => throw new NotSupportedException();

        void IList.Remove(object? value) => throw new NotSupportedException();

        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }

    /// <summary>
    /// A string-keyed dictionary that is also a list of its values and declares the integer indexer itself.
    /// </summary>
    private sealed class IndexedValueList : IDictionary<string, int>, IList<int>
    {
        private readonly ExplicitValueList _inner = new();

        public int this[string key] { get => _inner[key]; set => _inner[key] = value; }

        public int this[int index] { get => ((IList<int>) _inner)[index]; set => throw new NotSupportedException(); }

        public ICollection<string> Keys => _inner.Keys;

        public ICollection<int> Values => _inner.Values;

        public int Count => _inner.Count;

        public bool IsReadOnly => false;

        public void Add(string key, int value) => _inner.Add(key, value);

        public void Add(KeyValuePair<string, int> item) => _inner.Add(item);

        public void Add(int item) => throw new NotSupportedException();

        public void Clear() => _inner.Clear();

        public bool Contains(KeyValuePair<string, int> item) => _inner.Contains(item);

        public bool Contains(int item) => ((IList<int>) _inner).Contains(item);

        public bool ContainsKey(string key) => _inner.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);

        public void CopyTo(int[] array, int arrayIndex) => ((IList<int>) _inner).CopyTo(array, arrayIndex);

        public int IndexOf(int item) => ((IList<int>) _inner).IndexOf(item);

        public void Insert(int index, int item) => throw new NotSupportedException();

        public bool Remove(string key) => _inner.Remove(key);

        public bool Remove(KeyValuePair<string, int> item) => _inner.Remove(item);

        public bool Remove(int item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        public bool TryGetValue(string key, out int value) => _inner.TryGetValue(key, out value);

        IEnumerator<KeyValuePair<string, int>> IEnumerable<KeyValuePair<string, int>>.GetEnumerator() => _inner.GetEnumerator();

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => ((IList<int>) _inner).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }
}
