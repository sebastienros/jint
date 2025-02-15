using System.Runtime.CompilerServices;
using Jint.Native.Function;
using Jint.Runtime;

namespace Jint.Native.Object;

public partial class ObjectInstance
{
    private Dictionary<PrivateName, PrivateElement>? _privateElements;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-initializeinstanceelements
    /// </summary>
    internal void InitializeInstanceElements(ScriptFunction constructor)
    {
        var methods = constructor._privateMethods;
        if (methods is not null)
        {
            for (var i = 0; i < methods.Count; i++)
            {
                PrivateMethodOrAccessorAdd(methods[i]);
            }
        }

        var fields = constructor._fields;
        if (fields is not null)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                DefineField(this, fields[i]);
            }
        }
    }

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

        _privateElements ??= new Dictionary<PrivateName, PrivateElement>();
        _privateElements.Add(method.Key, method);
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

        _privateElements ??= new Dictionary<PrivateName, PrivateElement>();
        _privateElements.Add(property, new PrivateElement { Key = property, Kind = PrivateElementKind.Field, Value = value });
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-privateget
    /// </summary>
    internal JsValue PrivateGet(PrivateName property)
    {
        var entry = PrivateElementFind(property);
        if (entry is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, $"Cannot read private member #{property} from an object whose class did not declare it");
        }

        if (entry.Kind is PrivateElementKind.Field or PrivateElementKind.Method)
        {
            return entry.Value ?? Undefined;
        }

        var getter = entry.Get;
        if (getter is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, $"'#{property}' was defined without a getter");
        }

        var functionInstance = (Function.Function) getter;
        var privateGet = functionInstance._engine.Call(functionInstance, this);
        return privateGet;
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
                ExceptionHelper.ThrowTypeError(_engine.Realm, $"'#{property}' was defined without a setter");
            }

            _engine.Call(setter, this, [value]);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-privateelementfind
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PrivateElement? PrivateElementFind(PrivateName property)
    {
        return _privateElements?.TryGetValue(property, out var pe) == true ? pe : null;
    }
}

internal sealed class ClassFieldDefinition
{
    public required JsValue Name { get; set; }
    public ScriptFunction? Initializer { get; set; }
}

internal sealed class ClassStaticBlockDefinition
{
    public required Function.Function BodyFunction { get; set; }
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
