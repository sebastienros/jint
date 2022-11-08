using System.Runtime.CompilerServices;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// Fast access helpers which violate JavaScript specification, but helpful when accessed
/// against ObjectInstance and prototype chain is intact.
/// </summary>
public partial class ObjectInstance
{
    /// <summary>
    /// Creates data property without checking prototype, property existence and overwrites any data already present.
    /// </summary>
    /// <remarks>
    /// Does not conform to JavaScript specification prototype etc. handling etc.
    /// </remarks>
    public void FastSetProperty(string name, PropertyDescriptor value)
    {
        SetProperty(name, value);
    }

    /// <summary>
    /// Creates data property without checking prototype, property existence and overwrites any data already present.
    /// </summary>
    /// <remarks>
    /// Does not conform to JavaScript specification prototype etc. handling etc.
    /// </remarks>
    public void FastSetProperty(JsValue property, PropertyDescriptor value)
    {
        SetProperty(property, value);
    }

    /// <summary>
    /// Creates data property (configurable, enumerable, writable) without checking prototype, property existence and overwrites any data already present.
    /// </summary>
    /// <remarks>
    /// Does not conform to JavaScript specification prototype etc. handling etc.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastSetDataProperty(string name, JsValue value)
    {
        SetProperty(name, new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable));
    }
}
