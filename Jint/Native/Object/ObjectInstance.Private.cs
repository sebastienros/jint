using System.Runtime.CompilerServices;
using Jint.Native.Function;
using Jint.Runtime;

namespace Jint.Native.Object;

public partial class ObjectInstance
{
    // [[PrivateElements]] of a plain object — every class instance — is a packed array on the JsObject itself;
    // any other receiver (a base constructor may return an array, a function, a proxy...) keeps it off-object
    // in Engine._privateElementStore, a weak table keyed by the object, so the reference does not cost 8 bytes
    // on every ObjectInstance. Elements are found by PrivateName identity, which is what the specification's
    // PrivateElementFind compares, so a short reference scan replaces a hash lookup.

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

        AddPrivateElement(method);
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

        AddPrivateElement(new PrivateElement { Key = property, Kind = PrivateElementKind.Field, Value = value });
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
        var elements = this is JsObject plain ? plain._privateElements : StoredPrivateElements();
        if (elements is not null)
        {
            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                if (element is null)
                {
                    break;
                }

                if (ReferenceEquals(element.Key, property))
                {
                    return element;
                }
            }
        }

        return null;
    }

    private PrivateElement[]? StoredPrivateElements()
    {
        var store = _engine._privateElementStore;
        return store is not null && store.TryGetValue(this, out var holder) ? holder.Elements : null;
    }

    /// <summary>Appends to [[PrivateElements]]; the caller has checked the name is not already present.</summary>
    private void AddPrivateElement(PrivateElement element)
    {
        if (this is JsObject plain)
        {
            plain._privateElements = PrivateElementHolder.Append(plain._privateElements, element);
            return;
        }

        var store = _engine._privateElementStore ??= new();
        var holder = store.GetOrCreateValue(this);
        holder.Elements = PrivateElementHolder.Append(holder.Elements, element);
    }
}

/// <summary>The weak table's value for a receiver that is not a <see cref="JsObject"/>.</summary>
internal sealed class PrivateElementHolder
{
    public PrivateElement[]? Elements;

    /// <summary>Appends <paramref name="element"/> to a packed list, growing it when full.</summary>
    internal static PrivateElement[] Append(PrivateElement[]? elements, PrivateElement element)
    {
        if (elements is null)
        {
            return [element, null!, null!, null!];
        }

        var count = 0;
        while (count < elements.Length && elements[count] is not null)
        {
            count++;
        }

        if (count == elements.Length)
        {
            System.Array.Resize(ref elements, count * 2);
        }

        elements[count] = element;
        return elements;
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
