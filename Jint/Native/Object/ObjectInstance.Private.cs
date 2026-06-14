using System.Runtime.CompilerServices;
using Jint.Native.Function;
using Jint.Runtime;

namespace Jint.Native.Object;

public partial class ObjectInstance
{
    // [[PrivateElements]] is stored off-object in Engine._privateElementStore (a weak table keyed by this
    // object) so the reference doesn't cost 8 bytes on every ObjectInstance; only objects of classes that
    // declare #private members ever allocate an entry. See Engine._privateElementStore.

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

        if (!Extensible)
        {
            Throw.TypeError(_engine.Realm, "Object is not extensible");
        }

        var entry = PrivateElementFind(method.Key);
        if (entry is not null)
        {
            Throw.TypeError(_engine.Realm, "Already present");
        }

        var store = _engine._privateElementStore ??= new();
        store.GetOrCreateValue(this).Add(method.Key, method);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-privatefieldadd
    /// </summary>
    private void PrivateFieldAdd(PrivateName property, JsValue value)
    {
        // If the host is a web browser, then
        // Perform ? HostEnsureCanAddPrivateElement(O).

        if (!Extensible)
        {
            Throw.TypeError(_engine.Realm, "Object is not extensible");
        }

        var entry = PrivateElementFind(property);
        if (entry is not null)
        {
            Throw.TypeError(_engine.Realm, "Already present");
        }

        var store = _engine._privateElementStore ??= new();
        store.GetOrCreateValue(this).Add(property, new PrivateElement { Key = property, Kind = PrivateElementKind.Field, Value = value });
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-privateget
    /// </summary>
    internal JsValue PrivateGet(PrivateName property)
    {
        var entry = PrivateElementFind(property);
        if (entry is null)
        {
            Throw.TypeError(_engine.Realm, $"Cannot read private member #{property} from an object whose class did not declare it");
        }

        if (entry.Kind is PrivateElementKind.Field or PrivateElementKind.Method)
        {
            return entry.Value ?? Undefined;
        }

        var getter = entry.Get;
        if (getter is null)
        {
            Throw.TypeError(_engine.Realm, $"'#{property}' was defined without a getter");
        }

        var callable = (ICallable) getter;
        return _engine.Call(callable, this, Arguments.Empty, null);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-privateset
    /// </summary>
    internal void PrivateSet(PrivateName property, JsValue value)
    {
        var entry = PrivateElementFind(property);
        if (entry is null)
        {
            Throw.TypeError(_engine.Realm, "Not found");
        }

        if (entry.Kind == PrivateElementKind.Field)
        {
            entry.Value = value;
        }
        else if (entry.Kind == PrivateElementKind.Method)
        {
            Throw.TypeError(_engine.Realm, "Cannot set method");
        }
        else
        {
            var setter = entry.Set;
            if (setter is null)
            {
                Throw.TypeError(_engine.Realm, $"'#{property}' was defined without a setter");
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
        var store = _engine._privateElementStore;
        if (store is not null && store.TryGetValue(this, out var elements))
        {
            return elements.TryGetValue(property, out var pe) ? pe : null;
        }

        return null;
    }
}

internal sealed class ClassFieldDefinition
{
    public required JsValue Name { get; set; }
    public Function.Function? Initializer { get; set; }
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
