using BenchmarkDotNet.Attributes;
using Jint.Collections;

namespace Jint.Benchmark;

/// <summary>
/// The delete side of the property stores, which had no coverage before #3273 and is the only path that
/// change made more expensive. <see cref="StringDictionarySlim{TValue}"/> used to hand a removed entry's
/// slot to the next add from a free list; it now leaves a tombstone and appends, because a JS property
/// store enumerates in entry order and a reused slot puts a key back in a position older than its
/// creation. The tombstones are reclaimed by compaction inside the resize an add would have needed
/// anyway, so what is measured here is how much that reclamation costs on the shapes that provoke it.
/// <para>
/// The five collection rows are the distinguishable cases. <c>AddOnly</c> is the control: it never deletes,
/// so what it prices is what the delete side costs a workload that has none, which is what the engine does
/// far more of. <c>AddThenRemoveNewest</c> removes the entry that was just added, which
/// walks the window's top back and never compacts. <c>RotateOldest</c> adds a name never seen before and
/// drops the oldest live one; that shape used to leave a hole below the high-water mark on every step and
/// so compacted every <c>capacity - live</c> adds forever, and it is what the circular window of #3315
/// makes free — its removal advances the window's base instead. <c>RotateBehindPinnedKey</c> is the same
/// rotation with one key that is never deleted sitting underneath it, which is what the window cannot
/// help: the base is stuck on the pinned key, every removal is from the middle, and the compaction is
/// still there. It is the degradation control, and what it prices is that the shape got no worse.
/// <c>ReAddMiddle</c> looks like it should compact too and does not: after the first step the re-added
/// name <em>is</em> the newest entry, so it takes the same shortcut <c>AddThenRemoveNewest</c> does. That
/// asymmetry is the point of having both.
/// </para>
/// <para>
/// They measure the collection rather than a script on purpose: an engine-level delete costs a property
/// key conversion, a shape deopt check and a version bump on top, which would dilute the very difference
/// this class exists to size. The two <c>Script*</c> rows put it back in proportion, and there is one per
/// collection shape that behaves differently — <c>ScriptDeleteReAdd</c> re-adds the same name (the
/// non-compacting shape, and the issue's own repro) and <c>ScriptRotateOldest</c> adds a fresh one and
/// deletes the oldest (the compacting shape). Measuring only the first would have priced the shape that
/// did not regress.
/// </para>
/// <para>
/// Each script row gets its own engine through <see cref="IsolatedScript"/>, warmed with that row's script
/// and nothing else, so engine construction stays out of the measurement. <c>ScriptRotateOldest</c>'s
/// engine additionally carries one fixture that row alone needs — a JS array of the property names it
/// churns through, built once in <c>[GlobalSetup]</c> so that string concatenation does not enter the
/// measured loop. The measured script rebuilds its own object every invocation, which it must: the churn
/// deletes names in the order it created them, so an object carried over from the previous invocation
/// would have every one of those deletes miss and grow without bound.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class PropertyDeleteChurnBenchmark
{
    /// <summary>Live key count of the table being churned — 16 and 64 sit either side of a resize step.</summary>
    [Params(16, 64)]
    public int Width { get; set; }

    private const int Steps = 10_000;

    /// <summary>
    /// Churn rounds the script rows drive. Two orders of magnitude below <see cref="Steps"/> because an
    /// engine-level delete-and-add costs roughly two orders of magnitude more than a collection one, which
    /// is the whole comparison these rows exist to make.
    /// </summary>
    private const int ScriptSteps = 2_000;

    /// <summary>Enumerations the <c>ScriptKeysAfterRotation</c> row drives over the already-churned object.</summary>
    private const int EnumerationSteps = 200;

    private string[] _keys = null!;
    private string[] _fresh = null!;
    private IsolatedScript _scriptDeleteReAdd;
    private IsolatedScript _scriptRotateOldest;
    private IsolatedScript _scriptKeysAfterRotation;

    [GlobalSetup]
    public void Setup()
    {
        _keys = new string[Width];
        for (var i = 0; i < Width; i++)
        {
            _keys[i] = "k" + i;
        }

        _fresh = new string[Steps];
        for (var i = 0; i < Steps; i++)
        {
            _fresh[i] = "n" + i;
        }

        var literal = string.Join(",", _keys.Select(static k => k + ":1"));
        _scriptDeleteReAdd = IsolatedScript.Warm($$"""
            var o = { {{literal}} };
            for (var n = 0; n < {{ScriptSteps}}; n++) { delete o.k{{Width / 2}}; o.k{{Width / 2}} = n; }
            o.k{{Width / 2}};
            """);

        var names = $"var names = []; for (var i = 0; i < {ScriptSteps + Width}; i++) names[i] = 'n' + i;";
        var rotate = $$"""
            var o = {};
            for (var i = 0; i < {{Width}}; i++) { o[names[i]] = i; }
            for (var n = 0; n < {{ScriptSteps}}; n++) { o[names[{{Width}} + n]] = n; delete o[names[n]]; }
            """;
        _scriptRotateOldest = IsolatedScript.Warm(
            rotate,
            () =>
            {
                var engine = new Engine();
                engine.Execute(names);
                return engine;
            });

        _scriptKeysAfterRotation = IsolatedScript.Warm(
            $"for (var n = 0; n < {EnumerationSteps}; n++) {{ Object.keys(o); }}",
            () =>
            {
                var engine = new Engine();
                engine.Execute(names);
                engine.Execute(rotate);
                return engine;
            });
    }

    private StringDictionarySlim<object> Filled()
    {
        var dictionary = new StringDictionarySlim<object>();
        for (var i = 0; i < _keys.Length; i++)
        {
            dictionary[_keys[i]] = _keys;
        }

        return dictionary;
    }

    /// <summary>Control: the add path with no removals at all, which is what the engine does far more of.</summary>
    [Benchmark]
    public int AddOnly()
    {
        var total = 0;
        for (var step = 0; step < Steps / _keys.Length; step++)
        {
            total += Filled().Count;
        }

        return total;
    }

    /// <summary>Adding a key and immediately removing it: the high-water mark walks back, nothing compacts.</summary>
    [Benchmark]
    public int AddThenRemoveNewest()
    {
        var dictionary = Filled();
        for (var step = 0; step < Steps; step++)
        {
            dictionary["churn"] = _keys;
            dictionary.Remove("churn");
        }

        return dictionary.Count;
    }

    /// <summary>
    /// Steady-state rotation: every step adds a name never seen before and drops the oldest live one, which
    /// is what a bounded cache built out of an object does, and the shape the circular window makes free.
    /// </summary>
    [Benchmark]
    public int RotateOldest()
    {
        var dictionary = Filled();
        for (var step = 0; step < Steps; step++)
        {
            dictionary[_fresh[step]] = _fresh;
            dictionary.Remove(step < _keys.Length ? _keys[step] : _fresh[step - _keys.Length]);
        }

        return dictionary.Count;
    }

    /// <summary>
    /// The same rotation with an entry pinned underneath it — a name set once and never deleted, which any
    /// object used as a cache with a field on it has. The window's base cannot advance past a live key, so
    /// every removal is from the middle and leaves a tombstone only a compaction reclaims: this is the
    /// shape <see cref="RotateOldest"/> was before the window, and it is here to show it did not get worse.
    /// </summary>
    [Benchmark]
    public int RotateBehindPinnedKey()
    {
        var dictionary = Filled();
        var rotating = _keys.Length - 1;
        for (var step = 0; step < Steps; step++)
        {
            dictionary[_fresh[step]] = _fresh;
            dictionary.Remove(step < rotating ? _keys[step + 1] : _fresh[step - rotating]);
        }

        return dictionary.Count;
    }

    /// <summary>The issue's own shape: delete a key from the middle of the table and add the same name back.</summary>
    [Benchmark]
    public int ReAddMiddle()
    {
        var dictionary = Filled();
        var middle = _keys[_keys.Length / 2];
        for (var step = 0; step < Steps; step++)
        {
            dictionary.Remove(middle);
            dictionary[middle] = _fresh;
        }

        return dictionary.Count;
    }

    /// <summary>The same delete-and-re-add through the engine, so the collection delta can be put in proportion.</summary>
    [Benchmark]
    public void ScriptDeleteReAdd() => _scriptDeleteReAdd.Execute();

    /// <summary>
    /// <see cref="RotateOldest"/> through the engine: the compacting shape, which is the one
    /// <see cref="ScriptDeleteReAdd"/> does not reach.
    /// </summary>
    [Benchmark]
    public void ScriptRotateOldest() => _scriptRotateOldest.Execute();

    /// <summary>
    /// Reading the keys of an object that has already been churned, which is the cost the tombstones
    /// impose on the <em>other</em> side. The rotation happens once, in this row's own engine fixture, so
    /// what is measured is only the enumeration — and the enumerator has to step over every tombstone the
    /// table is holding, which is exactly what the compaction threshold decides the number of.
    /// </summary>
    [Benchmark]
    public void ScriptKeysAfterRotation() => _scriptKeysAfterRotation.Execute();
}
