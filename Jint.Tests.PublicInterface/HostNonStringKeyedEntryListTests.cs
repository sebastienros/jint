#nullable enable

using System.Collections;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A wrapped dictionary whose keys are not strings and which is also a list of its entries is the keyed object a
/// <see cref="Dictionary{TKey,TValue}"/> of the same keys is (#4527). <c>OrderedDictionary&lt;int, TValue&gt;</c>
/// (.NET 9 on, so not on <c>net8.0</c> or <c>net481</c>) is the reported case; <see cref="ExplicitEntryList"/> has its
/// shape on every target framework.
/// </summary>
/// <remarks>
/// <para>
/// Each holds <c>5: "a"</c> and <c>7: "b"</c>, so positions 0 and 1 of its list exist and are not keys. Until #4527
/// it was wrapped as a view of that list: <c>x[0]</c> read a <c>KeyValuePair</c>, <c>x[5]</c> was past the list's end,
/// <c>0 in x</c> was true, while <c>Reflect.ownKeys</c> listed the dictionary's keys, and a write or a delete reached
/// the list. The expected answers are an ordinary object's, the ones Node gives for <c>{ 5: "a", 7: "b" }</c>:
/// <c>x[0]</c> and its descriptor are undefined
/// (<see href="https://tc39.es/ecma262/#sec-ordinaryget">OrdinaryGet</see>,
/// <see href="https://tc39.es/ecma262/#sec-object.getownpropertydescriptor">Object.getOwnPropertyDescriptor</see>),
/// <c>x[5]</c> and <c>x["5"]</c> are one key (<see href="https://tc39.es/ecma262/#sec-topropertykey">ToPropertyKey</see>),
/// <c>0 in x</c> is false (<see href="https://tc39.es/ecma262/#sec-ordinaryhasproperty">OrdinaryHasProperty</see>),
/// <c>Reflect.ownKeys</c> and <c>for…in</c> list <c>5</c> and <c>7</c>
/// (<see href="https://tc39.es/ecma262/#sec-ordinaryownpropertykeys">OrdinaryOwnPropertyKeys</see>,
/// <see href="https://tc39.es/ecma262/#sec-enumerate-object-properties">EnumerateObjectProperties</see>), there is no
/// <c>length</c> (<see href="https://tc39.es/ecma262/#sec-lengthofarraylike">LengthOfArrayLike</see> is 0), and
/// <c>delete x[0]</c> is true (<see href="https://tc39.es/ecma262/#sec-ordinarydelete">OrdinaryDelete</see>).
/// </para>
/// <para>
/// The controls are the int-keyed dictionaries that have no list: <see cref="Dictionary{TKey,TValue}"/>,
/// <see cref="SortedList{TKey,TValue}"/> (whose <c>Keys</c> and <c>Values</c> are lists, but which is not one itself)
/// and <see cref="SortedDictionary{TKey,TValue}"/>. They answered this way before and must not move.
/// </para>
/// </remarks>
public class HostNonStringKeyedEntryListTests
{
    /// <summary>The int-keyed dictionaries that are also a list of their entries.</summary>
    public static IEnumerable<string> EntryListShapes
    {
        get
        {
            var shapes = new List<string> { "ExplicitEntryList" };
#if NET9_0_OR_GREATER
            shapes.Add("OrderedDictionary");
#endif
            return shapes;
        }
    }

    /// <summary>The same keys in int-keyed dictionaries that are no list: the answers the shapes above owe.</summary>
    public static IEnumerable<string> PlainDictionaryShapes => ["Dictionary", "SortedList", "SortedDictionary"];

    public static IEnumerable<string> AllShapes => EntryListShapes.Concat(PlainDictionaryShapes);

    public static TestCases<string, bool> AllShapesAndSettings
    {
        get
        {
            var data = new TestCases<string, bool>();
            foreach (var shape in AllShapes)
            {
                data.Add(shape, false);
                data.Add(shape, true);
            }

            return data;
        }
    }

