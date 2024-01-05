using System.Diagnostics;
using Jint.Native.Array;

namespace Jint.Native;

[DebuggerTypeProxy(typeof(JsArrayDebugView))]
[DebuggerDisplay("Count = {Length}")]
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

    public uint Length => GetLength();

    private sealed class JsArrayDebugView
    {
        private readonly JsArray _array;

        public JsArrayDebugView(JsArray array)
        {
            _array = array;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public JsValue[] Values
        {
            get
            {
                var values = new JsValue[_array.GetLength()];
                var i = 0;
                foreach (var value in _array)
                {
                    values[i++] = value;
                }
                return values;
            }
        }
    }
}
