using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;

namespace Jint;

public partial class Engine
{
    // Per-prototype hidden-class state, keyed weakly so a prototype's shapes are released with it. The
    // table is only consulted on a cold path (an object-literal site building its shape for the first
    // time, a new prototype, or a host layout that has not been resolved against this engine yet),
    // because the resulting leaf shape is cached on the AST node / in the per-prototype layout memo.
    private ConditionalWeakTable<ObjectInstance, ShapeRoot>? _shapeRoots;

    // Bounds how many NEW transition nodes host-driven object construction (JsObject.Create /
    // JsObject.CreateFromEntries) may intern over this engine's lifetime. A transition tree is pinned by
    // its prototype, which for Object.prototype means the engine's lifetime, so a host that feeds
    // wildly varying key sets — document field names, user-defined columns — must not be able to grow
    // it without bound. Reused transitions (the whole point: identically laid-out items share a shape)
    // cost nothing, so only genuinely new layouts consume the budget. Once exhausted, host construction
    // keeps working but produces ordinary dictionary-mode objects.
    //
    // Note this is the ONLY bound on the layout path: JsObjectLayout caps a single layout at
    // Shape.MaxShapeProperties, but resolving a layout walks Shape.Add directly (as object literals do)
    // and therefore does not go through TryShapeAdd's MaxFanout guard.
    internal const int HostShapeTransitionBudget = 16 * 1024;
    private int _hostShapeTransitions;

    internal Shape GetEmptyShape(ObjectInstance prototype) => GetShapeRoot(prototype).Empty;

    private ShapeRoot GetShapeRoot(ObjectInstance prototype)
    {
        _shapeRoots ??= new ConditionalWeakTable<ObjectInstance, ShapeRoot>();
        return _shapeRoots.GetValue(prototype, static _ => new ShapeRoot());
    }

    /// <summary>
    /// True while host-driven construction may still intern new hidden-class transitions; see
    /// <see cref="HostShapeTransitionBudget"/>. Callers that grow a shape incrementally check this once
    /// per object and then charge each newly interned transition with
    /// <see cref="ChargeHostShapeTransition"/>; an object already under construction is allowed to
    /// finish shaped (the overshoot is bounded by <see cref="Shape.MaxShapeProperties"/>).
    /// </summary>
    internal bool HostShapeBudgetAvailable => _hostShapeTransitions < HostShapeTransitionBudget;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ChargeHostShapeTransition() => _hostShapeTransitions++;

    /// <summary>
    /// Resolves a host-declared <see cref="JsObjectLayout"/> to the interned leaf <see cref="Shape"/> for
    /// <paramref name="prototype"/> in this engine, memoizing the result so every later call with the same
    /// layout is two weak-table probes — the prototype's <see cref="ShapeRoot"/>, then that root's layout
    /// memo — and nothing else. Returns <c>null</c> when the layout is not memoized yet and the
    /// engine's host transition budget cannot cover it, in which case the caller builds an ordinary
    /// dictionary-mode object instead.
    /// <para>
    /// Resolution is necessarily per engine: the empty root shape is interned per (engine, prototype) and
    /// the whole transition tree hangs off it, so one process-shared layout maps to a different
    /// <see cref="Shape"/> in every engine that uses it.
    /// </para>
    /// </summary>
    internal Shape? TryGetLayoutShape(ObjectInstance prototype, JsObjectLayout layout)
    {
        var root = GetShapeRoot(prototype);
        if (root.TryGetLayoutShape(layout, out var memoized))
        {
            return memoized;
        }

        var count = layout.Count;
        if (_hostShapeTransitions + count > HostShapeTransitionBudget)
        {
            return null;
        }

        // Conservatively charge the whole layout rather than only the transitions the build actually
        // interns: a layout that shares a prefix with an existing one is over-charged, which only makes
        // the bound stricter, and it keeps the memo miss a single pass.
        _hostShapeTransitions += count;
        return root.GetOrBuildLayoutShape(layout);
    }

    /// <summary>
    /// The hidden-class state anchored to one prototype: its empty root shape (from which the whole
    /// transition tree grows) plus the memo of host layouts already resolved against that root.
    /// <para>
    /// Lifetime: this object is the value of a <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed by the
    /// prototype, so a dropped prototype takes its root, its transition tree and its layout memo with it.
    /// The memo is itself weakly keyed by <see cref="JsObjectLayout"/>, so a dropped layout releases its
    /// entry; and a <see cref="Shape"/> references only its parent chain and its key strings — never a
    /// prototype, an engine or a layout — so a memo entry pins nothing beyond the layout it describes.
    /// </para>
    /// </summary>
    private sealed class ShapeRoot
    {
        internal readonly Shape Empty = new Shape();

        private ConditionalWeakTable<JsObjectLayout, Shape>? _layoutShapes;
        private ConditionalWeakTable<JsObjectLayout, Shape>.CreateValueCallback? _build;

        internal bool TryGetLayoutShape(JsObjectLayout layout, out Shape? shape)
        {
            var table = _layoutShapes;
            if (table is null)
            {
                shape = null;
                return false;
            }

            return table.TryGetValue(layout, out shape);
        }

        internal Shape GetOrBuildLayoutShape(JsObjectLayout layout)
        {
            var table = _layoutShapes ??= new ConditionalWeakTable<JsObjectLayout, Shape>();
            // Memoize the callback so the common (hit) path allocates nothing and the miss path allocates
            // one delegate per prototype rather than one per call.
            return table.GetValue(layout, _build ??= BuildLayoutShape);
        }

        private Shape BuildLayoutShape(JsObjectLayout layout)
        {
            var shape = Empty;
            var keys = layout.Keys;
            for (var i = 0; i < keys.Length; i++)
            {
                shape = shape.Add(in keys[i]);
            }

            return shape;
        }
    }
}
