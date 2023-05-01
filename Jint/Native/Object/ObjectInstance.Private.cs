using System.Runtime.CompilerServices;
using Jint.Native.Function;
using Jint.Runtime;

namespace Jint.Native.Object;

public partial class ObjectInstance
{
    private Dictionary<string, PrivateElement>? _privateElements;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-privatemethodoraccessoradd
    /// </summary>
    internal void PrivateMethodOrAccessorAdd(PrivateElement method)
    {
        // If the host is a web browser, then
        // Perform ? HostEnsureCanAddPrivateElement(O).

        var entry = PrivateElementFind(method.Key);
        if (entry is not null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "Already present");
        }

        _privateElements ??= new Dictionary<string, PrivateElement>();
        _privateElements[method.Key.ToString()] = method;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-privatefieldadd
    /// </summary>
    private void PrivateFieldAdd(PrivateName property, JsValue value)
    {
        // If the host is a web browser, then
        // Perform ? HostEnsureCanAddPrivateElement(O).

        var entry = PrivateElementFind(property);
        if (entry is not null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "Already present");
        }

        _privateElements ??= new Dictionary<string, PrivateElement>();
        _privateElements.Add(property.ToString(), new PrivateElement { Key = property, Kind = PrivateElementKind.Field, Value = value });
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-privateget
    /// </summary>
    internal JsValue PrivateGet(PrivateName property)
    {
        var entry = PrivateElementFind(property);
        if (entry is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "Not found");
        }

        if (entry.Kind is PrivateElementKind.Field or PrivateElementKind.Method)
        {
            return entry.Value ?? Undefined;
        }

        var getter = entry.Get;
        if (getter is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "Getter missing");
        }

        return getter.Call(this);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-privateset
    /// </summary>
    internal void PrivateSet(PrivateName property, JsValue value)
    {
        var entry = PrivateElementFind(property);
        if (entry is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "Not found");
        }

        if (entry.Kind == PrivateElementKind.Field)
        {
            entry.Value = value;
        }
        else if (entry.Kind == PrivateElementKind.Method)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "Cannot set method");
        }
        else
        {
            var setter = entry.Set;
            if (setter is null)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm, "Setter missing");
            }

            setter.Call(setter, new[] { value });
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-privateelementfind
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PrivateElement? PrivateElementFind(PrivateName property)
    {
        return _privateElements?.TryGetValue(property.ToString(), out var pe) == true ? pe : null;
    }
}


internal sealed class ClassFieldDefinition
{
    public required JsValue Name { get; set; }
    public FunctionInstance? Initializer { get; set; }
}

internal sealed class ClassStaticBlockDefinition
{
    public required FunctionInstance BodyFunction { get; set; }
}

internal sealed class PrivateElement
{
    public required PrivateName Key { get; set; }
    public PrivateElementKind Kind { get; set; }
    public JsValue? Value { get; set; }
    public JsValue? Get { get; set; }
    public JsValue? Set { get; set; }
}

internal enum PrivateElementKind
{
    Field,
    Method,
    Accessor
}
