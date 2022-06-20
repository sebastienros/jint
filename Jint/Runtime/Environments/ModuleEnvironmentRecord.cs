using System.Diagnostics.CodeAnalysis;
using Jint.Collections;
using Jint.Native;
using Jint.Runtime.Modules;

namespace Jint.Runtime.Environments;

/// <summary>
/// Represents a module environment record
/// https://tc39.es/ecma262/#sec-module-environment-records
/// </summary>
internal sealed class ModuleEnvironmentRecord : DeclarativeEnvironmentRecord
{
    private readonly HybridDictionary<IndirectBinding> _importBindings = new();

    internal ModuleEnvironmentRecord(Engine engine) : base(engine, false)
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-environment-records-getthisbinding
    /// </summary>
    public override JsValue GetThisBinding()
    {
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createimportbinding
    /// </summary>
    public void CreateImportBinding(string importName, ModuleRecord module, string name)
    {
        _importBindings[importName] = new IndirectBinding(module, name);
        CreateImmutableBindingAndInitialize(importName, true, JsValue.Undefined);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-environment-records-getbindingvalue-n-s
    /// </summary>
    public override JsValue GetBindingValue(string name, bool strict)
    {
        if (_importBindings.TryGetValue(name, out var indirectBinding))
        {
            return indirectBinding.Module._environment.GetBindingValue(indirectBinding.BindingName, true);
        }

        return base.GetBindingValue(name, strict);
    }

    internal override bool TryGetBinding(BindingName name, bool strict, out Binding binding, [NotNullWhen(true)] out JsValue? value)
    {
        if (_importBindings.TryGetValue(name.Key, out var indirectBinding))
        {
            value = indirectBinding.Module._environment.GetBindingValue(indirectBinding.BindingName, true);
            binding = new(value, canBeDeleted: false, mutable: false, strict: true);
            return true;
        }

        return base.TryGetBinding(name, strict, out binding, out value);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-environment-records-hasthisbinding
    /// </summary>
    public override bool HasThisBinding() => true;

    private readonly record struct IndirectBinding(ModuleRecord Module, string BindingName);
}
