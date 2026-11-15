using System.Diagnostics;
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
    /// <remarks>
    /// A class constructor always has a private-methods list, if only an empty one, and an ordinary function has
    /// neither list. A class whose decorators added instance initializers is the one case with a private-methods list
    /// and no fields list. Its list is a <see cref="DecoratedInstanceElements"/>, which carries the fields along with
    /// those initializers. So the decorated case is told apart by the null tests this method already made, and an
    /// undecorated class runs exactly what it ran before (sebastienros/jint#4421).
    /// </remarks>
    internal void InitializeInstanceElements(ScriptFunction constructor)
    {
        var methods = constructor._privateMethods;
        if (methods is null)
        {
            Debug.Assert(constructor._fields is null, "a constructor with fields has a private-methods list");
            return;
        }

        for (var i = 0; i < methods.Count; i++)
        {
            PrivateMethodOrAccessorAdd(methods[i]);
        }

        var fields = constructor._fields;
        if (fields is null)
        {
            InitializeDecoratedInstanceElements(methods);
            return;
        }

        for (var i = 0; i < fields.Count; i++)
        {
            DefineField(this, fields[i]);
        }
    }

    /// <summary>
    /// The rest of InitializeInstanceElements (https://github.com/tc39/ecma262/pull/2417) for a class whose decorators
    /// added instance initializers, once its private methods are added. First the extra initializers of the methods',
    /// getters' and setters' decorators run. Then each field and accessor is initialized in source order, and
    /// InitializeFieldOrAccessor runs that element's own extra initializers right after defining it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitializeDecoratedInstanceElements(List<PrivateElement> privateMethods)
    {
        var elements = (DecoratedInstanceElements) privateMethods;
        RunExtraInitializers(elements.MethodExtraInitializers);

        var fields = elements.Fields;
        for (var i = 0; i < fields.Length; i++)
        {
            DefineField(this, fields[i].Definition);
            RunExtraInitializers(fields[i].ExtraInitializers);
        }
    }

    /// <summary>
    /// Calls each of <paramref name="initializers"/> with this object as its this, in order.
    /// </summary>
    private void RunExtraInitializers(ICallable[]? initializers)
    {
        if (initializers is null)
        {
            return;
        }

        for (var i = 0; i < initializers.Length; i++)
        {
            _engine.Call(initializers[i], this, Arguments.Empty, expression: null);
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

/// <summary>
/// The instance half of a class whose decorators added instance initializers: the [[Elements]] and [[Initializers]]
/// of its constructor (https://github.com/tc39/ecma262/pull/2417). It is the constructor's private-methods list, so it
/// holds the private methods as any class's list does, and the constructor keeps no fields list.
/// </summary>
/// <remarks>
/// It derives from the list so that <see cref="ObjectInstance.InitializeInstanceElements"/> finds it with the reads and
/// null tests it already makes. A field of its own on every function would also cost every closure eight bytes.
/// </remarks>
internal sealed class DecoratedInstanceElements : List<PrivateElement>
{
    public DecoratedInstanceElements(List<PrivateElement>? privateMethods, ICallable[]? methodExtraInitializers, DecoratedInstanceField[] fields)
    {
        if (privateMethods is not null)
        {
            AddRange(privateMethods);
        }

        MethodExtraInitializers = methodExtraInitializers;
        Fields = fields;
    }

    /// <summary>
    /// [[Initializers]]: what the decorators of the instance methods, getters and setters passed to
    /// <c>addInitializer</c>, in the order they passed it; null for none.
    /// </summary>
    public ICallable[]? MethodExtraInitializers { get; }

    /// <summary>
    /// The instance fields and accessors' backing fields, in source order.
    /// </summary>
    public DecoratedInstanceField[] Fields { get; }
}

/// <summary>
/// An instance field or accessor of a class with <see cref="DecoratedInstanceElements"/>. It holds the definition
/// <see cref="ObjectInstance.DefineField"/> initializes the field from and the field's [[ExtraInitializers]]: what its
/// decorators passed to <c>addInitializer</c>, null for none.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DecoratedInstanceField(ClassFieldDefinition Definition, ICallable[]? ExtraInitializers);

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
