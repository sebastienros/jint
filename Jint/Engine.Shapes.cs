using System.Runtime.CompilerServices;
using Jint.Native.Object;

namespace Jint;

public partial class Engine
{
    // Empty root shapes, keyed by prototype, so all plain objects with the same prototype share one
    // transition tree (and therefore share Shape instances for identical layouts). Keyed weakly so a
    // prototype's shapes are released with it; the table is only consulted on a cold path (an
    // object-literal site building its shape for the first time, or for a new prototype), because the
    // resulting leaf shape is cached on the AST node.
    private ConditionalWeakTable<ObjectInstance, Shape>? _emptyShapes;

    internal Shape GetEmptyShape(ObjectInstance prototype)
    {
        _emptyShapes ??= new ConditionalWeakTable<ObjectInstance, Shape>();
        return _emptyShapes.GetValue(prototype, static _ => new Shape());
    }
}
