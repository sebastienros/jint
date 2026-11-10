#nullable enable

using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins that a wrapped read-only dictionary refuses a script's write or delete of an entry the JavaScript way,
/// <c>false</c> from <c>[[Set]]</c> / <c>[[Delete]]</c> and a <c>TypeError</c> in strict mode
/// (https://tc39.es/ecma262/#sec-putvalue), instead of letting the dictionary's
/// <see cref="NotSupportedException"/> out of <c>Evaluate</c>, raw or wrapped in a
/// <see cref="System.Reflection.TargetInvocationException"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="NotSupportedException"/> is what the collection contracts throw for a read-only dictionary, and
/// each lane now reads it as the refusal it is: the number lane of a non-string-keyed dictionary
/// (<c>d[5] = 'y'</c>), the string lane through an indexer whose setter throws it, and <c>delete</c> on both
/// lanes. Every case runs under <see cref="Options.InteropOptions.AllowWrite"/>; without it these writes were
/// refused already. The oracle is the host dictionary, read from C#, which must not change.
/// </para>
/// <para>
/// Found beside #4518: a non-string-keyed <c>ImmutableDictionary</c> also threw
/// <see cref="InvalidCastException"/> for a name it cannot hold as a key, even on a read, from its non-generic
/// <c>Contains</c>.
/// </para>
/// </remarks>
public class HostReadOnlyDictionaryWriteTests
{
    private static readonly string[] _shapes =
    [
        "ReadOnlyDictionary<int, string>",
        "ReadOnlyDictionary<string, string>",
        "ImmutableDictionary<int, string>",
        "ImmutableDictionary<string, string>",
        "ImmutableSortedDictionary<int, string>",
        "FrozenDictionary<int, string>",
        "FrozenDictionary<string, string>",
        nameof(ReadOnlyIntDictionary),
        nameof(ReadOnlyStringDictionary),
        nameof(ReadOnlyHashtable),
        nameof(BareReadOnlyDictionary),
    ];

    public static TestCases<string> Shapes => new(_shapes);

    public static TestCases<string, bool> ShapesAndModes
    {
        get
        {
            var data = new TestCases<string, bool>();
            foreach (var shape in _shapes)
            {
                data.Add(shape, false);
                data.Add(shape, true);
            }

            return data;
        }
    }

    private sealed record Shape(object Host, Func<string> Render, string ExistingKey, string NewKey);

    private static Shape CreateShape(string shape)
    {
        var ints = new Dictionary<int, string> { [1] = "one" };
        var strings = new Dictionary<string, string> { ["k"] = "v" };
        switch (shape)
        {
            case "ReadOnlyDictionary<int, string>":
            {
                var d = new ReadOnlyDictionary<int, string>(ints);
                return new Shape(d, () => Render(d), "1", "5");
            }
            case "ReadOnlyDictionary<string, string>":
            {
                var d = new ReadOnlyDictionary<string, string>(strings);
                return new Shape(d, () => Render(d), "'k'", "'x'");
            }
            case "ImmutableDictionary<int, string>":
            {
                var d = ImmutableDictionary.CreateRange(ints);
                return new Shape(d, () => Render(d), "1", "5");
            }
            case "ImmutableDictionary<string, string>":
            {
                var d = ImmutableDictionary.CreateRange(strings);
                return new Shape(d, () => Render(d), "'k'", "'x'");
            }
            case "ImmutableSortedDictionary<int, string>":
            {
                var d = ImmutableSortedDictionary.CreateRange(ints);
                return new Shape(d, () => Render(d), "1", "5");
            }
            case "FrozenDictionary<int, string>":
            {
                var d = ints.ToFrozenDictionary();
                return new Shape(d, () => Render(d), "1", "5");
            }
            case "FrozenDictionary<string, string>":
            {
                var d = strings.ToFrozenDictionary();
                return new Shape(d, () => Render(d), "'k'", "'x'");
            }
            case nameof(ReadOnlyIntDictionary):
            {
                var d = new ReadOnlyIntDictionary(ints);
                return new Shape(d, () => Render(d), "1", "5");
            }
            case nameof(ReadOnlyStringDictionary):
            {
                var d = new ReadOnlyStringDictionary(strings);
                return new Shape(d, () => Render(d), "'k'", "'x'");
            }
            case nameof(ReadOnlyHashtable):
            {
                var d = new ReadOnlyHashtable(strings);
                return new Shape(d, () => Render(d.Cast<DictionaryEntry>().Select(e => new KeyValuePair<object, object?>(e.Key, e.Value))), "'k'", "'x'");
            }
            case nameof(BareReadOnlyDictionary):
            {
                var d = new BareReadOnlyDictionary(ints);
                return new Shape(d, () => Render(d), "1", "5");
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown shape");
        }
    }

    private static string Render<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> entries)
        => string.Join(",", entries.Select(e => e.Key + "=" + e.Value).OrderBy(s => s, StringComparer.Ordinal));

