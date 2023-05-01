using System;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.References;

/// <summary>
/// https://tc39.es/ecma262/#sec-reference-record-specification-type
/// </summary>
public sealed class Reference
{
    private JsValue _base;
    private JsValue _referencedName;
    internal bool _strict;
    private JsValue? _thisValue;

    internal Reference(JsValue baseValue, JsValue referencedName, bool strict, JsValue? thisValue = null)
    {
        _base = baseValue;
        _referencedName = referencedName;
        _thisValue = thisValue;
    }

    /// <summary>
    /// The value or Environment Record which holds the binding. A [[Base]] of unresolvable indicates that the binding could not be resolved.
    /// </summary>
    public JsValue Base
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _base;
    }

    /// <summary>
    /// The value or Environment Record which holds the binding. A [[Base]] of unresolvable indicates that the binding could not be resolved.
    /// </summary>
    public JsValue ReferencedName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _referencedName;
    }

    /// <summary>
    /// true if the Reference Record originated in strict mode code, false otherwise.
    /// </summary>
    public bool Strict
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _strict;
    }

    public bool HasPrimitiveBase
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_base._type & InternalTypes.Primitive) != 0;
    }

    public bool IsUnresolvableReference
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _base._type == InternalTypes.Undefined;
    }

    public bool IsSuperReference => _thisValue is not null;

    // https://tc39.es/ecma262/#sec-ispropertyreference

    public bool IsPropertyReference
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_base._type & (InternalTypes.Primitive | InternalTypes.Object)) != 0;
    }

    public JsValue ThisValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsSuperReference ? _thisValue! : Base;
    }

    public bool IsPrivateReference
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _referencedName._type == InternalTypes.PrivateName;
    }

    internal Reference Reassign(JsValue baseValue, JsValue name, bool strict, JsValue? thisValue)
    {
        _base = baseValue;
        _referencedName = name;
        _strict = strict;
        _thisValue = thisValue;

        return this;
    }

    internal void AssertValid(Realm realm)
    {
        if (_strict
            && (_base._type & InternalTypes.ObjectEnvironmentRecord) != 0
            && (_referencedName == CommonProperties.Eval || _referencedName == CommonProperties.Arguments))
        {
            ExceptionHelper.ThrowSyntaxError(realm);
        }
    }

    internal void InitializeReferencedBinding(JsValue value)
    {
        ((EnvironmentRecord) _base).InitializeBinding(TypeConverter.ToString(_referencedName), value);
    }
}
