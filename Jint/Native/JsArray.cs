using Jint.Native.Array;

namespace Jint.Native;

public sealed class JsArray : ArrayInstance
{
    /// <summary>
    /// Creates a new array instance with defaults.
    /// </summary>
    /// <param name="engine">The engine that this array is bound to.</param>
    /// <param name="capacity">The initial size of underlying data structure, if you know you're going to add N items, provide N.</param>
    /// <param name="length">Sets the length of the array.</param>
    public JsArray(Engine engine, uint capacity = 0, uint length = 0) : base(engine, capacity, length)
    {
    }

    /// <summary>
    /// Possibility to construct valid array fast.
    /// The array will be owned and modified by Jint afterwards.
    /// </summary>
    public JsArray(Engine engine, JsValue[] items) : base(engine, items)
    {
    }
}