    private static Engine CreateEngine(object host, bool strict, bool allowWrite = true)
    {
        var engine = new Engine(options =>
        {
            options.Strict = strict;
            options.Interop.AllowWrite = allowWrite;
        });
        engine.SetValue("d", host);
        return engine;
    }

    private static string Outcome(Engine engine, string statement)
        => engine.Evaluate($"try {{ {statement}; 'no error' }} catch (e) {{ e.constructor.name }}").AsString();

    /// <summary>
    /// <c>d[5] = 'y'</c> on a read-only <c>Dictionary&lt;int, _&gt;</c> shape was the reported leak: the
    /// number lane let the setter's <see cref="NotSupportedException"/> out wrapped in a
    /// <see cref="System.Reflection.TargetInvocationException"/>.
    /// </summary>
    // Each assertion below gets an engine of its own. `d[1]` keeps the number for a non-string-keyed
    // dictionary while Reflect.set / Reflect.deleteProperty convert it to the string "1", so they are two lanes,
    // and a descriptor the string lane resolves would otherwise answer the number lane's question after it.

    [TestCaseSource(nameof(ShapesAndModes))]
    public void WritingANewEntryIsRefused(string shape, bool strict)
    {
        var (host, render, _, newKey) = CreateShape(shape);
        var before = render();

        Outcome(CreateEngine(host, strict), $"d[{newKey}] = 'y'").Should().Be(strict ? "TypeError" : "no error");
        CreateEngine(host, strict).Evaluate($"Reflect.set(d, {newKey}, 'y')").AsBoolean().Should().BeFalse();
        CreateEngine(host, strict: false).Evaluate($"d[{newKey}] = 'y'; {newKey} in d").AsBoolean().Should().BeFalse();
        render().Should().Be(before);
    }

    [TestCaseSource(nameof(ShapesAndModes))]
    public void OverwritingAnEntryIsRefused(string shape, bool strict)
    {
        var (host, render, existingKey, _) = CreateShape(shape);
        var before = render();

        Outcome(CreateEngine(host, strict), $"d[{existingKey}] = 'z'").Should().Be(strict ? "TypeError" : "no error");
        CreateEngine(host, strict).Evaluate($"Reflect.set(d, {existingKey}, 'z')").AsBoolean().Should().BeFalse();
        render().Should().Be(before);
    }

    [TestCaseSource(nameof(ShapesAndModes))]
    public void DeletingAnEntryIsRefused(string shape, bool strict)
    {
        var (host, render, existingKey, _) = CreateShape(shape);
        var before = render();

        Outcome(CreateEngine(host, strict), $"delete d[{existingKey}]").Should().Be(strict ? "TypeError" : "no error");
        CreateEngine(host, strict: false).Evaluate($"delete d[{existingKey}]; {existingKey} in d").AsBoolean().Should().BeTrue();
        CreateEngine(host, strict).Evaluate($"Reflect.deleteProperty(d, {existingKey})").AsBoolean().Should().BeFalse();
        render().Should().Be(before);
    }

    /// <summary>
    /// Only an entry is refused: a property the script defined on the wrapper is removed as on any object, and
    /// the dictionary is not asked to remove a key it does not hold.
    /// </summary>
    [TestCaseSource(nameof(Shapes))]
    public void APropertyTheScriptDefinedIsStillDeleted(string shape)
    {
        var (host, render, _, _) = CreateShape(shape);
        var before = render();
        var engine = CreateEngine(host, strict: true);

        engine.Execute("Object.defineProperty(d, 'tag', { value: 1, writable: true, enumerable: true, configurable: true });");

        engine.Evaluate("delete d.tag").AsBoolean().Should().BeTrue();
        engine.Evaluate("'tag' in d").AsBoolean().Should().BeFalse();
        render().Should().Be(before);
    }