    /// <summary>
    /// Every lane script reaches a key through, joined so that a failure names each lane that disagreed.
    /// </summary>
    private const string Lanes = """
        [
            String(x[0]),
            String(x['0']),
            String(x[1]),
            x[5],
            x['5'],
            Reflect.get(x, 7),
            0 in x,
            '1' in x,
            5 in x,
            '7' in x,
            Object.prototype.hasOwnProperty.call(x, 0),
            Object.prototype.hasOwnProperty.call(x, 5),
            Object.getOwnPropertyDescriptor(x, 0) === undefined,
            Object.getOwnPropertyDescriptor(x, 5) !== undefined,
            Object.prototype.propertyIsEnumerable.call(x, 7),
            Reflect.ownKeys(x).join('|'),
            Object.keys(x).join('|'),
            (() => { const keys = []; for (const k in x) keys.push(k); return keys.join('|'); })()
        ].join(',')
        """;

    /// <remarks>
    /// The descriptor of a present key is only tested for existence: on every int-keyed dictionary, the controls
    /// included, it is the reflected indexer's accessor descriptor where Node has a data descriptor, which is not
    /// what this fixture is about.
    /// </remarks>
    private const string NodeLanes = "undefined,undefined,undefined,a,a,b,false,false,true,true,false,true,true,true,true,5|7,5|7,5|7";

    [TestCaseSource(nameof(AllShapes))]
    public void EveryLaneAgreesOnTheKeys(string shape)
    {
        var (engine, _) = CreateEngine(shape, allowWrite: false);

        engine.Evaluate(Lanes).AsString().Should().Be(NodeLanes);
    }

    /// <summary>
    /// The object is the ordinary object a dictionary is: no list view, no <c>length</c>, nothing for the
    /// <c>Array.prototype</c> generics (<see href="https://tc39.es/ecma262/#sec-array.prototype.slice">Array.prototype.slice</see>).
    /// </summary>
    [TestCaseSource(nameof(AllShapes))]
    public void TheObjectIsAnOrdinaryObjectToScript(string shape)
    {
        var (engine, _) = CreateEngine(shape, allowWrite: false);

        engine.Evaluate("""
            [
                Object.getPrototypeOf(x) === Object.prototype,
                Array.isArray(x),
                x.length === undefined,
                Array.prototype.slice.call(x).length,
                Array.prototype.indexOf.call(x, 'a')
            ].join(',')
            """).AsString().Should().Be("true,false,true,0,-1");
    }

    /// <summary>
    /// What enumerates the entries answers as it does for a <see cref="Dictionary{TKey,TValue}"/>
    /// (<see href="https://tc39.es/ecma262/#sec-json.stringify">JSON.stringify</see>).
    /// </summary>
    [TestCaseSource(nameof(AllShapes))]
    public void TheEnumerationsAnswerAsADictionarysDo(string shape)
    {
        var (engine, _) = CreateEngine(shape, allowWrite: false);

        engine.Evaluate("JSON.stringify(x)").Should().Be("""{"5":"a","7":"b"}""");
        engine.Evaluate("JSON.stringify(Object.entries(x))").Should().Be("""[["5","a"],["7","b"]]""");
        engine.Evaluate("JSON.stringify({ ...x })").Should().Be("""{"5":"a","7":"b"}""");
    }

    /// <summary>
    /// Writing a number is writing that key, under <c>AllowWrite</c>, and is refused without it
    /// (<see href="https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor">OrdinarySetWithOwnDescriptor</see>
    /// answers false, a <c>TypeError</c> in strict mode). <c>x[0]</c> is a new key, not position 0.
    /// </summary>
    [TestCaseSource(nameof(AllShapesAndSettings))]
    public void WritingANumberIsWritingAKey(string shape, bool allowWrite)
    {
        var (engine, read) = CreateEngine(shape, allowWrite);

        engine.Evaluate(Strict("x[0] = 'z'; x[5] = 'c'; return [x[0], x[5], Reflect.set(x, 9, 'd')].join(',');")).AsString()
            .Should().Be(allowWrite ? "z,c,true" : "TypeError");
        engine.Evaluate("Reflect.set(x, 9, 'd')").Should().Be(allowWrite);

        read().Should().Be(allowWrite ? "0=z,5=c,7=b,9=d" : "5=a,7=b");
    }

