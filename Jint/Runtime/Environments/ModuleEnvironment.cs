using System.Diagnostics.CodeAnalysis;
using Jint.Collections;
using Jint.Native;
using Jint.Runtime.Modules;

namespace Jint.Runtime.Environments;

/// <summary>
/// Represents a module environment record
/// https://tc39.es/ecma262/#sec-module-environment-records
/// </summary>
internal sealed class ModuleEnvironment : DeclarativeEnvironment
{
    private readonly HybridDictionary<IndirectBinding> _importBindings = new();

    internal ModuleEnvironment(Engine engine) : base(engine, false)
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-environment-records-getthisbinding
    /// </summary>
    internal override JsValue GetThisBinding()
    {
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createimportbinding
    /// </summary>
    public void CreateImportBinding(string importName, ModuleRecord module, string name)
    {
        _importBindings[importName] = new IndirectBinding(module, name);
        CreateImmutableBindingAndInitialize(importName, true, Undefined, DisposeHint.Normal);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-environment-records-getbindingvalue-n-s
    /// </summary>
    internal override JsValue GetBindingValue(Key name, bool strict)
    {
        if (_importBindings.TryGetValue(name, out var indirectBinding))
        {
            return indirectBinding.Module._environment.GetBindingValue(indirectBinding.BindingName, true);
        }

        return base.GetBindingValue(name, strict);
    }

    internal override bool TryGetBinding(BindingName name, bool strict, [NotNullWhen(true)] out JsValue? value)
    {
        if (_importBindings.TryGetValue(name.Key, out var indirectBinding))
        {
            value = indirectBinding.Module._environment.GetBindingValue(indirectBinding.BindingName, strict: true);
            return true;
        }

        return base.TryGetBinding(name, strict, out value);
    }

    /// <summary>
    /// Locates the storage of the binding <paramref name="name"/> resolves to, following import
    /// bindings to the module that owns the value. A module environment's name set is fixed once the
    /// module is linked (module code is strict, so nothing can inject a binding later), which is what
    /// lets a read site remember the location instead of resolving the name on every read. Returns
    /// false when the binding is not stored in a hash table (small modules keep a list, which is
    /// already cheap to probe) or the chain cannot be followed.
    /// </summary>
    internal bool TryGetBindingLocation(Key name, [NotNullWhen(true)] out StringDictionarySlim<Binding>? bindings, out int entry, out Key key)
    {
        var environment = this;
        key = name;
        // Re-exports resolve to their final module when the import binding is created, so one hop is
        // the norm; the bound only guards against a malformed cycle.
        for (var hops = 0; hops < 4; hops++)
        {
            if (environment._importBindings.TryGetValue(key, out var indirectBinding))
            {
                if (indirectBinding.Module._environment is not { } target)
                {
                    break;
                }

                environment = target;
                key = indirectBinding.BindingName;
                continue;
            }

            if (environment._slots is null
                && environment._dictionary?._dictionary is { } dictionary
                && dictionary.FindEntryIndex(key) is var index and >= 0)
            {
                bindings = dictionary;
                entry = index;
                return true;
            }

            break;
        }

        bindings = null;
        entry = -1;
        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-module-environment-records-hasthisbinding
    /// </summary>
    internal override bool HasThisBinding() => true;

    private readonly record struct IndirectBinding(ModuleRecord Module, string BindingName);
}