    /// <summary>
    /// A read through the string lane stores the indexer's descriptor on the wrapper, so the write after it no
    /// longer takes the lane that reports a refusal: it goes through the stored descriptor instead, which
    /// reported success for a write the dictionary had refused. Both run in one engine here, in both modes.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void AWriteAfterAReadIsRefusedToo(string shape, bool strict)
    {
#if NETFRAMEWORK
        if (shape == "FrozenDictionary<int, string>")
        {
            // the read, not the write: the string lane reaches FrozenDictionary's `ref readonly TValue this[TKey]`,
            // and .NET Framework's reflection cannot invoke a method that returns by reference
            Assert.Ignore("reading an entry by its string spelling throws on .NET Framework, before and after #4518");
        }
#endif

        var (host, render, existingKey, _) = CreateShape(shape);
        var before = render();
        var key = StringSpelling(existingKey);
        var engine = CreateEngine(host, strict);

        engine.Evaluate($"var read = d[{key}]; Reflect.set(d, {key}, 'z')").AsBoolean().Should().BeFalse();
        Outcome(engine, $"d[{key}] = 'z'").Should().Be(strict ? "TypeError" : "no error");
        engine.Evaluate($"d[{key}] === read").AsBoolean().Should().BeTrue();
        render().Should().Be(before);
    }

    /// <summary>
    /// Under <c>CatchClrExceptions</c> the string lane's <see cref="NotSupportedException"/> used to reach
    /// script as a catchable <c>Error</c>, through the host's exception handler. A refusal is not an error, so
    /// the handler is not consulted: sloppy mode is silent, strict mode is the <c>TypeError</c> every refusal is.
    /// </summary>
    [TestCase(nameof(ReadOnlyStringDictionary), false)]
    [TestCase(nameof(ReadOnlyStringDictionary), true)]
    [TestCase(nameof(ReadOnlyHashtable), false)]
    [TestCase(nameof(ReadOnlyHashtable), true)]
    public void UnderCatchClrExceptionsTheRefusalIsNotAnError(string shape, bool strict)
    {
        var (host, render, existingKey, _) = CreateShape(shape);
        var before = render();
        var engine = new Engine(options =>
        {
            options.Strict = strict;
            options.Interop.AllowWrite = true;
            options.CatchClrExceptions();
        });
        engine.SetValue("d", host);

        Outcome(engine, $"d[{existingKey}] = 'z'").Should().Be(strict ? "TypeError" : "no error");
        render().Should().Be(before);
    }

    private static string StringSpelling(string key) => key.StartsWith("'", StringComparison.Ordinal) ? key : "'" + key + "'";

    /// <summary>
    /// <see cref="PlatformNotSupportedException"/> is a <see cref="NotSupportedException"/>, and one the engine
    /// always lets through to the host (it says the platform cannot run the operation at all), so it is not a
    /// read-only refusal. Each lane surfaces it exactly as before: the number lane and <c>delete</c> as the
    /// <see cref="System.Reflection.TargetInvocationException"/> it arrives in, the string lane unwrapped.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void PlatformNotSupportedExceptionStillReachesTheHost(bool catchClrExceptions)
    {
        Engine CreatePlatformEngine()
        {
            var engine = new Engine(options =>
            {
                options.Interop.AllowWrite = true;
                if (catchClrExceptions)
                {
                    options.CatchClrExceptions();
                }
            });
            engine.SetValue("d", new PlatformRefusingDictionary(new Dictionary<int, string> { [1] = "one" }));
            return engine;
        }

        var numberLaneSet = Caught.Exception(() => CreatePlatformEngine().Execute("d[5] = 'y';"));
        numberLaneSet.Should().BeOfType<System.Reflection.TargetInvocationException>()
            .Which.InnerException.Should().BeOfType<PlatformNotSupportedException>();

        var stringLaneSet = Caught.Exception(() => CreatePlatformEngine().Execute("d['1'] = 'z';"));
        stringLaneSet.Should().BeOfType<PlatformNotSupportedException>();

        var numberLaneDelete = Caught.Exception(() => CreatePlatformEngine().Execute("delete d[1];"));
        numberLaneDelete.Should().BeOfType<System.Reflection.TargetInvocationException>()
            .Which.InnerException.Should().BeOfType<PlatformNotSupportedException>();
    }

