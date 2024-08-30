using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Environments;

/// <summary>
/// https://tc39.es/ecma262/#sec-global-environment-records
/// </summary>
internal sealed class GlobalEnvironment : Environment
{
    /// <summary>
    /// A sealed class for global usage.
    /// </summary>
    internal sealed class GlobalDeclarativeEnvironment : DeclarativeEnvironment
    {
        public GlobalDeclarativeEnvironment(Engine engine) : base(engine)
        {
        }
    }

    internal readonly ObjectInstance _global;

    // we expect it to be GlobalObject, but need to allow to something host-defined, like Window
    private readonly GlobalObject? _globalObject;

    // Environment records are needed by debugger
    internal readonly GlobalDeclarativeEnvironment _declarativeRecord;

    public GlobalEnvironment(Engine engine, ObjectInstance global) : base(engine)
    {
        _global = global;
        _globalObject = global as GlobalObject;
        _declarativeRecord = new GlobalDeclarativeEnvironment(engine);
    }

    public ObjectInstance GlobalThisValue => _global;

    internal override bool HasBinding(Key name)
    {
        if (_declarativeRecord.HasBinding(name))
        {
            return true;
        }

        if (_globalObject is not null)
        {
            return _globalObject.HasProperty(name);
        }

        return _global.HasProperty(new JsString(name));
    }

    internal override bool HasBinding(BindingName name)
    {
        if (_declarativeRecord.HasBinding(name))
        {
            return true;
        }

        if (_globalObject is not null)
        {
            return _globalObject.HasProperty(name.Key);
        }

        return _global.HasProperty(name.Value);
    }

    internal override bool TryGetBinding(BindingName name, bool strict, [NotNullWhen(true)] out JsValue? value)
    {
        if (_declarativeRecord._dictionary is not null && _declarativeRecord.TryGetBinding(name, strict, out value))
        {
            return true;
        }

        // we unwrap by name
        value = default;

        // normal case is to find
        if (_global._properties!._dictionary.TryGetValue(name.Key, out var property)
            && property != PropertyDescriptor.Undefined)
        {
            value = ObjectInstance.UnwrapJsValue(property, _global);
            return true;
        }

        if (_global._prototype is not null)
        {
            return TryGetBindingForGlobalParent(name, out value);
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryGetBindingForGlobalParent(
        BindingName name,
        [NotNullWhen(true)] out JsValue? value)
    {
        value = default;

        var parent = _global._prototype!;
        var property = parent.GetOwnProperty(name.Value);

        if (property == PropertyDescriptor.Undefined)
        {
            return false;
        }

        value = ObjectInstance.UnwrapJsValue(property, _global);
        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-global-environment-records-createmutablebinding-n-d
    /// </summary>
    internal override void CreateMutableBinding(Key name, bool canBeDeleted = false)
    {
        if (_declarativeRecord.HasBinding(name))
        {
            ThrowAlreadyDeclaredException(name);
        }

        _declarativeRecord.CreateMutableBinding(name, canBeDeleted);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-global-environment-records-createimmutablebinding-n-s
    /// </summary>
    internal override void CreateImmutableBinding(Key name, bool strict = true)
    {
        if (_declarativeRecord.HasBinding(name))
        {
            ThrowAlreadyDeclaredException(name);
        }

        _declarativeRecord.CreateImmutableBinding(name, strict);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowAlreadyDeclaredException(Key name)
    {
        ExceptionHelper.ThrowTypeError(_engine.Realm, $"{name} has already been declared");
    }

    internal override void InitializeBinding(Key name, JsValue value)
    {
        if (_declarativeRecord.HasBinding(name))
        {
            _declarativeRecord.InitializeBinding(name, value);
        }
        else
        {
            _global._properties![name].Value = value;
        }
    }

    internal override void SetMutableBinding(Key name, JsValue value, bool strict)
    {
        if (_declarativeRecord.HasBinding(name))
        {
            _declarativeRecord.SetMutableBinding(name, value, strict);
        }
        else
        {
            if (_globalObject is not null)
            {
                // fast inlined path as we know we target global
                if (!_globalObject.SetFromMutableBinding(name, value, strict) && strict)
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm);
                }
            }
            else
            {
                SetMutableBindingUnlikely(name, value, strict);
            }
        }
    }

    internal override void SetMutableBinding(BindingName name, JsValue value, bool strict)
    {
        if (_declarativeRecord.HasBinding(name))
        {
            _declarativeRecord.SetMutableBinding(name, value, strict);
        }
        else
        {
            if (_globalObject is not null)
            {
                // fast inlined path as we know we target global
                if (!_globalObject.SetFromMutableBinding(name.Key, value, strict) && strict)
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm);
                }
            }
            else
            {
                SetMutableBindingUnlikely(name.Key, value, strict);
            }
        }
    }

    private void SetMutableBindingUnlikely(Key name, JsValue value, bool strict)
    {
        // see ObjectEnvironmentRecord.SetMutableBinding
        var jsString = new JsString(name.Name);
        if (strict && !_global.HasProperty(jsString))
        {
            ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name);
        }

        _global.Set(jsString, value);
    }

