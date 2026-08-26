namespace Jint.Tests.Runtime;

/// <summary>
/// Pins for-in enumeration semantics against the allocation-reduction work in the for-in lane:
/// shape-level cached key strings, the pooled loop-variable Reference, and the per-statement pooled
/// <c>ForInIterator</c>. Each behaviour below is a semantic invariant the optimizations must preserve —
/// enumeration order, prototype-chain shadowing, delete/add-during-iteration, proxy trap counts, and
/// (crucially for the pooled iterator) that two simultaneous enumerations of the same object never
/// share live state.
/// </summary>
public class ForInEnumerationTests
{
    [Test]
    public void EnumeratesOwnKeysInInsertionOrder()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var six = { a: 1, b: 2, c: 3, d: 4, e: 5, f: 6 };
            var out = [];
            for (var k in six) { out.push(k); }
            out.join(',');
            """).AsString();

        result.Should().Be("a,b,c,d,e,f");
    }

    [Test]
    public void HasOwnPropertyGuardLoopCountsOnlyOwnKeys()
    {
        // The census idiom: the guard filters inherited keys, and the loop must produce the same count
        // across repeated entries (each entry reuses the pooled iterator and the cached key strings).
        var engine = new Engine();
        var result = engine.Evaluate("""
            var six = { a: 1, b: 2, c: 3, d: 4, e: 5, f: 6 };
            var s = 0;
            for (var i = 0; i < 100; i++) {
                for (var k in six) { if (six.hasOwnProperty(k)) s++; }
            }
            s;
            """).AsNumber();

        result.Should().Be(600);
    }

    [Test]
    public void ShadowingOverTwoLevelChain()
    {
        // A key present (enumerable) on a shallower level suppresses the same key on a deeper level;
        // deeper-only keys still appear, after the shallower keys.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var proto = { a: 1, x: 10 };
            var child = Object.create(proto);
            child.a = 2; // shadows proto.a
            child.y = 20;
            var out = [];
            for (var k in child) { out.push(k + '=' + child[k]); }
            out.join(',');
            """).AsString();

        result.Should().Be("a=2,y=20,x=10");
    }

    [Test]
    public void NonEnumerableShallowerKeyStillShadowsDeeperEnumerableKey()
    {
        // A present-but-non-enumerable own key on a shallower level must still shadow a deeper
        // enumerable key of the same name (it never yields, but it does suppress).
        var engine = new Engine();
        var result = engine.Evaluate("""
            var proto = { a: 1 };
            var child = Object.create(proto);
            Object.defineProperty(child, 'a', { value: 2, enumerable: false });
            var out = [];
            for (var k in child) { out.push(k); }
            out.join(',');
            """).AsString();

        result.Should().Be("");
    }

    [Test]
    public void DeleteDuringIterationSkipsNotYetVisitedKey()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var o = { a: 1, b: 2, c: 3 };
            var out = [];
            for (var k in o) { out.push(k); if (k === 'a') { delete o.c; } }
            out.join(',');
            """).AsString();

        result.Should().Be("a,b");
    }

    [Test]
    public void VisitedShallowerKeyDeletedMidLoopStillShadowsDeeperKey()
    {
        // Regression: a shallower own key that was already visited and is then deleted must still
        // shadow the same-named enumerable key on a deeper level — "each key visited at most once".
        // A live re-probe of the shadow set would see the key as Missing and wrongly re-enumerate it.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var proto = { a: 1 };
            var o = Object.create(proto);
            o.a = 2; // shadows proto.a, visited first
            o.b = 3;
            var out = [];
            for (var k in o) { out.push(k); if (k === 'b') { delete o.a; } }
            out.join(',');
            """).AsString();

        result.Should().Be("a,b");
    }

    [Test]
    public void VisitedKeyDeletedMidLoopShadowsAcrossThreeLevels()
    {
        // Same invariant across a three-level chain: the deleted-after-visit own key must not
        // resurface from a grandparent that carries an enumerable key of the same name.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var grand = { x: 1 };
            var proto = Object.create(grand); proto.y = 1;
            var o = Object.create(proto); o.x = 2; o.z = 3;
            var out = [];
            for (var k in o) { out.push(k); if (k === 'z') { delete o.x; } }
            out.join(',');
            """).AsString();

        result.Should().Be("x,z,y");
    }

    [Test]
    public void PooledIteratorReuseKeepsShadowingInheritedEnumerableKey()
    {
        // Regression: the pooled ForInIterator must reset its shadow-set sentinel to null (not merely
        // clear it) between reuses. An own key shadowing an enumerable INHERITED key must keep
        // shadowing on the second and later runs of the same for-in statement — otherwise the
        // inherited key double-enumerates from the second iteration onward.
        var engine = new Engine();
        var result = engine.Evaluate("""
            function Base() {}
            Base.prototype.type = 'base'; // enumerable inherited
            var out = [];
            for (var i = 0; i < 3; i++) {
                var o = new Base();
                o.type = 'x'; // own, shadows inherited 'type'
                o.id = 1;
                var ks = [];
                for (var k in o) { ks.push(k); }
                out.push(ks.join(','));
            }
            out.join(' | ');
            """).AsString();

        result.Should().Be("type,id | type,id | type,id");
    }

    [Test]
    public void AddDuringIterationDoesNotYieldNewKey()
    {
        // Spec permits either; this pins Jint's current behaviour (level keys are snapshotted at entry).
        var engine = new Engine();
        var result = engine.Evaluate("""
            var o = { a: 1 };
            var out = [];
            for (var k in o) { out.push(k); if (k === 'a') { o.z = 99; } }
            out.join(',');
            """).AsString();

        result.Should().Be("a");
    }

    [Test]
    public void ReAddingADeletedKeyDuringIterationKeepsItInSnapshot()
    {
        // Deleting then re-adding a key that hasn't been visited yet: it was in the entry snapshot, so it
        // is re-probed as present when its turn comes and still yields.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var o = { a: 1, b: 2 };
            var out = [];
            for (var k in o) { out.push(k); if (k === 'a') { delete o.b; o.b = 3; } }
            out.join(',');
            """).AsString();

        result.Should().Be("a,b");
    }

    [Test]
    public void ProxyOwnKeysAndDescriptorTrapCountsUnchanged()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var calls = [];
            var p = new Proxy({ a: 1, b: 2 }, {
                ownKeys: function (t) { calls.push('ownKeys'); return Reflect.ownKeys(t); },
                getOwnPropertyDescriptor: function (t, k) { calls.push('gopd:' + k); return Reflect.getOwnPropertyDescriptor(t, k); }
            });
            var out = [];
            for (var k in p) { out.push(k); }
            JSON.stringify([out, calls]);
            """).AsString();

        result.Should().Be("[[\"a\",\"b\"],[\"ownKeys\",\"gopd:a\",\"gopd:b\"]]");
    }

    [Test]
    public void TwoNestedForInOverSameObjectSimultaneously()
    {
        // Pooling must not share live iterator state: the inner loop runs a full enumeration on each
        // outer step, and the outer must continue from where it left off.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var six = { a: 1, b: 2, c: 3, d: 4, e: 5, f: 6 };
            var pairs = [];
            for (var k in six) { for (var j in six) { pairs.push(k + j); } }
            [pairs.length, pairs[0], pairs[7], pairs[35]].join(',');
            """).AsString();

        // 6 * 6 = 36 pairs; pairs[0]=aa, pairs[7]=bb, pairs[35]=ff
        result.Should().Be("36,aa,bb,ff");
    }

    [Test]
    public void RecursiveForInOfSameStatementKeepsOuterState()
    {
        // The same for-in statement re-entered recursively must allocate/keep independent state per level.
        var engine = new Engine();
        var result = engine.Evaluate("""
            function rec(o, depth) {
                var out = [];
                for (var k in o) { out.push(k); if (depth > 0) { out = out.concat(rec(o, depth - 1)); } }
                return out;
            }
            rec({ p: 1, q: 2 }, 1).join(',');
            """).AsString();

        // p -> [p,q]; q -> [p,q]  =>  p,p,q,q,p,q
        result.Should().Be("p,p,q,q,p,q");
    }

    [Test]
    public void InterleavedGeneratorsOverSameObjectDoNotShareState()
    {
        // Generators suspend across the for-in body; the iterator lives in suspend data and must never be
        // pooled/shared. Two generator instances over the same object produce independent sequences.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var obj = { a: 1, b: 2, c: 3 };
            function* keys(o) { for (var k in o) { yield k; } }
            var g1 = keys(obj), g2 = keys(obj);
            var seq = [];
            seq.push(g1.next().value);
            seq.push(g2.next().value);
            seq.push(g1.next().value);
            seq.push(g2.next().value);
            seq.push(g1.next().value);
            seq.push(g2.next().value);
            seq.push(g1.next().done);
            seq.join(',');
            """).AsString();

        // g1/g2 independently yield a,b,c; g1's 4th next() is done => the JS boolean true joins as "true".
        result.Should().Be("a,a,b,b,c,c,true");
    }

    [Test]
    public void BreakThenReuseResetsPooledIterator()
    {
        // Breaking out early parks a partially-advanced iterator; the next entry must reset it.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var obj = { a: 1, b: 2, c: 3 };
            var rounds = [];
            for (var r = 0; r < 3; r++) {
                var got = [];
                for (var k in obj) { got.push(k); if (k === 'b') break; }
                rounds.push(got.join(''));
            }
            rounds.join(',');
            """).AsString();

        result.Should().Be("ab,ab,ab");
    }

    [Test]
    public void ForInOverDictionaryModeObjectWithIntegerKeys()
    {
        // Integer-index-like keys enumerate first in ascending numeric order, then string keys in
        // insertion order (the shape memo declines integer keys, so this routes through the dictionary
        // path's numeric sort).
        var engine = new Engine();
        var result = engine.Evaluate("""
            var o = {};
            for (var i = 0; i < 5; i++) { o['key' + i] = i; }
            o['2'] = 1; o['10'] = 1; o['1'] = 1;
            var out = [];
            for (var k in o) { out.push(k); }
            out.join(',');
            """).AsString();

        result.Should().Be("1,2,10,key0,key1,key2,key3,key4");
    }

    [Test]
    public void ForInOverArrayEnumeratesIndicesThenExtraProperties()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [10, 20, 30];
            arr.foo = 'bar';
            var out = [];
            for (var k in arr) { out.push(k); }
            out.join(',');
            """).AsString();

        result.Should().Be("0,1,2,foo");
    }

    [Test]
    public void ForInOverArrayIncludesInheritedEnumerableArrayPrototypeIndex()
    {
        // Regression: Array.prototype is itself array-backed, so script-placed elements on it live in
        // its dense store — the builtin-shape shared name list does not include them. The for-in key
        // shortcut must not hand out that list once Array.prototype carries own index keys: the
        // inherited enumerable '5' must be enumerated after the own indices, and the own '1' must
        // shadow the inherited '1'.
        var engine = new Engine();
        var result = engine.Evaluate("""
            Array.prototype[5] = 'inherited';
            Array.prototype[1] = 'shadowed';
            var arr = [10, 20];
            var out = [];
            for (var k in arr) { out.push(k); }
            out.join(',');
            """).AsString();

        result.Should().Be("0,1,5");
    }

    [Test]
    public void ForInKeysAreStrings()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var six = { a: 1, b: 2, c: 3 };
            var allStrings = true;
            for (var k in six) { if (typeof k !== 'string') { allStrings = false; } }
            allStrings;
            """).AsBoolean();

        result.Should().BeTrue();
    }

    [Test]
    public void CachedKeyStringsEqualByValueAcrossEntries()
    {
        // The shape memo shares JsString instances across enumerations; value equality (and thus keyed
        // lookups) must be unaffected.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var six = { a: 1, b: 2, c: 3 };
            var first = [], second = [];
            for (var k in six) { first.push(k); }
            for (var k in six) { second.push(k); }
            var ok = first.length === second.length;
            for (var i = 0; i < first.length; i++) { if (first[i] !== second[i]) ok = false; }
            ok;
            """).AsBoolean();

        result.Should().BeTrue();
    }

    [Test]
    public void ForInOverDenseArrayYieldsAscendingIndicesAsStrings()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [10, 20, 30, 40];
            var out = [];
            for (var k in arr) { out.push(typeof k + ':' + k); }
            out.join(',');
            """).AsString();

        result.Should().Be("string:0,string:1,string:2,string:3");
    }

    [Test]
    public void ForInOverHoleyArraySkipsHoles()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [];
            arr[0] = 'a'; arr[3] = 'b'; arr[7] = 'c';
            var out = [];
            for (var k in arr) { out.push(k); }
            out.join(',');
            """).AsString();

        result.Should().Be("0,3,7");
    }

    [Test]
    public void ForInOverEmptyArrayWithLargeLengthYieldsNothing()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [];
            arr.length = 100000;
            var n = 0;
            for (var k in arr) { n++; }
            n;
            """).AsNumber();

        result.Should().Be(0);
    }

    [Test]
    public void ForInOverArrayWithEnumerablePropOnObjectPrototype()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Object.prototype.zzz = 1;
            var arr = [10, 20];
            var out = [];
            for (var k in arr) { out.push(k); }
            out.join(',');
            """).AsString();

        result.Should().Be("0,1,zzz");
    }

    [Test]
    public void ForInOverArrayDeleteDuringIterationSkipsNotYetVisitedIndex()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [10, 20, 30, 40];
            var out = [];
            for (var k in arr) { out.push(k); if (k === '0') { delete arr[2]; } }
            out.join(',');
            """).AsString();

        result.Should().Be("0,1,3");
    }

    [Test]
    public void ForInOverArrayPushDuringIterationDoesNotYieldNewIndex()
    {
        // Pins snapshot behaviour: elements appended mid-enumeration are not visited (matches the
        // key-list snapshot the exact path takes).
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [10, 20];
            var out = [];
            for (var k in arr) { out.push(k); arr.push(99); }
            out.join(',');
            """).AsString();

        result.Should().Be("0,1");
    }

    [Test]
    public void ForInOverArrayLengthTruncationDuringIterationSkipsRemovedIndices()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [10, 20, 30, 40, 50];
            var out = [];
            for (var k in arr) { out.push(k); if (k === '1') { arr.length = 3; } }
            out.join(',');
            """).AsString();

        result.Should().Be("0,1,2");
    }

    [Test]
    public void ForInOverArrayNonEnumerableIndexIsSkipped()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [10, 20, 30];
            Object.defineProperty(arr, '1', { value: 20, enumerable: false });
            var out = [];
            for (var k in arr) { out.push(k); }
            out.join(',');
            """).AsString();

        result.Should().Be("0,2");
    }

    [Test]
    public void ForInOverArrayDefinePropertyMidLoopHonorsNewEnumerability()
    {
        // Materializing a non-enumerable descriptor mid-loop must stop the not-yet-visited index
        // from yielding (the fast step falls back to per-index probing once descriptors exist).
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [10, 20, 30, 40];
            var out = [];
            for (var k in arr) {
                out.push(k);
                if (k === '0') { Object.defineProperty(arr, '2', { value: 30, enumerable: false }); }
            }
            out.join(',');
            """).AsString();

        result.Should().Be("0,1,3");
    }

    [Test]
    public void ForInOverArraySparseConversionMidLoopKeepsEnumeratingOriginalIndices()
    {
        // A far write mid-loop converts the backing store dense->sparse; the remaining original
        // indices must still enumerate (the added far index is beyond the snapshot and not visited).
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [10, 20, 30, 40];
            var out = [];
            for (var k in arr) { out.push(k); if (k === '1') { arr[50000000] = 1; } }
            out.join(',');
            """).AsString();

        result.Should().Be("0,1,2,3");
    }

    [Test]
    public void NestedForInOverSameArrayDoNotShareState()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [1, 2, 3];
            var pairs = [];
            for (var k in arr) { for (var j in arr) { pairs.push(k + j); } }
            pairs.join(',');
            """).AsString();

        result.Should().Be("00,01,02,10,11,12,20,21,22");
    }

    [Test]
    public void InterleavedGeneratorsOverSameArrayDoNotShareState()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [7, 8, 9];
            function* keys(a) { for (var k in a) { yield k; } }
            var g1 = keys(arr), g2 = keys(arr);
            var seq = [];
            seq.push(g1.next().value);
            seq.push(g2.next().value);
            seq.push(g1.next().value);
            seq.push(g2.next().value);
            seq.push(g1.next().value);
            seq.push(g2.next().value);
            seq.push(g1.next().done);
            seq.join(',');
            """).AsString();

        result.Should().Be("0,0,1,1,2,2,true");
    }

    [Test]
    public void ForInOverArraySubclassInstanceYieldsIndices()
    {
        // The subclass prototype is a plain object between the instance and Array.prototype; the
        // chain is still provably clean, and inherited accessors/methods stay non-enumerable.
        var engine = new Engine();
        var result = engine.Evaluate("""
            class A extends Array {}
            var a = new A();
            a[0] = 'x'; a[1] = 'y';
            var out = [];
            for (var k in a) { out.push(k); }
            out.join(',');
            """).AsString();

        result.Should().Be("0,1");
    }

    [Test]
    public void ForInOverArrayBreakThenReuseResetsPooledIterator()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var arr = [1, 2, 3, 4];
            var rounds = [];
            for (var r = 0; r < 3; r++) {
                var got = [];
                for (var k in arr) { got.push(k); if (k === '1') break; }
                rounds.push(got.join(''));
            }
            rounds.join(',');
            """).AsString();

        result.Should().Be("01,01,01");
    }

    [Test]
    public void ForInOverBuiltinPrototypeYieldsNoEnumerableKeys()
    {
        // The deeper for-in level is often a built-in (Object.prototype): all its members are
        // non-enumerable, so a for-in over it directly yields nothing (exercises the builtin-shape memo).
        var engine = new Engine();
        var result = engine.Evaluate("""
            var out = [];
            for (var k in Object.prototype) { out.push(k); }
            out.length;
            """).AsNumber();

        result.Should().Be(0);
    }

    /// <summary>
    /// <a href="https://tc39.es/ecma262/#sec-enumerate-object-properties">EnumerateObjectProperties</a> is
    /// specified over the key set the enumeration started with: a key deleted before its turn is ignored,
    /// and a key that did not exist then is not part of the enumeration. Filling a hole below the starting
    /// bound is exactly that second case — an <c>unshift</c> from inside the loop body shifts the elements
    /// down over the hole, so index 1 becomes present and index 2 becomes the hole. Only "0" was ever in
    /// the key set.
    /// </summary>
    [TestCase("Array.prototype.unshift", "[0]")]
    [TestCase("Array.prototype.splice", "[0, 0, 0]")]
    public void FillingAHoleDuringEnumerationDoesNotAddItToTheEnumeration(string method, string args)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            var array = [1, /* hole */, 3];
            var called = false;
            var keys = [];
            for (var key in array) {
                keys.push(key);
                if (!called) {
                    called = true;
                    Reflect.apply({{method}}, array, {{args}});
                }
            }
            keys.join(',');
            """).AsString();

        result.Should().Be("0");
    }

    /// <summary>
    /// The hole-free case the index fast path still serves: every index below the bound was present at head
    /// evaluation, so an index that goes missing mid-loop was "deleted before it is processed" and skipping
    /// it is what the specification requires.
    /// </summary>
    [Test]
    public void ShiftingDuringEnumerationOfADenseArraySkipsTheVanishedTail()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var array = [1, 2, 3];
            var called = false;
            var keys = [];
            for (var key in array) {
                keys.push(key);
                if (!called) { called = true; array.shift(); }
            }
            keys.join(',');
            """).AsString();

        result.Should().Be("0,1");
    }

    [Test]
    public void HolesAreSkippedWhenNothingMutatesTheArray()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var array = [1, , 3, , 5];
            var keys = [];
            for (var key in array) { keys.push(key); }
            keys.join(',');
            """).AsString();

        result.Should().Be("0,2,4");
    }
}