    /// <summary>
    /// Deleting a number is deleting that key: true for a key the dictionary does not hold, under every setting; a
    /// key it holds is removed under <c>AllowWrite</c> and refused without it.
    /// </summary>
    [TestCaseSource(nameof(AllShapesAndSettings))]
    public void DeletingANumberIsDeletingAKey(string shape, bool allowWrite)
    {
        var (engine, read) = CreateEngine(shape, allowWrite);

        engine.Evaluate("[delete x[0], delete x[1], delete x['3']].join(',')").AsString().Should().Be("true,true,true");
        read().Should().Be("5=a,7=b");

        if (allowWrite)
        {
            engine.Evaluate(Strict("return [delete x[5], 5 in x, Reflect.ownKeys(x).join('|')].join(',');")).AsString()
                .Should().Be("true,false,7");
            read().Should().Be("7=b");
        }
        else
        {
            engine.Evaluate("delete x[5]").Should().Be(false);
            engine.Evaluate(Strict("delete x[5]; return 'no-throw';")).AsString().Should().Be("TypeError");
            read().Should().Be("5=a,7=b");
        }
    }

    /// <summary>
    /// The issue's reproduction: position 0 of the list is not a key, and the key the dictionary holds is.
    /// </summary>
    [Test]
    public void TheIssuesReproduction()
    {
        var target = new ExplicitEntryList { [5] = "a" };
#if NET9_0_OR_GREATER
        foreach (object reported in new object[] { target, new OrderedDictionary<int, string> { [5] = "a" } })
#else
        foreach (object reported in new object[] { target })
#endif
        {
            var engine = new Engine().SetValue("x", reported);

            engine.Evaluate("[x[0] === undefined, x[5], Reflect.ownKeys(x).join('|'), 0 in x, 5 in x].join(',')").AsString()
                .Should().Be("true,a,5,false,true", reported.GetType().Name);
        }
    }

    /// <summary>
    /// Handed over through a member, the object is the same keyed object when the member is typed as something other
    /// than a list contract (#4524's rule, which the type-level classification feeds); typed as the list it is, it is
    /// that list, as the declaration names.
    /// </summary>
    /// <remarks>
    /// #4524 lets a declared type defer to the runtime type only for a target that is a non-generic <see cref="IList"/>,
    /// which <c>OrderedDictionary&lt;,&gt;</c> is and <see cref="ExplicitEntryList"/> is not, so the
    /// <c>IEnumerable&lt;KeyValuePair&lt;int, string&gt;&gt;</c> member is asked of the first alone.
    /// </remarks>
    public static TestCases<string, string> MembersNotTypedAsAList
    {
        get
        {
            var data = new TestCases<string, string>();
            foreach (var member in new[] { nameof(Holder<ExplicitEntryList>.AsObject), nameof(Holder<ExplicitEntryList>.AsDictionary), nameof(Holder<ExplicitEntryList>.AsReadOnlyDictionary), nameof(Holder<ExplicitEntryList>.AsItself) })
            {
                data.Add("ExplicitEntryList", member);
#if NET9_0_OR_GREATER
                data.Add("OrderedDictionary", member);
#endif
            }

#if NET9_0_OR_GREATER
            data.Add("OrderedDictionary", nameof(Holder<ExplicitEntryList>.AsEnumerable));
#endif
            return data;
        }
    }

    [TestCaseSource(nameof(MembersNotTypedAsAList))]
    public void AMemberNotTypedAsAListIsTheKeyedObject(string shape, string member)
    {
        var engine = new Engine().SetValue("holder", CreateHolder(shape));
        engine.Execute($"var x = holder.{member};");

        engine.Evaluate(Lanes).AsString().Should().Be(NodeLanes);
        engine.Evaluate("[x.length === undefined, Array.prototype.slice.call(x).length].join(',')").AsString()
            .Should().Be("true,0");
    }

    [TestCaseSource(nameof(EntryListShapes))]
    public void AMemberTypedAsTheListIsTheList(string shape)
    {
        var engine = new Engine().SetValue("holder", CreateHolder(shape));

        engine.Evaluate("var x = holder.AsList; [x.length, x[0].Key, x[1].Value, 0 in x, 5 in x].join(',')").AsString()
            .Should().Be("2,5,b,true,false");
    }

    private static object CreateHolder(string shape) => shape switch
    {
        "ExplicitEntryList" => new Holder<ExplicitEntryList>(new ExplicitEntryList { [5] = "a", [7] = "b" }),
#if NET9_0_OR_GREATER
        "OrderedDictionary" => new Holder<OrderedDictionary<int, string>>(new OrderedDictionary<int, string> { [5] = "a", [7] = "b" }),
#endif
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown shape"),
    };

