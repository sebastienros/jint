using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime.Interop;

#pragma warning disable IL2072

internal sealed class ClrHelper
{
    // Snapshot, like every other engine-visible reading of this option: it is settled once the engine's
    // configuration callbacks have run (this type is built right after them) and mutating it afterwards is
    // not a supported pattern - member resolution could not honour a later change either, since the accessor
    // cache is partitioned by the value captured at construction.
    private readonly bool _allowGetType;
    private readonly ClrTypeResolutionPolicy _typeResolutionPolicy;
    private readonly ConditionalWeakTable<ObjectWrapper, TypeReference> _explicitTypeCapabilities = new();

    internal ClrHelper(Options.InteropOptions interopOptions, ClrTypeResolutionPolicy typeResolutionPolicy)
    {
        _allowGetType = interopOptions.AllowGetType;
        _typeResolutionPolicy = typeResolutionPolicy;
    }

    /// <summary>
    /// Call JsValue.ToString(), mainly for NamespaceReference.
    /// </summary>
#pragma warning disable CA1822
    public JsValue ToString(JsValue value)
#pragma warning restore CA1822
    {
        return value.ToString();
    }

    /// <summary>
    /// Cast `obj as ISomeInterface` to `obj`
    /// </summary>
#pragma warning disable CA1822
    public JsValue Unwrap(ObjectWrapper obj)
#pragma warning restore CA1822
    {
        MustAllowGetType();
        MustAllowType(obj.Target.GetType());
        return ObjectWrapper.Create(obj.Engine, obj.Target);
    }

    /// <summary>
    /// Cast `obj` to `obj as ISomeInterface`
    /// </summary>
#pragma warning disable CA1822
    public JsValue Wrap(ObjectWrapper obj, TypeReference type)
#pragma warning restore CA1822
    {
        if (!type.ReferenceType.IsInstanceOfType(obj.Target))
        {
            Throw.TypeError(type.Engine.Realm, "Argument obj must be an instance of type");
        }
        return ObjectWrapper.Create(obj.Engine, obj.Target, type.ReferenceType);
    }

    /// <summary>
    /// Get `TypeReference(ISomeInterface)` from `obj as ISomeInterface`
    /// </summary>
    public JsValue TypeOf(ObjectWrapper obj)
    {
        MustAllowGetType();
        MustAllowType(obj.ClrType);
        return TypeReference.CreateTypeReference(obj.Engine, obj.ClrType);
    }

    /// <summary>
    /// Cast `TypeReference(SomeClass)` to `ObjectWrapper(SomeClass)`
    /// </summary>
    public JsValue TypeToObject(TypeReference type)
    {
        MustAllowGetType();
        var engine = type.Engine;
        var wrapped = engine.Options.Interop.WrapObjectHandler.Invoke(engine, type.ReferenceType, null);
        if (wrapped is ObjectWrapper objectWrapper)
        {
            _explicitTypeCapabilities.Remove(objectWrapper);
            _explicitTypeCapabilities.Add(objectWrapper, type);
        }
        return wrapped ?? JsValue.Undefined;
    }

    /// <summary>
    /// Cast `ObjectWrapper(SomeClass)` to `TypeReference(SomeClass)`
    /// </summary>
    public JsValue ObjectToType(ObjectWrapper obj)
    {
        MustAllowGetType();
        if (obj.Target is Type t)
        {
            if (_explicitTypeCapabilities.TryGetValue(obj, out var explicitType)
                && ReferenceEquals(explicitType.ReferenceType, t))
            {
                return explicitType;
            }

            MustAllowType(t);
            return TypeReference.CreateTypeReference(obj.Engine, t);
        }

        Throw.ArgumentException("Must be an ObjectWrapper of Type", nameof(obj));
        return JsValue.Undefined;
    }

    private void MustAllowGetType()
    {
        if (!_allowGetType)
        {
            Throw.InvalidOperationException("Invalid when Engine.Options.Interop.AllowGetType == false");
        }
    }

    private void MustAllowType(Type type)
    {
        if (!_typeResolutionPolicy.Allows(type))
        {
            Throw.InvalidOperationException("The CLR type is not allowed by the engine's namespace resolution policy");
        }
    }
}