    /// <summary>
    /// Without <c>AllowWrite</c> all three were refused already, and still are.
    /// </summary>
    [TestCaseSource(nameof(Shapes))]
    public void WithoutAllowWriteEverythingIsRefusedAsBefore(string shape)
    {
        var (host, render, existingKey, newKey) = CreateShape(shape);
        var before = render();
        var engine = CreateEngine(host, strict: true, allowWrite: false);

        Outcome(engine, $"d[{newKey}] = 'y'").Should().Be("TypeError");
        Outcome(engine, $"d[{existingKey}] = 'z'").Should().Be("TypeError");
        Outcome(engine, $"delete d[{existingKey}]").Should().Be("TypeError");
        render().Should().Be(before);
    }

    /// <summary>
    /// A non-string-keyed <c>ImmutableDictionary</c> answers its non-generic <c>Contains</c> by casting the
    /// key, so a name it cannot hold threw <see cref="InvalidCastException"/> on a read, in both modes. Such a
    /// name is not an entry, and is not asked about.
    /// </summary>
    [TestCase("ImmutableDictionary<int, string>", false)]
    [TestCase("ImmutableDictionary<int, string>", true)]
    [TestCase("ImmutableSortedDictionary<int, string>", false)]
    [TestCase("ImmutableSortedDictionary<int, string>", true)]
    public void ANameAnImmutableDictionaryCannotHoldReadsAsAbsent(string shape, bool allowWrite)
    {
        var (host, render, _, _) = CreateShape(shape);
        var before = render();
        var engine = CreateEngine(host, strict: false, allowWrite);

        engine.Evaluate("typeof d.foo").AsString().Should().Be("undefined");
        engine.Evaluate("'foo' in d").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.getOwnPropertyDescriptor(d, 'foo') === undefined").AsBoolean().Should().BeTrue();
        engine.Evaluate("delete d.foo").AsBoolean().Should().BeTrue();

        // and, as #4518 has it, under AllowWrite the name is a property of the wrapper
        engine.Execute("d.foo = 2;");
        engine.Evaluate("typeof d.foo").AsString().Should().Be(allowWrite ? "number" : "undefined");
        render().Should().Be(before);
    }

    /// <summary>A read-only generic dictionary that is not a base class library type.</summary>
    public sealed class ReadOnlyIntDictionary(Dictionary<int, string> inner) : IDictionary<int, string>
    {
        public string this[int key]
        {
            get => inner[key];
            set => throw new NotSupportedException("read-only");
        }

        public ICollection<int> Keys => inner.Keys;
        public ICollection<string> Values => inner.Values;
        public int Count => inner.Count;
        public bool IsReadOnly => true;
        public void Add(int key, string value) => throw new NotSupportedException("read-only");
        public void Add(KeyValuePair<int, string> item) => throw new NotSupportedException("read-only");
        public void Clear() => throw new NotSupportedException("read-only");
        public bool Contains(KeyValuePair<int, string> item) => ((ICollection<KeyValuePair<int, string>>) inner).Contains(item);
        public bool ContainsKey(int key) => inner.ContainsKey(key);
        public void CopyTo(KeyValuePair<int, string>[] array, int arrayIndex) => ((ICollection<KeyValuePair<int, string>>) inner).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<int, string>> GetEnumerator() => inner.GetEnumerator();
        public bool Remove(int key) => throw new NotSupportedException("read-only");
        public bool Remove(KeyValuePair<int, string> item) => throw new NotSupportedException("read-only");
        public bool TryGetValue(int key, out string value) => inner.TryGetValue(key, out value!);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A read-only string-keyed dictionary whose public indexer has a setter that throws, which is the string
    /// lane's path to the target.
    /// </summary>
    public sealed class ReadOnlyStringDictionary(Dictionary<string, string> inner) : IDictionary<string, string>
    {
        public string this[string key]
        {
            get => inner[key];
            set => throw new NotSupportedException("read-only");
        }

