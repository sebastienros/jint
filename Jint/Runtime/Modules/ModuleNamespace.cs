#nullable disable

using Jint.Collections;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Modules;

/// <summary>
/// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects
/// </summary>
internal sealed class ModuleNamespace : ObjectInstance
{
    private readonly Module _module;
    private readonly HashSet<string> _exports;

    public ModuleNamespace(Engine engine, Module module, List<string> exports) : base(engine)
    {
        _module = module;
        _exports = new HashSet<string>(exports, StringComparer.Ordinal);
    }

    protected override void Initialize()
    {
        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Module", false, false, false)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-getprototypeof
    /// </summary>
    protected internal override ObjectInstance GetPrototypeOf() => null;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-setprototypeof-v
    /// </summary>
    internal override bool SetPrototypeOf(JsValue value) => SetImmutablePrototype(value);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set-immutable-prototype
    /// </summary>
    private bool SetImmutablePrototype(JsValue value)
    {
        var current = GetPrototypeOf();
        return SameValue(value, current ?? Null);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-isextensible
    /// </summary>
    public override bool Extensible => false;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-preventextensions
    /// </summary>
    public override bool PreventExtensions() => true;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-getownproperty-p
    /// </summary>
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-defineownproperty-p-desc
    /// </summary>
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

        if (desc.Configurable)
        {
            return false;
        }

        if (desc.EnumerableSet && !desc.Enumerable)
        {
            return false;
        }

        if (desc.IsAccessorDescriptor())
        {
            return false;
        }

        if (desc.WritableSet && !desc.Writable)
        {
            return false;
        }

        if (desc.Value is not null)
        {
            return SameValue(desc.Value, current.Value);
        }

        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-hasproperty-p
    /// </summary>
    public override bool HasProperty(JsValue property)
    {
        if (property.IsSymbol())
        {
            return base.HasProperty(property);
        }

        var p = TypeConverter.ToString(property);
        return _exports.Contains(p);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-get-p-receiver
    /// </summary>
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

        if (string.Equals(binding.BindingName, "*namespace*", StringComparison.Ordinal))
        {
            return Module.GetModuleNamespace(targetModule);
        }

        var targetEnv = targetModule._environment;
        if (targetEnv is null)
        {
            ExceptionHelper.ThrowReferenceError(_engine.Realm, "Module's environment hasn't been initialized");
        }

        return targetEnv.GetBindingValue(binding.BindingName, true);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-set-p-v-receiver
    /// </summary>
    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-delete-p
    /// </summary>
    public override bool Delete(JsValue property)
    {
        if (property.IsSymbol())
        {
            return base.Delete(property);
        }

        var p = TypeConverter.ToString(property);
        return !_exports.Contains(p);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-ownpropertykeys
    /// </summary>
    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var result = new List<JsValue>();
        if ((types & Types.String) != Types.Empty)
        {
            result.Capacity = _exports.Count;
            foreach (var export in _exports)
            {
                result.Add(export);
            }
            result.Sort(ArrayPrototype.ArrayComparer.Default);
        }

        result.AddRange(base.GetOwnPropertyKeys(types));

        return result;
    }
}
