using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Modules;

/// <summary>
/// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects
/// </summary>
internal sealed class ModuleNamespace : ObjectInstance
{
    private readonly JsModule _module;
    private readonly HashSet<string> _exports;

    public ModuleNamespace(Engine engine, JsModule module, List<string> exports) : base(engine)
    {
        _module = module;
        exports.Sort();
        _exports = new HashSet<string>(exports);
    }

    protected internal override ObjectInstance GetPrototypeOf() => null;

    public override bool SetPrototypeOf(JsValue value) => SetImmutablePrototype(value);

    private bool SetImmutablePrototype(JsValue value)
    {
        var current = GetPrototypeOf();
        return SameValue(value, current ?? Null);
    }

    public override bool Extensible => false;

    public override bool PreventExtensions() => true;

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (property.IsSymbol())
        {
            return base.GetOwnProperty(property);
        }

        var p = TypeConverter.ToString(property);

        if (!_exports.Contains(p))
        {
            return PropertyDescriptor.Undefined;
        }

        var value = Get(property);
        return new PropertyDescriptor(value, true, true, false);
    }

    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (property.IsSymbol())
        {
            return base.DefineOwnProperty(property, desc);
        }

        var current = GetOwnProperty(property);

        if (current == PropertyDescriptor.Undefined)
        {
            return false;
        }

        if (desc.Configurable || desc.Enumerable || desc.IsAccessorDescriptor() || !desc.Writable)
        {
            return false;
        }

        if (desc.Value is not null)
        {
            return SameValue(desc.Value, current.Value);
        }

        return true;
    }

    public override bool HasProperty(JsValue property)
    {
        if (property.IsSymbol())
        {
            return base.HasProperty(property);
        }

        var p = TypeConverter.ToString(property);
        return _exports.Contains(p);
    }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        if (property.IsSymbol())
        {
            return base.Get(property, receiver);
        }

        var p = TypeConverter.ToString(property);

        if (!_exports.Contains(p))
        {
            return Undefined;
        }

        var m = _module;
        var binding = m.ResolveExport(p);
        var targetModule = binding.Module;

        if (binding.BindingName == "*namespace*")
        {
            return JsModule.GetModuleNamespace(targetModule);
        }

        var targetEnv = targetModule._environment;
        if (targetEnv is null)
        {
            ExceptionHelper.ThrowReferenceError(_engine.Realm, "environment");
        }

        return targetEnv.GetBindingValue(binding.BindingName, true);
    }

    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        return false;
    }

    public override bool Delete(JsValue property)
    {
        if (property.IsSymbol())
        {
            return base.Delete(property);
        }

        var p = TypeConverter.ToString(property);
        return !_exports.Contains(p);
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = base.GetOwnPropertyKeys(types);
        if ((types & Types.String) != 0)
        {
            foreach (var export in _exports)
            {
                keys.Add(export);
            }
        }

        return keys;
    }
}