        public ICollection<string> Keys => inner.Keys;
        public ICollection<string> Values => inner.Values;
        public int Count => inner.Count;
        public bool IsReadOnly => true;
        public void Add(string key, string value) => throw new NotSupportedException("read-only");
        public void Add(KeyValuePair<string, string> item) => throw new NotSupportedException("read-only");
        public void Clear() => throw new NotSupportedException("read-only");
        public bool Contains(KeyValuePair<string, string> item) => ((ICollection<KeyValuePair<string, string>>) inner).Contains(item);
        public bool ContainsKey(string key) => inner.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, string>>) inner).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => inner.GetEnumerator();
        public bool Remove(string key) => throw new NotSupportedException("read-only");
        public bool Remove(KeyValuePair<string, string> item) => throw new NotSupportedException("read-only");
        public bool TryGetValue(string key, out string value) => inner.TryGetValue(key, out value!);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>A non-generic <see cref="IDictionary"/> whose <see cref="IDictionary.IsReadOnly"/> is true.</summary>
    public sealed class ReadOnlyHashtable : Hashtable
    {
        private readonly bool _sealed;

        public ReadOnlyHashtable(Dictionary<string, string> entries)
        {
            foreach (var entry in entries)
            {
                base[entry.Key] = entry.Value;
            }

            _sealed = true;
        }

        public override bool IsReadOnly => _sealed;

        public override object? this[object key]
        {
            get => base[key];
            set
            {
                if (_sealed)
                {
                    throw new NotSupportedException("read-only");
                }

                base[key] = value;
            }
        }

        public override void Add(object key, object? value) => throw new NotSupportedException("read-only");
        public override void Remove(object key) => throw new NotSupportedException("read-only");
        public override void Clear() => throw new NotSupportedException("read-only");
    }

    /// <summary>A dictionary whose every mutation throws <see cref="PlatformNotSupportedException"/>.</summary>
    public sealed class PlatformRefusingDictionary(Dictionary<int, string> inner) : IDictionary<int, string>
    {
        public string this[int key]
        {
            get => inner[key];
            set => throw new PlatformNotSupportedException("not on this platform");
        }

        public ICollection<int> Keys => inner.Keys;
        public ICollection<string> Values => inner.Values;
        public int Count => inner.Count;
        public bool IsReadOnly => false;
        public void Add(int key, string value) => throw new PlatformNotSupportedException("not on this platform");
        public void Add(KeyValuePair<int, string> item) => throw new PlatformNotSupportedException("not on this platform");
        public void Clear() => throw new PlatformNotSupportedException("not on this platform");
        public bool Contains(KeyValuePair<int, string> item) => ((ICollection<KeyValuePair<int, string>>) inner).Contains(item);
        public bool ContainsKey(int key) => inner.ContainsKey(key);
        public void CopyTo(KeyValuePair<int, string>[] array, int arrayIndex) => ((ICollection<KeyValuePair<int, string>>) inner).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<int, string>> GetEnumerator() => inner.GetEnumerator();
        public bool Remove(int key) => throw new PlatformNotSupportedException("not on this platform");
        public bool Remove(KeyValuePair<int, string> item) => throw new PlatformNotSupportedException("not on this platform");
        public bool TryGetValue(int key, out string value) => inner.TryGetValue(key, out value!);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A dictionary that implements only <see cref="IReadOnlyDictionary{TKey,TValue}"/>, so there is no
    /// <c>Remove</c> to call. Its delete threw nothing, but reported <c>true</c> while the entry stayed.
    /// </summary>
    public sealed class BareReadOnlyDictionary(Dictionary<int, string> inner) : IReadOnlyDictionary<int, string>
    {
        public string this[int key] => inner[key];
        public IEnumerable<int> Keys => inner.Keys;
        public IEnumerable<string> Values => inner.Values;
        public int Count => inner.Count;
        public bool ContainsKey(int key) => inner.ContainsKey(key);
        public bool TryGetValue(int key, out string value) => inner.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<int, string>> GetEnumerator() => inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
