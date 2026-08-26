using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Map and Set are specified over a List of entries in which a deleted entry becomes <c>~empty~</c>
/// <em>in place</em> rather than being removed — https://tc39.es/ecma262/#sec-set.prototype.delete and
/// https://tc39.es/ecma262/#sec-map.prototype.delete. Every traversal the spec defines walks that List
/// by index while user code is free to mutate the collection between two steps: <c>forEach</c>
/// (https://tc39.es/ecma262/#sec-set.prototype.foreach), the iterators
/// (https://tc39.es/ecma262/#sec-createsetiterator, https://tc39.es/ecma262/#sec-createmapiterator) and
/// the index-walking half of <c>difference</c>, <c>intersection</c>, <c>isDisjointFrom</c> and
/// <c>isSubsetOf</c>. The tombstone is what makes those walks coherent, and these tests pin the
/// behaviour that depends on it from outside test262.
/// </summary>
public class KeyedCollectionMutationTests
{
    private static string Run(string script) => new Engine().Evaluate(script).AsString();

    [Test]
    public void SetForEachRevisitsAValueDeletedAndReAddedWhileVisiting()
    {
        const string Script = """
            var s = new Set([1, 2, 3]);
            var seen = [];
            var once = true;
            s.forEach(function (v) {
              seen.push(v);
              if (v === 2 && once) { once = false; s.delete(2); s.add(2); }
            });
            seen.join(',') + '|' + [...s].join(',');
            """;

        Run(Script).Should().Be("1,2,3,2|1,3,2");
    }

    [Test]
    public void SetForEachDoesNotVisitAValueDeletedBeforeItsTurn()
    {
        const string Script = """
            var s = new Set([1, 2, 3, 4]);
            var seen = [];
            s.forEach(function (v) { seen.push(v); if (v === 1) { s.delete(3); } });
            seen.join(',');
            """;

        Run(Script).Should().Be("1,2,4");
    }

    [Test]
    public void SetForEachKeepsItsPlaceWhenAnAlreadyVisitedValueIsDeleted()
    {
        const string Script = """
            var s = new Set([1, 2, 3, 4]);
            var seen = [];
            s.forEach(function (v) { seen.push(v); if (v === 1) { s.delete(1); } });
            seen.join(',');
            """;

        Run(Script).Should().Be("1,2,3,4");
    }

    [Test]
    public void SetIteratorResumesAtTheRightEntryAfterDeletesAndAdds()
    {
        const string Script = """
            var s = new Set([1, 2, 3]);
            var it = s.values();
            var out = [it.next().value];
            s.delete(1);
            s.delete(2);
            out.push(it.next().value);
            s.add(4);
            out.push(it.next().value);
            out.push(String(it.next().done));
            out.join(',');
            """;

        Run(Script).Should().Be("1,3,4,true");
    }

    /// <summary>
    /// The entry List is reclaimed once at least half of its slots are deleted, which moves every live
    /// entry. A suspended iterator has to survive that, so it resumes by the entry's own sequence
    /// number rather than by a raw slot index.
    /// </summary>
    [Test]
    public void SetIteratorSurvivesAnEntryListCompaction()
    {
        const string Script = """
            var s = new Set();
            for (var i = 0; i < 100; i++) { s.add(i); }
            var it = s.values();
            var out = [it.next().value];
            for (var i = 1; i < 100; i += 2) { s.delete(i); }
            for (var i = 100; i < 300; i++) { s.add(i); }
            out.push(it.next().value);
            out.push(it.next().value);
            out.push(it.next().value);
            out.join(',');
            """;

        Run(Script).Should().Be("0,2,4,6");
    }

    [Test]
    public void SetIteratorSeesEntriesAddedAfterAClear()
    {
        const string Script = """
            var s = new Set([1, 2, 3]);
            var it = s.values();
            var out = [it.next().value];
            s.clear();
            s.add(7);
            s.add(8);
            out.push(it.next().value);
            out.push(it.next().value);
            out.push(String(it.next().done));
            out.join(',');
            """;

        Run(Script).Should().Be("1,7,8,true");
    }

    [Test]
    public void AnExhaustedSetIteratorStaysDone()
    {
        const string Script = """
            var s = new Set([1]);
            var it = s.values();
            it.next();
            var first = it.next().done;
            s.add(2);
            String(first) + ',' + String(it.next().done);
            """;

        Run(Script).Should().Be("true,true");
    }

    /// <summary>
    /// The receiver's <c>has</c> callback removes and re-adds an element that has already been visited,
    /// which the spec says makes that element visible a second time, at its new position — and the walk
    /// must still reach the elements after it.
    /// </summary>
    [Test]
    public void IntersectionRevisitsAnElementItsHasCallbackReAdded()
    {
        const string Script = """
            var seen = [];
            var setLike = {
              size: 100,
              has: function (v) {
                if (v === 2 && seen.indexOf(v) < 0) { s.delete(v); s.add(v); }
                seen.push(v);
                return true;
              },
              keys: function () { throw new Error('unexpected keys'); }
            };
            var s = new Set([1, 2, 3]);
            [...s.intersection(setLike)].join(',') + '|' + seen.join(',');
            """;

        Run(Script).Should().Be("1,2,3|1,2,3,2");
    }

