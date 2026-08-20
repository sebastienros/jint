using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Diagnostics;

/// <summary>
/// Builds an <see cref="EngineMemoryReport"/> by reading the engine's own collections. Everything here is a
/// read of state that already exists, which is what makes the report a diagnostic rather than a perturbation:
/// no getter is invoked, no lazy property factory is run, and no built-in's function object is created in
/// order to be counted. The engine gains no field for any of this — the report is derived on demand and
/// nothing about it costs anything to a host that never asks.
/// </summary>
internal static class EngineMemoryReportBuilder
{
    /// <summary>
    /// The flags that mean "this descriptor has no value in hand". <see cref="PropertyFlag.NonData"/> marks
    /// an accessor, whose value only exists once its getter has run; <see cref="PropertyFlag.CustomJsValue"/>
    /// marks a descriptor whose value comes from an override — a lazy property whose factory has not run, a
    /// reflected CLR member read live off the host object — which producing would mean running host code.
    /// Testing the flags rather than the <see cref="PropertyDescriptor.Get"/> / <see cref="PropertyDescriptor.Value"/>
    /// properties is what keeps the read non-perturbing: several descriptor subclasses materialize their
    /// getter function on the first read of <c>Get</c>.
    /// </summary>
    private const PropertyFlag NoValueInHand = PropertyFlag.NonData | PropertyFlag.CustomJsValue;

    internal static EngineMemoryReport Build(Engine engine, int objectCensusBound)
    {
        // The principal realm, not the current one: this answers a question about the engine, and inside a
        // ShadowRealm evaluation `engine.Realm` would name the shadow realm instead.
        var realm = engine._mainRealm;
        var global = realm.GlobalObject;
        var globalEnvironment = realm.GlobalEnv;
        var lexical = globalEnvironment._declarativeRecord;

        CountGlobalProperties(global, out var globalPropertyCount, out var materializedGlobalPropertyCount);

        return new EngineMemoryReport(
            globalPropertyCount,
            materializedGlobalPropertyCount,
            (lexical._dictionary?.Count ?? 0) + (lexical._slotNames?.Length ?? 0),
            engine.EventLoop.QueueDepth,
            engine.PendingTimerCount,
            engine.PendingAtomicsWaiterCount,
            engine.Modules.RegisteredModuleCount,
            engine.Modules.PendingModuleLoadCount,
            new HandlerTreeCacheReport(
                engine.FunctionDefinitionCacheCount,
                engine.ScriptStatementListCacheCount,
                engine.EvaluatedScriptCount),
            new InteropCacheReport(
                engine.TypeCache.Count,
                engine._typeReferences?.Count ?? 0),
            new PoolReport(
                engine._referencePool.PooledCount,
                engine._argumentsInstancePool.PooledCount,
                engine._objectTraverseStackPool.PooledCount,
                engine._jsValueArrayPool.PooledArrayCount,
                engine._jsValueArrayPool.PooledArraySlotCount),
            Census(global, objectCensusBound));
    }

