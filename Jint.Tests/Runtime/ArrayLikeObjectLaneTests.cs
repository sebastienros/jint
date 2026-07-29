#nullable enable

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Witness for the two lanes <see cref="ArrayLikeObject"/> exists to reach. The observable behaviour of the type
/// is covered from the public surface in Jint.Tests.PublicInterface/HostArrayLikeObjectTests.cs; what cannot be
/// seen from there is <em>which</em> implementation answered, and a host collection silently falling back to the
/// generic per-key <c>ObjectOperations</c> path — three virtual probes and a key string per element — would pass
/// every one of those tests. This project has <c>InternalsVisibleTo</c>, so it can pin the dispatch itself.
/// </summary>
public class ArrayLikeObjectLaneTests
{
    private sealed class HostList : ArrayLikeObject
    {
        private readonly List<string> _items;

        public HostList(Engine engine, params string[] items) : base(engine)
        {
            _items = new List<string>(items);
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }

        public override uint Length => (uint) _items.Count;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            if (index < (uint) _items.Count)
            {
                value = _items[(int) index];
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }
    }

    [Fact]
    public void TheReadDispatcherKeysOnTheTypeAndTheWriteDispatcherDeliberatelyDoesNot()
    {
        var engine = new Engine();
        var host = new HostList(engine, "a", "b", "c");

        // Read side: the dedicated operations class, so every Array.prototype generic, the array iterator
        // (for-of / spread / Array.from / destructuring) and apply-spreading cost one TryGetIndex per element.
        ArrayOperations.For(host, forWrite: false).GetType().Name.Should().Be("ArrayLikeObjectOperations");

        // Write side: deliberately NOT keyed on the type. The class is read-only, so a mutating generic must
        // land on the ordinary object path and fail through [[Set]] against a non-writable own property —
        // a spec-shaped TypeError rather than a CLR exception out of a lane that cannot write.
        ArrayOperations.For(host, forWrite: true).GetType().Name.Should().Be("ObjectOperations");
    }

    [Fact]
    public void TheOperationsClassReportsTheLiveLengthAndElements()
    {
        var engine = new Engine();
        var host = new HostList(engine, "a", "b", "c");
        var operations = ArrayOperations.For(host, forWrite: false);

        operations.GetLength().Should().Be(3u);
        operations.GetLongLength().Should().Be(3ul);
        operations.Get(1).Should().Be("b");
        operations.HasProperty(2).Should().Be(true);
        operations.HasProperty(3).Should().Be(false);
        operations.TryGetValue(0, out var value).Should().Be(true);
        value.Should().Be("a");
        operations.TryGetValue(9, out _).Should().Be(false);
    }

    [Fact]
    public void TheTypeDerivesOrdinaryReadSemanticsWithTheOwnValueHook()
    {
        var engine = new Engine();
        var host = new HostList(engine, "a");

        // Sealing Get would otherwise derive ExoticGet; the constructor corrects it, and the
        // TryGetOwnPropertyValue override independently earns the descriptor-free own-read routing.
        (host._type & InternalTypes.OrdinaryGet).Should().NotBe(InternalTypes.Empty);
        (host._type & InternalTypes.ExoticGet).Should().Be(InternalTypes.Empty);
        (host._type & InternalTypes.OwnValueHook).Should().NotBe(InternalTypes.Empty);

        // and the array-like claims the receiver guards of Array.prototype.keys/values/entries and the
        // destructuring fast path consult
        host.IsArrayLike.Should().Be(true);
        host.GetLength().Should().Be(1u);
        host.HasOriginalIterator.Should().Be(true);
        host.IsArray().Should().Be(false);
    }

    /// <summary>
    /// The prototype-method cache's holder gate refuses an <see cref="InternalTypes.OrdinaryGet"/> object because
    /// that flag stands in for "this object's own-property set lives outside the engine, so no version witnesses
    /// it" — and carves out <see cref="InternalTypes.BuiltinShapeMode"/>, where the inference is wrong because the
    /// whole set is engine storage and every change to it bumps the version. A live host collection is the exact
    /// case the refusal exists for, so the carve-out must never admit it: its elements are in the host, and
    /// nothing the engine counts moves when one appears. It cannot enter that mode (InitializeBuiltinShape is
    /// private protected and IBuiltinShaped is internal), but that is a fact about reachability, and this pins it
    /// as a fact about the answer.
    /// </summary>
    [Fact]
    public void TheBuiltinShapeCarveOutDoesNotAdmitAHostCollectionAsACacheHolder()
    {
        var engine = new Engine();
        var host = new HostList(engine, "a");

        (host._type & InternalTypes.BuiltinShapeMode).Should().Be(InternalTypes.Empty);
        (host._type & InternalTypes.ShapeMode).Should().Be(InternalTypes.Empty);
        (host._type & InternalTypes.PlainObject).Should().Be(InternalTypes.Empty);
    }

    /// <summary>
    /// A host that overrides only the containment hook, so the operations class's per-element hole test can be
    /// told apart from its element read. The observable behaviour is covered from the public surface in
    /// Jint.Tests.PublicInterface/HostArrayLikeHasIndexTests.cs; what needs internals is proving that the
    /// dispatcher's <c>HasProperty</c> — the hole test the generics run — is the containment hook and not a
    /// discarded read.
    /// </summary>
    private sealed class ContainmentOnlyHostList : ArrayLikeObject
    {
        public ContainmentOnlyHostList(Engine engine) : base(engine)
        {
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }

        public int TryGetIndexCalls { get; private set; }

        public int HasIndexCalls { get; private set; }

        public override uint Length => 3;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            TryGetIndexCalls++;
            if (index < 3)
            {
                value = "v" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }

        protected override bool HasIndex(uint index)
        {
            HasIndexCalls++;
            return index < 3;
        }
    }

    [Fact]
    public void TheOperationsHoleTestIsTheContainmentHookAndTheElementReadIsNot()
    {
        var engine = new Engine();
        var host = new ContainmentOnlyHostList(engine);
        var operations = ArrayOperations.For(host, forWrite: false);

        operations.HasProperty(1).Should().Be(true);
        host.HasIndexCalls.Should().Be(1);
        if (HostContractVerificationSwitch.Enabled)
        {
            // the agreement verifier re-runs TryGetIndex once per probe
            host.TryGetIndexCalls.Should().Be(1);
        }
        else
        {
            host.TryGetIndexCalls.Should().Be(0);
        }

        var before = host.TryGetIndexCalls;
        var hasIndexBefore = host.HasIndexCalls;
        operations.Get(1).Should().Be("v1");
        (host.TryGetIndexCalls - before).Should().Be(1, "a read produces the element");
        host.HasIndexCalls.Should().Be(hasIndexBefore, "a read must never ask the containment hook first");
    }
}
