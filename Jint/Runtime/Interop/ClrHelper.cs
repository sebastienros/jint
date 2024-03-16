using Jint.Native;

namespace Jint.Runtime.Interop;

#pragma warning disable IL2072

internal sealed class ClrHelper
{
    private readonly Options.InteropOptions _interopOptions;

    internal ClrHelper(Options.InteropOptions interopOptions)
    {
        _interopOptions = interopOptions;
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
            ExceptionHelper.ThrowTypeError(type.Engine.Realm, "Argument obj must be an instance of type");
        }
        return ObjectWrapper.Create(obj.Engine, obj.Target, type.ReferenceType);
    }

    /// <summary>
    /// Get `TypeReference(ISomeInterface)` from `obj as ISomeInterface`
    /// </summary>
    public JsValue TypeOf(ObjectWrapper obj)
    {
        MustAllowGetType();
        return TypeReference.CreateTypeReference(obj.Engine, obj.ClrType);
    }

    /// <summary>
    /// Cast `TypeReference(SomeClass)` to `ObjectWrapper(SomeClass)`
    /// </summary>
    public JsValue TypeToObject(TypeReference type)
    {
        MustAllowGetType();
        var engine = type.Engine;
        return engine.Options.Interop.WrapObjectHandler.Invoke(engine, type.ReferenceType, null) ?? JsValue.Undefined;
    }

    /// <summary>
    /// Cast `ObjectWrapper(SomeClass)` to `TypeReference(SomeClass)`
    /// </summary>
    public JsValue ObjectToType(ObjectWrapper obj)
    {
        MustAllowGetType();
        if (obj.Target is Type t)
        {
            return TypeReference.CreateTypeReference(obj.Engine, t);
        }

        ExceptionHelper.ThrowArgumentException("Must be an ObjectWrapper of Type", nameof(obj));
        return JsValue.Undefined;
    }

    private void MustAllowGetType()
    {
        if (!_interopOptions.AllowGetType)
        {
            ExceptionHelper.ThrowInvalidOperationException("Invalid when Engine.Options.Interop.AllowGetType == false");
        }
    }
}
