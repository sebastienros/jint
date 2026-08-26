#nullable enable

using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Tests.Runtime;

/// <summary>
/// The field-backed lazy descriptor is a memo over the inherited <c>_value</c>/<c>_flags</c> pair: it resolves
/// once and then clears <see cref="PropertyFlag.CustomJsValue"/>, which is what admits it to the
/// global-identifier and member-write inline caches that decline a custom-valued descriptor. Those two fields
/// are not observable from outside the assembly, so the transition is pinned here rather than in the
/// public-interface suite — what a host can see (the value, and that the factory ran once) is pinned there.
/// </summary>
public class LazyDescriptorInternalsTests
{
    private static readonly PropertyFlag CachedByTheWriteFastPath =
        PropertyFlag.NonData | PropertyFlag.CustomJsValue | PropertyFlag.Writable;

    [Test]
    public void FlagIsSetBeforeTheFirstReadAndClearedAfterIt()
    {
        var calls = 0;
        var descriptor = PropertyDescriptor.CreateLazy<object?>(null, _ =>
        {
            calls++;
            return "built";
        });

        descriptor.Should().BeOfType<LazyPropertyDescriptor<object?>>();
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().Be(PropertyFlag.CustomJsValue);
        descriptor._value.Should().BeNull();
        calls.Should().Be(0);

        descriptor.Value.Should().Be(new JsString("built"));

        calls.Should().Be(1);
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().Be(PropertyFlag.None);
        descriptor._value.Should().Be(new JsString("built"));

        descriptor.Value.Should().Be(new JsString("built"));
        calls.Should().Be(1);
    }

    /// <summary>
    /// A write is materialization too: once a value is stored the factory can never run, because the getter
    /// finds a non-null value. Leaving the flag set would keep the descriptor out of the write fast path and
    /// the global-identifier cache for the rest of its life, for laziness that no longer exists.
    /// </summary>
    [Test]
    public void WriteBeforeFirstReadClearsTheFlagAndSkipsTheFactory()
    {
        var calls = 0;
        var descriptor = PropertyDescriptor.CreateLazy<object?>(null, _ =>
        {
            calls++;
            return "built";
        });

        descriptor.Value = new JsString("written");

        (descriptor._flags & PropertyFlag.CustomJsValue).Should().Be(PropertyFlag.None);
        descriptor._value.Should().Be(new JsString("written"));

        descriptor.Value.Should().Be(new JsString("written"));
        calls.Should().Be(0);
    }

    /// <summary>
    /// <see cref="Jint.Native.Object.ObjectInstance.Set"/>'s dictionary fast path stores into the inherited
    /// <c>_value</c> field directly, without going through the <c>CustomValue</c> setter — so the flag has to
    /// be cleared on the read side as well, not only on the write side.
    /// </summary>
    [Test]
    public void ReadOfAnExternallyStoredValueClearsTheFlagWithoutRunningTheFactory()
    {
        var calls = 0;
        var descriptor = PropertyDescriptor.CreateLazy<object?>(null, _ =>
        {
            calls++;
            return "built";
        });

        // exactly what the fast path does
        descriptor._value = new JsString("stored");

        descriptor.Value.Should().Be(new JsString("stored"));

        calls.Should().Be(0);
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().Be(PropertyFlag.None);
    }

    /// <summary>
    /// The write fast path (<c>JintMemberExpression</c>) and the global-identifier cache both decline a
    /// descriptor carrying <see cref="PropertyFlag.CustomJsValue"/>. After materialization the descriptor must
    /// pass exactly the masks those two lanes test.
    /// </summary>
    [Test]
    public void MaterializedDescriptorPassesTheCacheGates()
    {
        var engine = new Engine();
        engine.AddLazyGlobal("value", _ => "built");

        var descriptor = engine.Global.GetOwnProperty("value");

        // cold: declined by both lanes
        (descriptor._flags & CachedByTheWriteFastPath).Should().NotBe(PropertyFlag.Writable);
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().Be(PropertyFlag.CustomJsValue);

        engine.Evaluate("value").AsString().Should().Be("built");

        // warm: the write fast path's mask must equal exactly Writable, and the identifier cache requires a
        // writable data descriptor without the custom-value flag
        (descriptor._flags & CachedByTheWriteFastPath).Should().Be(PropertyFlag.Writable);
        descriptor.IsDataDescriptor().Should().BeTrue();
        descriptor.Writable.Should().BeTrue();
    }

    [Test]
    public void GlobalWrittenBeforeFirstReadRejoinsTheIdentifierCache()
    {
        var calls = 0;
        var engine = new Engine();
        engine.AddLazyGlobal("value", _ =>
        {
            calls++;
            return "built";
        }, PropertyFlag.NonEnumerable);

        engine.Evaluate("value = 'written';");

        var descriptor = engine.Global.GetOwnProperty("value");
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().Be(PropertyFlag.None);
        (descriptor._flags & CachedByTheWriteFastPath).Should().Be(PropertyFlag.Writable);

        engine.Evaluate("value").AsString().Should().Be("written");
        calls.Should().Be(0, "a value was stored before anything read it, so the factory can never run");
    }

    /// <summary>
    /// The <see cref="IFieldBackedLazyDescriptor"/> claim is that <c>_flags</c> and <c>_value</c> are the whole
    /// state, which is what lets a snapshot restore put a materialized descriptor back to unmaterialized by
    /// writing both. Clearing the flag on a write must not break that: the restore rewrites the flag too.
    /// </summary>
    [Test]
    public void SnapshotRestoreReArmsADescriptorMaterializedByAWrite()
    {
        var calls = 0;
        var engine = new Engine();
        engine.AddLazyGlobal("value", _ =>
        {
            calls++;
            return "built";
        }, PropertyFlag.NonEnumerable);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("value = 'written';");
        var descriptor = engine.Global.GetOwnProperty("value");
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().Be(PropertyFlag.None);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        ReferenceEquals(engine.Global.GetOwnProperty("value"), descriptor).Should().BeTrue();
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().Be(PropertyFlag.CustomJsValue);
        descriptor._value.Should().BeNull();

        engine.Evaluate("value").AsString().Should().Be("built");
        calls.Should().Be(1);
    }
}