    /// <summary>
    /// Counts the global object's own properties, and how many of them currently hold a produced value.
    /// Storage-shaped rather than key-shaped: asking the object for a descriptor per name would materialize
    /// every built-in reference the walk was supposed to observe as unmaterialized.
    /// </summary>
    private static void CountGlobalProperties(ObjectInstance global, out int total, out int materialized)
    {
        total = 0;
        materialized = 0;

        if ((global._type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty
            && global is IBuiltinShaped shaped
            && shaped.BuiltinDescriptors is { } builtinDescriptors)
        {
            // The declared layout is the object's own string-keyed property set, whether or not a given
            // slot's descriptor has been created yet; a null entry is a slot nothing has touched.
            total += shaped.BuiltinShape.Names.Length;
            foreach (var descriptor in builtinDescriptors)
            {
                if (HasValueInHand(descriptor))
                {
                    materialized++;
                }
            }
        }
        else if ((global._type & InternalTypes.ShapeMode) != InternalTypes.Empty && global is JsObject shapeModeObject)
        {
            var shape = shapeModeObject.ShapeOf;
            total += shape.SlotCount;
            for (var slot = 0; slot < shape.SlotCount; slot++)
            {
                if (shapeModeObject.GetSlot(slot) is not JsObject.UnmaterializedSlots)
                {
                    materialized++;
                }
            }
        }

        // The ordinary property dictionary. On a shaped host this is the hybrid side dictionary holding
        // whatever was added after initialization; once the host has deopted — which any top-level `var`
        // does to the global object — it holds everything, and the branches above contributed nothing.
        if (global._properties is { } properties)
        {
            total += properties.Count;
            foreach (var pair in properties)
            {
                if (HasValueInHand(pair.Value))
                {
                    materialized++;
                }
            }
        }

        if (global._symbols is { } symbols)
        {
            total += symbols.Count;
            foreach (var pair in symbols)
            {
                if (HasValueInHand(pair.Value))
                {
                    materialized++;
                }
            }
        }
    }

    private static ObjectCensusReport Census(ObjectInstance global, int bound)
    {
        if (bound <= 0)
        {
            return new ObjectCensusReport(bound, boundReached: false, 0, 0, 0, 0, 0, 0);
        }

        var walk = new CensusWalk(bound);
        walk.Visit(global);

        while (!walk.BoundReached && walk.Queue.Count > 0)
        {
            VisitStoredOwnValues(walk.Queue.Dequeue(), walk);
        }

        return walk.ToReport();
    }

    /// <summary>
    /// Offers <paramref name="walk"/> the own-property values <paramref name="obj"/> is <em>already</em>
    /// holding, and stops the moment the walk says it is full. Reads the storage directly instead of going
    /// through <c>GetOwnPropertyKeys</c> plus <c>GetOwnProperty</c>, for two reasons: that pair materializes a
    /// built-in's function object per name, which the census must not do, and it would build one key object
    /// per element of an array whose elements this reads straight out of the backing store.
    /// </summary>
    /// <remarks>
    /// Values are handed over one at a time rather than collected into a list first, because a dense array
    /// may hold ten million elements and a list of them would be the largest allocation in the process — an
    /// absurd thing for a memory diagnostic to do. Offering them keeps the walk's own budget the only bound
    /// that matters.
    /// </remarks>
    private static void VisitStoredOwnValues(ObjectInstance obj, CensusWalk walk)
    {
        if (obj is ArrayInstance array)
        {
            var dense = array._dense;
            if (dense is not null)
            {
                foreach (var element in dense)
                {
                    if (!walk.Visit(element))
                    {
                        return;
                    }
                }
            }
            else if (array.SparseElements is { } sparse)
            {
                foreach (var entry in sparse)
                {
                    if (!VisitValueInHand(entry.Value, walk))
                    {
                        return;
                    }
                }
            }
        }

        if ((obj._type & InternalTypes.ShapeMode) != InternalTypes.Empty && obj is JsObject shapeModeObject)
        {
            var shape = shapeModeObject.ShapeOf;
            for (var slot = 0; slot < shape.SlotCount; slot++)
            {
                // A slot whose factory has not run holds a sentinel that is not an ObjectInstance, so the
                // walk skips it without anything here having to know the sentinel by name.
                if (!walk.Visit(shapeModeObject.GetSlot(slot)))
                {
                    return;
                }
            }
        }
        else if ((obj._type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty
                 && obj is IBuiltinShaped shaped
                 && shaped.BuiltinDescriptors is { } builtinDescriptors)
        {
            foreach (var descriptor in builtinDescriptors)
            {
                if (!VisitValueInHand(descriptor, walk))
                {
                    return;
                }
            }
        }

        if (obj._properties is { } properties)
        {
            foreach (var pair in properties)
            {
                if (!VisitValueInHand(pair.Value, walk))
                {
                    return;
                }
            }
        }

        if (obj._symbols is { } symbols)
        {
            foreach (var pair in symbols)
            {
                if (!VisitValueInHand(pair.Value, walk))
                {
                    return;
                }
            }
        }
    }

    private static bool VisitValueInHand(PropertyDescriptor? descriptor, CensusWalk walk)
        => !HasValueInHand(descriptor) || walk.Visit(descriptor!._value);

    private static bool HasValueInHand(PropertyDescriptor? descriptor)
        => descriptor is not null
           && (descriptor.Flags & NoValueInHand) == PropertyFlag.None
           && descriptor._value is not null;

    private static ObjectCategory Categorize(ObjectInstance obj)
    {
        // Host bridges first: several of them are Functions, and which of the two a CLR method wrapper is
        // reported as decides whether "this engine is holding CLR state" is answerable at all.
        if (obj is IObjectWrapper or NamespaceReference or ClrFunction or DelegateWrapper or MethodInfoFunction)
        {
            return ObjectCategory.HostWrapper;
        }

        if (obj is ArrayInstance)
        {
            return ObjectCategory.Array;
        }

        if (obj is Native.Function.Function)
        {
            return ObjectCategory.Function;
        }

        // Sealed, and the only type the ordinary-object representations belong to, so this is exactly "an
        // object literal or something built like one" and never a built-in wearing a plain object's clothes.
        if (obj is JsObject)
        {
            return ObjectCategory.Plain;
        }

        return ObjectCategory.Other;
    }

    private const int CategoryCount = 5;

    private enum ObjectCategory
    {
        Plain = 0,
        Array = 1,
        Function = 2,
        HostWrapper = 3,
        Other = 4,
    }

    /// <summary>
    /// The breadth-first walk's state: what has been seen, what is still to visit, how many of each category
    /// have been counted, and the bound that ends it.
    /// </summary>
    private sealed class CensusWalk
    {
        private readonly int[] _counts = new int[CategoryCount];
        private readonly int _bound;
        private int _visited;

        internal CensusWalk(int bound)
        {
            _bound = bound;
        }

        internal HashSet<ObjectInstance> Seen { get; } = new(ReferenceComparer.Instance);

        internal Queue<ObjectInstance> Queue { get; } = new();

        /// <summary>
        /// Set once a genuinely new object was found that the bound has no room for, which is what makes the
        /// counts a lower bound rather than the whole graph.
        /// </summary>
        internal bool BoundReached { get; private set; }

        /// <summary>
        /// Counts <paramref name="value"/> if it is an object this walk has not seen. Returns whether the
        /// walk should carry on, so a caller iterating a large backing store stops as soon as the bound is
        /// hit instead of running the store out first.
        /// </summary>
        internal bool Visit(JsValue? value)
        {
            if (value is not ObjectInstance instance || !Seen.Add(instance))
            {
                return true;
            }

            if (_visited == _bound)
            {
                // Recorded here — on a new object that will not fit — rather than on reaching the bound, so
                // that a graph of exactly `bound` objects is still reported as a complete walk.
                BoundReached = true;
                return false;
            }

            _counts[(int) Categorize(instance)]++;
            _visited++;
            Queue.Enqueue(instance);
            return true;
        }

        internal ObjectCensusReport ToReport() => new(
            _bound,
            BoundReached,
            _visited,
            _counts[(int) ObjectCategory.Plain],
            _counts[(int) ObjectCategory.Array],
            _counts[(int) ObjectCategory.Function],
            _counts[(int) ObjectCategory.HostWrapper],
            _counts[(int) ObjectCategory.Other]);
    }

    /// <summary>
    /// Identity, spelled out rather than relying on <see cref="ObjectInstance"/>'s own equality: that is
    /// reference equality today, but it is reachable through a virtual a host subclass may override, and a
    /// census that silently merged two distinct objects would under-count with nothing to notice it.
    /// </summary>
    private sealed class ReferenceComparer : IEqualityComparer<ObjectInstance>
    {
        internal static readonly ReferenceComparer Instance = new();

        public bool Equals(ObjectInstance? x, ObjectInstance? y) => ReferenceEquals(x, y);

        public int GetHashCode(ObjectInstance obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
