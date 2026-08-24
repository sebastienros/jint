using System.Reflection;
using Jint.Collections;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see href="https://tc39.es/ecma262/#sec-ordinaryownpropertykeys">OrdinaryOwnPropertyKeys</see> lists
/// string keys, and then symbol keys, "in ascending chronological order of property creation". A
/// <c>delete</c> destroys the property; adding the name back creates a <em>new</em> one, which is
/// therefore the newest key — so the name moves to the end, and a brand-new name added after any delete
/// appends rather than filling the gap.
/// <para>
/// The property stores are hash tables that used to reuse a removed entry's slot from a free list while
/// enumerating in entry-array index order, so a re-added key came back in its old position and a new key
/// landed in the hole (issue #3273). <see cref="PropertyDictionary"/> switches from its list backing to
/// <see cref="StringDictionarySlim{TValue}"/> at nine entries, which is why the string cases below
/// straddle that boundary deliberately; the symbol store is a
/// <see cref="DictionarySlim{TKey,TValue}"/> from the first key, so it had no correct size at all.
/// </para>
/// </summary>
public class OwnPropertyCreationOrderTests
{
    private static string Keys(Engine engine, string expression) => engine.Evaluate(expression).AsString();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(8)]  // last size backed by ListDictionary
    [InlineData(9)]  // first size backed by StringDictionarySlim
    [InlineData(10)]
    [InlineData(16)]
    [InlineData(40)]
    public void ADeletedKeyThatIsAddedBackIsTheNewestKey(int n)
    {
        var engine = new Engine();
        var actual = Keys(engine, $$"""
            var o = {};
            for (var i = 0; i < {{n}}; i++) o['k' + i] = i;
            delete o.k0;
            o.k0 = 99;
            Object.keys(o).join(',');
            """);

        var expected = new List<string>();
        for (var i = 1; i < n; i++)
        {
            expected.Add("k" + i);
        }
        expected.Add("k0");

        actual.Should().Be(string.Join(",", expected));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(24)]
    public void ADeletedKeyInTheMiddleThatIsAddedBackIsTheNewestKey(int n)
    {
        var engine = new Engine();
        var actual = Keys(engine, $$"""
            var o = {};
            for (var i = 0; i < {{n}}; i++) o['k' + i] = i;
            delete o.k3;
            o.k3 = 99;
            Object.keys(o).join(',');
            """);

        var expected = new List<string>();
        for (var i = 0; i < n; i++)
        {
            if (i != 3)
            {
                expected.Add("k" + i);
            }
        }
        expected.Add("k3");

        actual.Should().Be(string.Join(",", expected));
    }

    /// <summary>
    /// A brand-new name added after a delete appends too — the vacated position belongs to no key at all.
    /// This is the half of the defect the free list made worst: two deletes and two unrelated adds put the
    /// new names into the two holes, in reverse order (the free list is LIFO).
    /// </summary>
    [Fact]
    public void NewKeysAddedAfterADeleteAppendRatherThanFillingTheGap()
    {
        var engine = new Engine();
        var actual = Keys(engine, """
            var o = {};
            for (var i = 0; i < 9; i++) o['k' + i] = i;
            delete o.k1;
            delete o.k3;
            o.a = 1;
            o.b = 2;
            Object.keys(o).join(',');
            """);

        actual.Should().Be("k0,k2,k4,k5,k6,k7,k8,a,b");
    }

    [Fact]
    public void EmptyingAndRefillingRebuildsTheOrderFromScratch()
    {
        var engine = new Engine();
        var actual = Keys(engine, """
            var o = {};
            for (var i = 0; i < 9; i++) o['k' + i] = i;
            for (var i = 0; i < 9; i++) delete o['k' + i];
            for (var i = 8; i >= 0; i--) o['k' + i] = i;
            Object.keys(o).join(',');
            """);

        actual.Should().Be("k8,k7,k6,k5,k4,k3,k2,k1,k0");
    }

    /// <summary>
    /// Every own-key listing goes through the one enumeration, so each of these is the same defect seen
    /// from a different built-in. Kept explicit because they are what an embedder actually calls.
    /// </summary>
    [Theory]
    [InlineData("Object.keys(o).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [InlineData("Object.values(o).join(',')", "1,2,3,4,5,6,7,8,99")]
    [InlineData("Object.entries(o).map(function (e) { return e[0]; }).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [InlineData("Object.getOwnPropertyNames(o).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [InlineData("Object.keys(Object.getOwnPropertyDescriptors(o)).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [InlineData("Reflect.ownKeys(o).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [InlineData("Reflect.ownKeys(new Proxy(o, {})).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [InlineData("var r = []; for (var k in o) r.push(k); r.join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [InlineData("Object.keys(Object.assign({}, o)).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [InlineData("Object.keys({ ...o }).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [InlineData("var { k5, ...rest } = o; Object.keys(rest).join(',')", "k1,k2,k3,k4,k6,k7,k8,k0")]
    [InlineData("JSON.stringify(o)", """{"k1":1,"k2":2,"k3":3,"k4":4,"k5":5,"k6":6,"k7":7,"k8":8,"k0":99}""")]
    public void EveryOwnKeyListingSeesTheReAddedKeyLast(string expression, string expected)
    {
        var engine = new Engine();
        var actual = engine.Evaluate($$"""
            var o = {};
            for (var i = 0; i < 9; i++) o['k' + i] = i;
            delete o.k0;
            o.k0 = 99;
            {{expression}};
            """).AsString();

        actual.Should().Be(expected);
    }

    [Fact]
    public void DefinePropertyAsTheReAddIsAlsoACreation()
    {
        var engine = new Engine();
        var actual = Keys(engine, """
            var o = {};
            for (var i = 0; i < 9; i++) o['k' + i] = i;
            delete o.k0;
            Object.defineProperty(o, 'k0', { value: 99, writable: true, enumerable: true, configurable: true });
            Object.keys(o).join(',');
            """);

        actual.Should().Be("k1,k2,k3,k4,k5,k6,k7,k8,k0");
    }

    [Fact]
    public void ReflectDeleteAndSetBehaveLikeTheOperators()
    {
        var engine = new Engine();
        var actual = Keys(engine, """
            var o = {};
            for (var i = 0; i < 9; i++) o['k' + i] = i;
            Reflect.deleteProperty(o, 'k0');
            Reflect.set(o, 'k0', 99);
            Object.keys(o).join(',');
            """);

        actual.Should().Be("k1,k2,k3,k4,k5,k6,k7,k8,k0");
    }

    /// <summary>
    /// Integer-like keys are listed first in ascending numeric order whatever happens to them, so the
    /// mixed case pins that the string half moves and the index half does not.
    /// </summary>
    [Fact]
    public void IndexKeysStayAscendingWhileStringKeysFollowCreationOrder()
    {
        var engine = new Engine();
        var actual = Keys(engine, """
            var o = {};
            o[2] = 'i2';
            o[0] = 'i0';
            for (var i = 0; i < 9; i++) o['s' + i] = i;
            delete o.s0;
            o.s0 = 99;
            Object.keys(o).join(',');
            """);

        actual.Should().Be("0,2,s1,s2,s3,s4,s5,s6,s7,s8,s0");
    }

    [Fact]
    public void AnArrayWithStringPropertiesFollowsTheSameRule()
    {
        var engine = new Engine();
        var actual = Keys(engine, """
            var a = [1, 2, 3];
            for (var i = 0; i < 10; i++) a['p' + i] = i;
            delete a.p0;
            a.p0 = 99;
            Object.keys(a).join(',');
            """);

        actual.Should().Be("0,1,2,p1,p2,p3,p4,p5,p6,p7,p8,p9,p0");
    }

    /// <summary>
    /// Symbol keys are their own list in <see cref="Object" />.getOwnPropertySymbols and carry the same
    /// chronological rule. The symbol store has no small-size backing to hide behind, so two keys are
    /// already enough.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(9)]
    [InlineData(12)]
    public void ADeletedSymbolThatIsAddedBackIsTheNewestSymbol(int n)
    {
        var engine = new Engine();
        var actual = Keys(engine, $$"""
            var o = {};
            var s = [];
            for (var i = 0; i < {{n}}; i++) { s.push(Symbol('s' + i)); o[s[i]] = i; }
            delete o[s[0]];
            o[s[0]] = 99;
            Object.getOwnPropertySymbols(o).map(String).join(',');
            """);

        var expected = new List<string>();
        for (var i = 1; i < n; i++)
        {
            expected.Add($"Symbol(s{i})");
        }
        expected.Add("Symbol(s0)");

        actual.Should().Be(string.Join(",", expected));
    }

    /// <summary>
    /// A built-in prototype starts in the shared built-in shape, where a string-key delete deopts into
    /// <see cref="PropertyDictionary"/> first. <c>Map.prototype</c> has enough members (13) to land in the
    /// hash backing rather than the list backing, so it is the shape path's witness for this bug.
    /// </summary>
    [Fact]
    public void ADeoptedBuiltinShapeFollowsTheSameRule()
    {
        var engine = new Engine();
        var before = Keys(engine, "Object.getOwnPropertyNames(Map.prototype).join(',')");
        before.Should().Contain("get");

        var after = Keys(engine, """
            delete Map.prototype.get;
            Map.prototype.get = 1;
            Object.getOwnPropertyNames(Map.prototype).join(',');
            """);

        var expected = string.Join(",", before.Split(',').Where(static k => k != "get").Concat(["get"]));
        after.Should().Be(expected);
    }

    [Fact]
    public void RepeatedDeleteAndReAddOfTheSameKeyKeepsItLast()
    {
        var engine = new Engine();
        var actual = Keys(engine, """
            var o = {};
            for (var i = 0; i < 12; i++) o['k' + i] = i;
            for (var r = 0; r < 200; r++) { delete o.k5; o.k5 = r; }
            Object.keys(o).join(',');
            """);

        actual.Should().Be("k0,k1,k2,k3,k4,k6,k7,k8,k9,k10,k11,k5");
    }

    /// <summary>
    /// A randomized cross-check against a model kept in script: 20,000 add/delete operations over a key
    /// space wide enough to cross the cutover, resize and compact many times. The model is the rule
    /// itself — a new name appends, assigning an existing one leaves it where it is, a delete drops it —
    /// so a mismatch means the store diverged from creation order somewhere in the churn rather than in
    /// any one hand-picked scenario. Deterministic seed, so a failure is reproducible.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RandomizedChurnKeepsTheStoreInCreationOrder(bool symbolKeys)
    {
        var subject = symbolKeys
            ? "var key = function (i) { return syms[i]; }; var list = function (o) { return Object.getOwnPropertySymbols(o).map(String).join(','); }; var name = function (i) { return String(syms[i]); };"
            : "var key = function (i) { return 'x' + i; }; var list = function (o) { return Object.keys(o).join(','); }; var name = function (i) { return 'x' + i; };";

        var engine = new Engine();
        var result = engine.Evaluate($$"""
            var syms = [];
            for (var i = 0; i < 60; i++) syms.push(Symbol('y' + i));
            {{subject}}

            var seed = 12345;
            function rnd(n) { seed = (seed * 1103515245 + 12345) & 0x7fffffff; return seed % n; }

            var o = {};
            var model = [];
            for (var step = 0; step < 20000; step++) {
                var i = rnd(60);
                if (rnd(2) === 0) {
                    if (model.indexOf(i) < 0) model.push(i);
                    o[key(i)] = step;
                } else {
                    var at = model.indexOf(i);
                    if (at >= 0) model.splice(at, 1);
                    delete o[key(i)];
                }
            }

            var want = model.map(name).join(',');
            list(o) === want ? 'OK:' + model.length : 'MISMATCH\n got  ' + list(o) + '\n want ' + want;
            """).AsString();

        result.Should().StartWith("OK:");
        // A churn that ended with an empty or barely-filled store would prove very little.
        int.Parse(result.Substring(3), System.Globalization.CultureInfo.InvariantCulture).Should().BeGreaterThan(9);
    }

    /// <summary>
    /// Insertion order is preserved by never reusing a removed entry's slot, so the entry array has to be
    /// reclaimed some other way: it is compacted when a resize would otherwise be needed. That makes the
    /// storage of a long add/delete churn bounded by the live key count rather than by the number of
    /// operations, which is the property that keeps the fix from being a leak.
    /// </summary>
    [Fact]
    public void ChurningAKeyDoesNotGrowTheEntryArrayWithoutBound()
    {
        var dictionary = new StringDictionarySlim<int>();
        for (var i = 0; i < 16; i++)
        {
            dictionary["k" + i] = i;
        }

        for (var r = 0; r < 100_000; r++)
        {
            dictionary.Remove("churn");
            dictionary["churn"] = r;
        }

        dictionary.Count.Should().Be(17);
        EntryCapacityOf(dictionary).Should().BeLessThan(128);
    }

    /// <summary>
    /// The same bound, but for churn that does not simply re-add the newest key: every round adds a fresh
    /// name and deletes one from the middle of the table, so no entry slot can be reclaimed on removal and
    /// only compaction keeps the array in check.
    /// </summary>
    [Fact]
    public void ChurningDistinctKeysDoesNotGrowTheEntryArrayWithoutBound()
    {
        var dictionary = new StringDictionarySlim<int>();
        for (var i = 0; i < 16; i++)
        {
            dictionary["k" + i] = i;
        }

        for (var r = 0; r < 100_000; r++)
        {
            dictionary["n" + r] = r;
            dictionary.Remove("k" + (r % 16));
            dictionary["k" + (r % 16)] = r;
            dictionary.Remove("n" + r);
        }

        dictionary.Count.Should().Be(16);
        EntryCapacityOf(dictionary).Should().BeLessThan(256);
    }

    [Fact]
    public void RemovalAndReAdditionKeepsTheCollectionEnumerableInInsertionOrder()
    {
        var dictionary = new StringDictionarySlim<int>();
        for (var i = 0; i < 20; i++)
        {
            dictionary["k" + i] = i;
        }

        dictionary.Remove("k0");
        dictionary.Remove("k7");
        dictionary["k7"] = 700;
        dictionary["fresh"] = 1;
        dictionary["k0"] = 0;

        var order = dictionary.Select(static pair => pair.Key.Name).ToList();

        var expected = new List<string>();
        for (var i = 1; i < 20; i++)
        {
            if (i != 7)
            {
                expected.Add("k" + i);
            }
        }
        expected.Add("k7");
        expected.Add("fresh");
        expected.Add("k0");

        order.Should().Equal(expected);
        dictionary.Count.Should().Be(21);
        dictionary["k7"].Should().Be(700);
    }

    private static int EntryCapacityOf<T>(StringDictionarySlim<T> dictionary)
    {
        var field = typeof(StringDictionarySlim<T>).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
        return ((Array) field!.GetValue(dictionary)!).Length;
    }
}
