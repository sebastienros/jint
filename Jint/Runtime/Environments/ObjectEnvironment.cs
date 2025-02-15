using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Environments;

/// <summary>
/// Represents an object environment record
/// https://tc39.es/ecma262/#sec-object-environment-records
/// </summary>
internal sealed class ObjectEnvironment : Environment
{
    internal readonly ObjectInstance _bindingObject;
    private readonly bool _provideThis;
    private readonly bool _withEnvironment;

    public ObjectEnvironment(
        Engine engine,
        ObjectInstance bindingObject,
        bool provideThis,
        bool withEnvironment) : base(engine)
    {
        _bindingObject = bindingObject;
        _provideThis = provideThis;
        _withEnvironment = withEnvironment;
    }

    internal override bool HasBinding(Key name) => HasBinding(JsString.Create(name.Name));

    internal override bool HasBinding(BindingName name) => HasBinding(name.Value);

    private bool HasBinding(JsString nameValue)
    {
        var foundBinding = _bindingObject.HasProperty(nameValue);

        if (!foundBinding)
        {
            return false;
        }

        if (!_withEnvironment)
        {
            return true;
        }

        return !IsBlocked(nameValue);
    }

    internal override bool TryGetBinding(BindingName name, bool strict, [NotNullWhen(true)] out JsValue? value)
    {
        // we unwrap by name
        if (!_bindingObject.HasProperty(name.Value))
        {
            value = default;
            return false;
        }

        if (_withEnvironment && IsBlocked(name.Value))
        {
            value = default;
            return false;
        }

        if (!_bindingObject.HasProperty(name.Value))
        {
            if (strict)
            {
                // data was deleted during reading of unscopable information, of course...
                ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name.Key);
            }
        }

        value = _bindingObject.Get(name.Value);

        return true;
    }

    private bool IsBlocked(JsValue property)
    {
        var unscopables = _bindingObject.Get(GlobalSymbolRegistry.Unscopables);
        if (unscopables is ObjectInstance oi)
        {
            var blocked = TypeConverter.ToBoolean(oi.Get(property));
            if (blocked)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/6.0/#sec-object-environment-records-createmutablebinding-n-d
    /// </summary>
    internal override void CreateMutableBinding(Key name, bool canBeDeleted = false)
    {
        _bindingObject.DefinePropertyOrThrow(name.Name, new PropertyDescriptor(Undefined, canBeDeleted
            ? PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.MutableBinding
            : PropertyFlag.NonConfigurable | PropertyFlag.MutableBinding));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object-environment-records-createimmutablebinding-n-s
    /// </summary>
    internal override void CreateImmutableBinding(Key name, bool strict = true)
    {
        ExceptionHelper.ThrowInvalidOperationException("The concrete Environment Record method CreateImmutableBinding is never used within this specification in association with Object Environment Records.");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object-environment-records-initializebinding-n-v
    /// </summary>
    internal override void InitializeBinding(Key name, JsValue value) => SetMutableBinding(name, value, strict: false);

    internal override void SetMutableBinding(Key name, JsValue value, bool strict)
    {
        var jsString = new JsString(name);
        if (!_bindingObject.HasProperty(jsString))
        {
            if (strict)
            {
                ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name);
            }
        }

        _bindingObject.Set(jsString, value, strict);
    }

    internal override void SetMutableBinding(BindingName name, JsValue value, bool strict)
    {
        if (!_bindingObject.HasProperty(name.Value))
        {
            if (strict)
            {
                ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name.Key);
            }
        }

        _bindingObject.Set(name.Value, value, strict);
    }

    internal override JsValue GetBindingValue(Key name, bool strict)
    {
        if (!_bindingObject.HasProperty(name.Name))
        {
            if (strict)
            {
                ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name.Name);
            }
        }

        return _bindingObject.Get(name.Name);
    }

    internal override bool DeleteBinding(Key name) => _bindingObject.Delete(name.Name);

    internal override bool HasThisBinding() => false;

    internal override bool HasSuperBinding() => false;

    internal override JsValue WithBaseObject() => _withEnvironment ? _bindingObject : Undefined;

    internal override bool HasBindings() => _bindingObject._properties?.Count > 0;

    internal override string[] GetAllBindingNames()
    {
        if (_bindingObject is not null)
        {
            var names = new List<string>(_bindingObject._properties?.Count ?? 0);
            foreach (var name in _bindingObject.GetOwnProperties())
            {
                names.Add(name.Key.ToString());
            }
            return names.ToArray();
        }

        return [];
    }

    public override bool Equals(JsValue? other)
    {
        return ReferenceEquals(_bindingObject, other);
    }

    internal override JsValue GetThisBinding()
    {
        throw new NotImplementedException();
    }
}