    /// <summary>
    /// Each call to <c>has</c> deletes the element it was handed and appends a new one; the walk has to
    /// keep its place across both, so every element ever in the set is visited exactly once.
    /// </summary>
    [Test]
    public void IsSubsetOfVisitsEveryElementWhenHasDeletesTheCurrentOne()
    {
        const string Script = """
            var s = new Set([1]);
            var seen = [];
            var newKeys = [2, 3, 4, 5];
            var setLike = {
              size: 100,
              has: function (v) {
                seen.push(v);
                s.delete(v);
                if (newKeys.length) { s.add(newKeys.shift()); }
                return true;
              },
              keys: function () { throw new Error('unexpected keys'); }
            };
            String(s.isSubsetOf(setLike)) + '|' + seen.join(',') + '|' + s.size;
            """;

        Run(Script).Should().Be("true|1,2,3,4,5|0");
    }

    /// <summary>
    /// A delete followed by an add moves the added element to the end, and every Set method has to
    /// report that order. The combining methods used to answer from an unordered hash set, whose
    /// enumeration reuses the slot a delete freed, so the re-added element came back first.
    /// </summary>
    [Test]
    public void SetMethodsReportInsertionOrderAfterADeleteAndAnAdd()
    {
        const string Script = """
            var s = new Set([1, 2, 3]);
            s.delete(1);
            s.add(4);
            [
              [...s].join(''),
              [...s.intersection(new Set([2, 3, 4]))].join(''),
              [...s.union(new Set([9]))].join(''),
              [...s.symmetricDifference(new Set([9]))].join(''),
              [...s.difference(new Set([9]))].join(''),
              [...s.difference(new Set([3]))].join('')
            ].join('|');
            """;

        Run(Script).Should().Be("234|234|2349|2349|234|24");
    }

    [Test]
    public void MapForEachRevisitsAKeyDeletedAndReAddedWhileVisiting()
    {
        const string Script = """
            var m = new Map([[1, 'a'], [2, 'b'], [3, 'c']]);
            var seen = [];
            var once = true;
            m.forEach(function (v, k) {
              seen.push(k);
              if (k === 2 && once) { once = false; m.delete(2); m.set(2, 'B'); }
            });
            seen.join(',') + '|' + [...m.keys()].join(',');
            """;

        Run(Script).Should().Be("1,2,3,2|1,3,2");
    }

    [Test]
    public void MapForEachKeepsItsPlaceWhenAnAlreadyVisitedKeyIsDeleted()
    {
        const string Script = """
            var m = new Map([[1, 'a'], [2, 'b'], [3, 'c'], [4, 'd']]);
            var seen = [];
            m.forEach(function (v, k) { seen.push(k + '=' + v); if (k === 1) { m.delete(1); } });
            seen.join(',');
            """;

        Run(Script).Should().Be("1=a,2=b,3=c,4=d");
    }

    [Test]
    public void MapIteratorResumesAtTheRightEntryAfterDeletesAndAdds()
    {
        const string Script = """
            var m = new Map([[1, 'a'], [2, 'b'], [3, 'c']]);
            var it = m.entries();
            var out = [it.next().value[0]];
            m.delete(1);
            m.delete(2);
            out.push(it.next().value[0]);
            m.set(4, 'd');
            out.push(it.next().value[0]);
            out.push(String(it.next().done));
            out.join(',');
            """;

        Run(Script).Should().Be("1,3,4,true");
    }

    [Test]
    public void MapIteratorSurvivesAnEntryListCompaction()
    {
        const string Script = """
            var m = new Map();
            for (var i = 0; i < 100; i++) { m.set(i, i); }
            var it = m.keys();
            var out = [it.next().value];
            for (var i = 1; i < 100; i += 2) { m.delete(i); }
            for (var i = 100; i < 300; i++) { m.set(i, i); }
            out.push(it.next().value);
            out.push(it.next().value);
            out.join(',');
            """;

        Run(Script).Should().Be("0,2,4");
    }

    [Test]
    public void SettingAnExistingMapKeyLeavesItWhereItIs()
    {
        const string Script = """
            var m = new Map([[1, 'a'], [2, 'b']]);
            m.set(1, 'z');
            [...m.keys()].join(',') + '|' + [...m.values()].join(',');
            """;

        Run(Script).Should().Be("1,2|z,b");
    }

    /// <summary>
    /// A tombstone that is never reclaimed turns add/delete churn into unbounded growth, so the entry
    /// List has to stay within a constant factor of the live count. Deleting the last entry drops its
    /// slot outright, which is what keeps this shape flat.
    /// </summary>
    [Test]
    public void AddDeleteChurnDoesNotGrowTheSetEntryList()
    {
        var engine = new Engine();
        var set = (JsSet) engine.Evaluate("var s = new Set([0]); for (var i = 1; i <= 20000; i++) { s.add(i); s.delete(i); } s;");

        set.Size.Should().Be(1);
        set._data.SlotCount.Should().BeLessThanOrEqualTo(4);
    }

    /// <summary>
    /// The sliding-window shape deletes from the front rather than the tail, so the slot list is held
    /// down by compaction instead: it may never grow while more than half of it is live.
    /// </summary>
    [Test]
    public void ASlidingWindowDoesNotGrowTheMapEntryList()
    {
        var engine = new Engine();
        var map = (JsMap) engine.Evaluate("var m = new Map(); for (var i = 0; i < 20000; i++) { m.set(i, i); if (i >= 100) { m.delete(i - 100); } } m;");

        map.Size.Should().Be(100);
        map._data.SlotCount.Should().BeLessThanOrEqualTo(512);
    }
}