    internal override JsValue GetBindingValue(Key name, bool strict)
    {
        if (_declarativeRecord.HasBinding(name))
        {
            return _declarativeRecord.GetBindingValue(name, strict);
        }

        // see ObjectEnvironmentRecord.GetBindingValue
        var desc = PropertyDescriptor.Undefined;
        if (_globalObject is not null)
        {
            if (_globalObject._properties?.TryGetValue(name, out desc) == false)
            {
                desc = PropertyDescriptor.Undefined;
            }
        }
        else
        {
            desc = _global.GetOwnProperty(name.Name);
        }

        if (strict && desc == PropertyDescriptor.Undefined)
        {
            ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name);
        }

        return ObjectInstance.UnwrapJsValue(desc, _global);
    }

    internal override bool DeleteBinding(Key name)
    {
        if (_declarativeRecord.HasBinding(name))
        {
            return _declarativeRecord.DeleteBinding(name);
        }

        var n = JsString.Create(name.Name);
        if (_global.HasOwnProperty(n))
        {
            return _global.Delete(n);
        }

        return true;
    }

    internal override bool HasThisBinding() => true;

    internal override bool HasSuperBinding() => false;

    internal override JsValue WithBaseObject() => Undefined;

    internal override JsValue GetThisBinding() => _global;

    internal bool HasLexicalDeclaration(Key name) => _declarativeRecord.HasBinding(name);

    internal bool HasRestrictedGlobalProperty(Key name)
    {
        if (_globalObject is not null)
        {
            return _globalObject._properties?.TryGetValue(name, out var desc) == true
                   && !desc.Configurable;
        }

        var existingProp = _global.GetOwnProperty(name.Name);
        if (existingProp == PropertyDescriptor.Undefined)
        {
            return false;
        }

        return !existingProp.Configurable;
    }

    public bool CanDeclareGlobalVar(Key name)
    {
        if (_global._properties!.ContainsKey(name))
        {
            return true;
        }

        return _global.Extensible;
    }

    public bool CanDeclareGlobalFunction(Key name)
    {
        if (!_global._properties!.TryGetValue(name, out var existingProp)
            || existingProp == PropertyDescriptor.Undefined)
        {
            return _global.Extensible;
        }

        if (existingProp.Configurable)
        {
            return true;
        }

        if (existingProp.IsDataDescriptor() && existingProp.Writable && existingProp.Enumerable)
        {
            return true;
        }

        return false;
    }

    public void CreateGlobalVarBinding(Key name, bool canBeDeleted)
    {
        if (!_global.Extensible)
        {
            return;
        }

        _global._properties!.TryAdd(name, new PropertyDescriptor(Undefined, canBeDeleted
            ? PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.MutableBinding
            : PropertyFlag.NonConfigurable | PropertyFlag.MutableBinding));
    }

    internal void CreateGlobalVarBindings(List<Key> names, bool canBeDeleted)
    {
        if (!_global.Extensible)
        {
            return;
        }

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];

            _global._properties!.TryAdd(name, new PropertyDescriptor(Undefined, canBeDeleted
                ? PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.MutableBinding
                : PropertyFlag.NonConfigurable | PropertyFlag.MutableBinding));
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createglobalfunctionbinding
    /// </summary>
    public void CreateGlobalFunctionBinding(Key name, JsValue value, bool canBeDeleted)
    {
        var jsString = new JsString(name);
        var existingProp = _global.GetOwnProperty(jsString);

        PropertyDescriptor desc;
        if (existingProp == PropertyDescriptor.Undefined || existingProp.Configurable)
        {
            desc = new PropertyDescriptor(value, true, true, canBeDeleted);
        }
        else
        {
            desc = new PropertyDescriptor(value, PropertyFlag.None);
        }

        _global.DefinePropertyOrThrow(jsString, desc);
        _global.Set(jsString, value, false);
    }

    internal override bool HasBindings()
    {
        return _declarativeRecord.HasBindings() || _globalObject?._properties?.Count > 0 || _global._properties?.Count > 0;
    }

    internal override string[] GetAllBindingNames()
    {
        // JT: Rather than introduce a new method for the debugger, I'm reusing this one,
        // which - in spite of the very general name - is actually only used by the debugger
        // at this point.
        var names = new List<string>(_global._properties?.Count ?? 0 + _declarativeRecord._dictionary?.Count ?? 0);
        foreach (var name in _global.GetOwnProperties())
        {
            names.Add(name.Key.ToString());
        }

        foreach (var name in _declarativeRecord.GetAllBindingNames())
        {
            names.Add(name);
        }

        return names.ToArray();
    }

    public override bool Equals(JsValue? other)
    {
        return ReferenceEquals(this, other);
    }
}