    public sealed class Holder<T>(T target) where T : IDictionary<int, string>, IReadOnlyDictionary<int, string>, IList<KeyValuePair<int, string>>
    {
        public object AsObject => target;

        public IDictionary<int, string> AsDictionary => target;

        public IReadOnlyDictionary<int, string> AsReadOnlyDictionary => target;

        public IEnumerable<KeyValuePair<int, string>> AsEnumerable => target;

        public T AsItself => target;

        public IList<KeyValuePair<int, string>> AsList => target;
    }

    private static string Strict(string body)
        => "(function () { 'use strict'; try {\n" + body + "\n} catch (e) { return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; } })()";

    private static (Engine Engine, Func<string> Read) CreateEngine(string shape, bool allowWrite)
    {
        var target = CreateTarget(shape);
        target.Add(5, "a");
        target.Add(7, "b");

        var engine = new Engine(options => options.Interop.AllowWrite = allowWrite);
        engine.SetValue("x", target);
        return (engine, () => string.Join(",", target.OrderBy(e => e.Key).Select(e => e.Key + "=" + e.Value)));
    }

    private static IDictionary<int, string> CreateTarget(string shape) => shape switch
    {
        "ExplicitEntryList" => new ExplicitEntryList(),
#if NET9_0_OR_GREATER
        "OrderedDictionary" => new OrderedDictionary<int, string>(),
#endif
        "Dictionary" => new Dictionary<int, string>(),
        "SortedList" => new SortedList<int, string>(),
        "SortedDictionary" => new SortedDictionary<int, string>(),
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown shape"),
    };

    /// <summary>
    /// An int-keyed dictionary that is also a list of its entries, the list reachable through the interface alone:
    /// the shape of <c>OrderedDictionary&lt;int, TValue&gt;</c> on every target framework.
    /// </summary>
    public sealed class ExplicitEntryList : IDictionary<int, string>, IReadOnlyDictionary<int, string>, IList<KeyValuePair<int, string>>
    {
        private readonly List<KeyValuePair<int, string>> _entries = [];

        public string this[int key]
        {
            get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key.ToString(System.Globalization.CultureInfo.InvariantCulture));
            set
            {
                var index = _entries.FindIndex(e => e.Key == key);
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

        KeyValuePair<int, string> IList<KeyValuePair<int, string>>.this[int index]
        {
            get => _entries[index];
            set => _entries[index] = value;
        }

        public ICollection<int> Keys => _entries.Select(e => e.Key).ToList();

        public ICollection<string> Values => _entries.Select(e => e.Value).ToList();

        IEnumerable<int> IReadOnlyDictionary<int, string>.Keys => Keys;

        IEnumerable<string> IReadOnlyDictionary<int, string>.Values => Values;

        public int Count => _entries.Count;

        public bool IsReadOnly => false;

        public void Add(int key, string value)
        {
            if (ContainsKey(key))
            {
                throw new ArgumentException("duplicate key", nameof(key));
            }

            _entries.Add(new(key, value));
        }

        public void Add(KeyValuePair<int, string> item) => Add(item.Key, item.Value);

        public void Clear() => _entries.Clear();

        public bool Contains(KeyValuePair<int, string> item) => _entries.Contains(item);

        public bool ContainsKey(int key) => _entries.Exists(e => e.Key == key);

        public void CopyTo(KeyValuePair<int, string>[] array, int arrayIndex) => _entries.CopyTo(array, arrayIndex);

        int IList<KeyValuePair<int, string>>.IndexOf(KeyValuePair<int, string> item) => _entries.IndexOf(item);

        void IList<KeyValuePair<int, string>>.Insert(int index, KeyValuePair<int, string> item) => _entries.Insert(index, item);

        public bool Remove(int key) => _entries.RemoveAll(e => e.Key == key) > 0;

        public bool Remove(KeyValuePair<int, string> item) => _entries.Remove(item);

        void IList<KeyValuePair<int, string>>.RemoveAt(int index) => _entries.RemoveAt(index);

        public bool TryGetValue(int key, out string value)
        {
            var index = _entries.FindIndex(e => e.Key == key);
            value = index >= 0 ? _entries[index].Value : null!;
            return index >= 0;
        }

        public IEnumerator<KeyValuePair<int, string>> GetEnumerator() => _entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}