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

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(7)]
    [TestCase(8)]  // last size backed by ListDictionary
    [TestCase(9)]  // first size backed by StringDictionarySlim
    [TestCase(10)]
    [TestCase(16)]
    [TestCase(40)]
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

    [TestCase(8)]
    [TestCase(9)]
    [TestCase(24)]
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
    [Test]
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

    [Test]
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
    [TestCase("Object.keys(o).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [TestCase("Object.values(o).join(',')", "1,2,3,4,5,6,7,8,99")]
    [TestCase("Object.entries(o).map(function (e) { return e[0]; }).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [TestCase("Object.getOwnPropertyNames(o).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [TestCase("Object.keys(Object.getOwnPropertyDescriptors(o)).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [TestCase("Reflect.ownKeys(o).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [TestCase("Reflect.ownKeys(new Proxy(o, {})).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [TestCase("var r = []; for (var k in o) r.push(k); r.join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [TestCase("Object.keys(Object.assign({}, o)).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [TestCase("Object.keys({ ...o }).join(',')", "k1,k2,k3,k4,k5,k6,k7,k8,k0")]
    [TestCase("var { k5, ...rest } = o; Object.keys(rest).join(',')", "k1,k2,k3,k4,k6,k7,k8,k0")]
    [TestCase("JSON.stringify(o)", """{"k1":1,"k2":2,"k3":3,"k4":4,"k5":5,"k6":6,"k7":7,"k8":8,"k0":99}""")]
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

    [Test]
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

    [Test]
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
    [Test]
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

    [Test]
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
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(9)]
    [TestCase(12)]
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
    [Test]
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

    [Test]
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
    [TestCase(false)]
    [TestCase(true)]
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
    [Test]
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
    [Test]
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

    [Test]
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

    /// <summary>
    /// A rotating key set — a fresh name in, the oldest one out, which is what an object used as a bounded
    /// cache does — is the shape the tombstones cost the most: every step left a hole below the high-water
    /// mark, so the table compacted every <c>capacity - live</c> adds forever and settled on four times its
    /// live key count. The entries now live in a circular window whose base advances when the oldest entry
    /// goes, so that shape reaches no compaction at all (#3315). What the window must not disturb is the
    /// one thing the tombstones exist for: enumeration order, which now has to survive the window wrapping
    /// the array.
    /// </summary>
    [Test]
    public void ARotatingKeySetKeepsCreationOrderAcrossAWrap()
    {
        var dictionary = new StringDictionarySlim<int>();
        var model = new List<string>();
        var next = 0;

        for (var i = 0; i < 16; i++)
        {
            dictionary["k" + i] = i;
            model.Add("k" + i);
        }

        for (var step = 0; step < 5_000; step++)
        {
            Churn(dictionary, model, ref next, dropOldest: true);

            if (step % 97 == 0)
            {
                NamesOf(dictionary).Should().Equal(model);
            }
        }

        NamesOf(dictionary).Should().Equal(model);
        dictionary.Count.Should().Be(16);
    }

    /// <summary>
    /// The same rotation, priced in slots: the window travels around the array instead of leaving a trail
    /// of tombstones behind it, so the table holds the live key count rounded up to a power of two — 32
    /// for 16 live keys plus the one that is transiently added before the oldest goes. Compaction settled
    /// the same table on 64.
    /// </summary>
    [Test]
    public void ARotatingKeySetSettlesAtItsLiveCountRatherThanFourTimesIt()
    {
        var dictionary = new StringDictionarySlim<int>();
        var model = new List<string>();
        var next = 0;

        for (var i = 0; i < 16; i++)
        {
            dictionary["k" + i] = i;
            model.Add("k" + i);
        }

        for (var step = 0; step < 10_000; step++)
        {
            Churn(dictionary, model, ref next, dropOldest: true);
        }

        dictionary.Count.Should().Be(16);
        EntryCapacityOf(dictionary).Should().Be(32);
    }

    /// <summary>
    /// The window is only free while the churn touches its ends. Here the oldest entry stays put and the
    /// keys above it churn, so the base cannot advance and the tombstones pile up exactly as they did
    /// before: this is the degradation path, and what it must do is degrade to the old behaviour rather
    /// than to something worse. The table still compacts, still stays bounded, and still enumerates in
    /// creation order.
    /// </summary>
    [Test]
    public void ChurnBehindAPinnedOldestKeyStillCompactsAndStaysBounded()
    {
        var dictionary = new StringDictionarySlim<int>();
        var model = new List<string>();
        var next = 0;

        for (var i = 0; i < 16; i++)
        {
            dictionary["k" + i] = i;
            model.Add("k" + i);
        }

        for (var step = 0; step < 5_000; step++)
        {
            // Never the oldest: every removal is from the middle, so every one leaves a tombstone.
            Churn(dictionary, model, ref next, dropOldest: false);

            if (step % 97 == 0)
            {
                NamesOf(dictionary).Should().Equal(model);
            }
        }

        NamesOf(dictionary).Should().Equal(model);
        model[0].Should().Be("k0");
        dictionary.Count.Should().Be(16);
        EntryCapacityOf(dictionary).Should().Be(64);
    }

    /// <summary>
    /// The novel state: a window that wraps the end of the array while it is resized. Both halves of
    /// <c>Resize</c> have to be reached with the window in that state — the growth that unwraps it into a
    /// doubled array, and the in-place compaction that keeps the base where it is and squeezes the
    /// survivors down towards it — and neither may reorder a key. Each round rotates far enough to carry
    /// the base past the end of the array, then pins the oldest entry so the tombstones fill the wrapped
    /// window.
    /// </summary>
    [Test]
    public void AWrappedWindowIsGrownAndCompactedWithoutLosingTheOrder()
    {
        var dictionary = new StringDictionarySlim<int>();
        var model = new List<string>();
        var next = 0;

        for (var i = 0; i < 16; i++)
        {
            dictionary["k" + i] = i;
            model.Add("k" + i);
        }

        var grewWhileWrapped = 0;
        var compactedInPlaceWhileWrapped = 0;

        for (var round = 0; round < 3; round++)
        {
            // The base advances one slot per rotation, so this takes at most a capacity's worth of them.
            for (var step = 0; step < 1_000 && !WindowIsWrapped(dictionary); step++)
            {
                Churn(dictionary, model, ref next, dropOldest: true);
            }

            WindowIsWrapped(dictionary).Should().BeTrue("rotating past the end of the array is what wraps the window");

            for (var step = 0; step < 400; step++)
            {
                var wrapped = WindowIsWrapped(dictionary);
                var capacity = EntryCapacityOf(dictionary);
                var width = WindowWidthOf(dictionary);

                Churn(dictionary, model, ref next, dropOldest: false);

                if (WindowWidthOf(dictionary) != width + 1 && wrapped)
                {
                    // The window did not simply grow by the added entry, so Resize ran.
                    if (EntryCapacityOf(dictionary) == capacity)
                    {
                        compactedInPlaceWhileWrapped++;
                    }
                    else
                    {
                        grewWhileWrapped++;
                    }
                }

                NamesOf(dictionary).Should().Equal(model);
            }
        }

        grewWhileWrapped.Should().BeGreaterThan(0);
        compactedInPlaceWhileWrapped.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// The other way out of a wrapped window: a table that rotated for a while and then only grew. There is
    /// nothing to compact, so the survivors are copied into the doubled array in two runs — the tail of the
    /// old array first, then the head — and the window starts over at slot 0. Getting those two runs the
    /// wrong way round would reverse the object's keys around the wrap point and nothing else would notice.
    /// </summary>
    [Test]
    public void AWrappedWindowWithNothingToCompactIsUnwrappedByGrowing()
    {
        var dictionary = new StringDictionarySlim<int>();
        var model = new List<string>();
        var next = 0;

        for (var i = 0; i < 16; i++)
        {
            dictionary["k" + i] = i;
            model.Add("k" + i);
        }

        for (var step = 0; step < 1_000 && !WindowIsWrapped(dictionary); step++)
        {
            Churn(dictionary, model, ref next, dropOldest: true);
        }

        WindowIsWrapped(dictionary).Should().BeTrue();
        WindowWidthOf(dictionary).Should().Be(dictionary.Count, "a rotation leaves no tombstones behind it");

        var capacity = EntryCapacityOf(dictionary);
        while (EntryCapacityOf(dictionary) == capacity)
        {
            var name = "g" + next++;
            dictionary[name] = next;
            model.Add(name);
        }

        EntryCapacityOf(dictionary).Should().Be(capacity * 2);
        WindowIsWrapped(dictionary).Should().BeFalse("growing is where a wrapped window unwraps");
        NamesOf(dictionary).Should().Equal(model);
    }

    /// <summary>
    /// The pooled reset, which a function environment does on every reuse: it keeps the grown arrays and
    /// has to put the window back at the base of them, wrapped or not, or the next refill would enumerate
    /// from wherever the last one happened to stop.
    /// </summary>
    [Test]
    public void ClearPreservingCapacityResetsAWrappedWindow()
    {
        var dictionary = new StringDictionarySlim<int>();
        var model = new List<string>();
        var next = 0;

        for (var i = 0; i < 16; i++)
        {
            dictionary["k" + i] = i;
            model.Add("k" + i);
        }

        for (var step = 0; step < 200 && !WindowIsWrapped(dictionary); step++)
        {
            Churn(dictionary, model, ref next, dropOldest: true);
        }

        WindowIsWrapped(dictionary).Should().BeTrue();
        var capacity = EntryCapacityOf(dictionary);

        dictionary.ClearPreservingCapacity();

        dictionary.Count.Should().Be(0);
        NamesOf(dictionary).Should().BeEmpty();
        EntryCapacityOf(dictionary).Should().Be(capacity);

        var refilled = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            dictionary["r" + i] = i;
            refilled.Add("r" + i);
        }

        NamesOf(dictionary).Should().Equal(refilled);
        EntryCapacityOf(dictionary).Should().Be(capacity);
    }

    /// <summary>
    /// A randomized cross-check of both stores against the same model, at the collection level rather than
    /// through the engine: 50,000 operations over a key space narrow enough that the window wraps, fills
    /// and compacts many times, with every live key looked up again at each checkpoint so that a wrap
    /// cannot quietly strand an entry its bucket chain still points at. Deterministic seeds, so a failure
    /// is reproducible. The symbol store is the same generic class as the object's, so string keys in it
    /// exercise the identical window arithmetic.
    /// </summary>
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void RandomizedChurnKeepsBothStoresInCreationOrder(int seed)
    {
        var random = new Random(seed);
        var strings = new StringDictionarySlim<int>();
        var symbols = new DictionarySlim<string, int>();
        var model = new List<string>();

        for (var step = 0; step < 50_000; step++)
        {
            var name = "x" + random.Next(24);
            if (random.Next(2) == 0)
            {
                if (!model.Contains(name))
                {
                    model.Add(name);
                }

                strings[name] = step;
                symbols[name] = step;
            }
            else
            {
                model.Remove(name);
                strings.Remove(name);
                symbols.Remove(name);
            }

            if (step % 499 == 0)
            {
                AssertBothStoresMatch(strings, symbols, model);
            }
        }

        AssertBothStoresMatch(strings, symbols, model);
    }

    /// <summary>
    /// The window's own arithmetic, checked after every single operation rather than only through what an
    /// enumeration can see. The add path is one comparison — <c>_lastIndex == _limit</c> — so everything
    /// that keeps it from writing over a live key is in that bound: it is the capacity while the window
    /// runs to the end of the array and the base while the window wraps below it, it is never below the
    /// top, the base is a live entry whenever the table is not empty, and an emptied table is back at slot
    /// 0 so the next refill starts there. The mix keeps all three removal positions and both resets in
    /// play, because the states this walks through are the ones a prefix-shaped table never reached.
    /// </summary>
    [TestCase(11)]
    [TestCase(12)]
    public void TheWindowBoundStaysTheOneAnAddMayTest(int seed)
    {
        var random = new Random(seed);
        var dictionary = new StringDictionarySlim<int>();
        var model = new List<string>();
        var next = 0;

        for (var step = 0; step < 10_000; step++)
        {
            switch (random.Next(7))
            {
                case 0 when model.Count > 0:
                    // The oldest: the base advances past the slot and any tombstones behind it.
                    Drop(dictionary, model, model[0]);
                    break;
                case 1 when model.Count > 0:
                    // The newest: the top walks back.
                    Drop(dictionary, model, model[model.Count - 1]);
                    break;
                case 2 when model.Count > 0:
                    // The middle: a tombstone only a resize reclaims.
                    Drop(dictionary, model, model[random.Next(model.Count)]);
                    break;
                case 3 when model.Count > 8:
                    // A drain, which has to retire the window along with the last live entry.
                    while (model.Count > 0)
                    {
                        Drop(dictionary, model, model[0]);
                    }

                    break;
                case 4 when model.Count > 8:
                    // The pooled reset, from wherever the window happens to have travelled to.
                    dictionary.ClearPreservingCapacity();
                    model.Clear();
                    break;
                default:
                    var name = "n" + next++;
                    dictionary[name] = next;
                    model.Add(name);
                    break;
            }

            AssertWindowInvariants(dictionary);
        }

        NamesOf(dictionary).Should().Equal(model);
    }

    private static void Drop(StringDictionarySlim<int> dictionary, List<string> model, string name)
    {
        dictionary.Remove(name).Should().BeTrue();
        model.Remove(name);
    }

    private static void AssertWindowInvariants<T>(StringDictionarySlim<T> dictionary)
    {
        var capacity = EntryCapacityOf(dictionary);
        var first = PrivateInt(dictionary, "_firstIndex");
        var last = PrivateInt(dictionary, "_lastIndex");
        var limit = PrivateInt(dictionary, "_limit");

        if (last > limit || limit != capacity && limit != first || (uint) first >= (uint) capacity)
        {
            Assert.Fail(
                $"window [{first}, {last}) bounded at {limit} over {capacity} slots: the bound is the "
                + "capacity while the window runs to the end of the array and the base while it wraps "
                + "below it, and the top may never pass it");
        }

        if (dictionary.Count == 0)
        {
            if (first != last)
            {
                Assert.Fail($"an emptied table closes its window, but this one is [{first}, {last})");
            }

            return;
        }

        if (WindowWidthOf(dictionary) < dictionary.Count)
        {
            Assert.Fail($"the window is {WindowWidthOf(dictionary)} slots wide but holds {dictionary.Count} live entries");
        }

        if (NextOfSlot(dictionary, first) == -2)
        {
            Assert.Fail(
                "the base came to rest on a tombstone, so the next removal of the oldest entry would no "
                + "longer recognize itself and one delete from the middle would disarm the fast path");
        }
    }

    /// <summary>The chain link of a slot, which is <c>-2</c> exactly when a removal left a tombstone.</summary>
    private static int NextOfSlot<T>(StringDictionarySlim<T> dictionary, int index)
    {
        var entries = (Array) typeof(StringDictionarySlim<T>)
            .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(dictionary)!;

        var entry = entries.GetValue(index)!;
        return (int) entry.GetType().GetField("next", BindingFlags.Instance | BindingFlags.Public)!.GetValue(entry)!;
    }

    private static void AssertBothStoresMatch(StringDictionarySlim<int> strings, DictionarySlim<string, int> symbols, List<string> model)
    {
        NamesOf(strings).Should().Equal(model);
        symbols.Select(static pair => pair.Key).ToList().Should().Equal(model);
        strings.Count.Should().Be(model.Count);
        symbols.Count.Should().Be(model.Count);

        foreach (var name in model)
        {
            strings.TryGetValue(name, out _).Should().BeTrue();
            symbols.TryGetValue(name, out _).Should().BeTrue();
        }
    }

    /// <summary>
    /// The same rotation through the engine, which is where it actually happens: a bounded cache built out
    /// of a plain object, listed with the built-in every embedder calls. The name pool is narrower than the
    /// churn, so names come back — and a name that comes back is a new property, which belongs last.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void RotatingPropertiesKeepCreationOrderAcrossAWrap(bool symbolKeys)
    {
        var subject = symbolKeys
            ? "var key = function (i) { return syms[i]; }; var list = function (o) { return Object.getOwnPropertySymbols(o).map(String).join(','); }; var name = function (i) { return String(syms[i]); };"
            : "var key = function (i) { return 'x' + i; }; var list = function (o) { return Object.keys(o).join(','); }; var name = function (i) { return 'x' + i; };";

        var engine = new Engine();
        var result = engine.Evaluate($$"""
            var syms = [];
            for (var i = 0; i < 60; i++) syms.push(Symbol('y' + i));
            {{subject}}

            var o = {};
            var model = [];
            for (var i = 0; i < 20; i++) { o[key(i)] = i; model.push(i); }

            for (var n = 0; n < 600; n++) {
                var i = (20 + n) % 60;
                var at = model.indexOf(i);
                if (at >= 0) { model.splice(at, 1); delete o[key(i)]; }
                o[key(i)] = n;
                model.push(i);
                delete o[key(model.shift())];
            }

            var want = model.map(name).join(',');
            list(o) === want ? 'OK:' + model.length : 'MISMATCH\n got  ' + list(o) + '\n want ' + want;
            """).AsString();

        result.Should().Be("OK:20");
    }

    private static void Churn(StringDictionarySlim<int> dictionary, List<string> model, ref int next, bool dropOldest)
    {
        var name = "n" + next++;
        dictionary[name] = next;
        model.Add(name);

        var victim = dropOldest ? model[0] : model[model.Count / 2];
        dictionary.Remove(victim).Should().BeTrue();
        model.Remove(victim);
    }

    private static List<string> NamesOf<T>(StringDictionarySlim<T> dictionary) =>
        dictionary.Select(static pair => pair.Key.Name).ToList();

    private static int EntryCapacityOf<T>(StringDictionarySlim<T> dictionary)
    {
        var field = typeof(StringDictionarySlim<T>).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
        return ((Array) field!.GetValue(dictionary)!).Length;
    }

    /// <summary>The window wraps when it runs off the end of the array and continues at slot 0.</summary>
    private static bool WindowIsWrapped<T>(StringDictionarySlim<T> dictionary) =>
        dictionary.Count > 0
        && PrivateInt(dictionary, "_firstIndex") + WindowWidthOf(dictionary) > EntryCapacityOf(dictionary);

    /// <summary>
    /// The width of the window in slots — live entries plus the tombstones between them. It is not stored:
    /// the distance from the base to the top is it, and the one distance that reads as zero is a full
    /// window, since an empty one leaves no live entries to count.
    /// </summary>
    private static int WindowWidthOf<T>(StringDictionarySlim<T> dictionary)
    {
        if (dictionary.Count == 0)
        {
            return 0;
        }

        var capacity = EntryCapacityOf(dictionary);
        var width = (PrivateInt(dictionary, "_lastIndex") - PrivateInt(dictionary, "_firstIndex")) & (capacity - 1);
        return width == 0 ? capacity : width;
    }

    private static int PrivateInt<T>(StringDictionarySlim<T> dictionary, string name)
    {
        var field = typeof(StringDictionarySlim<T>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null)
        {
            Assert.Fail($"StringDictionarySlim<T> has no {name}: the circular window these tests are about does not exist in this build.");
        }

        return (int) field!.GetValue(dictionary)!;
    }
}
