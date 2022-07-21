#nullable disable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Modules;

internal sealed record ExportResolveSetItem(
    CyclicModuleRecord Module,
    string ExportName
);

/// <summary>
/// https://tc39.es/ecma262/#sec-abstract-module-records
/// </summary>
public abstract class ModuleRecord : JsValue, IScriptOrModule
{
    private ObjectInstance _namespace;
    protected readonly Engine _engine;
    protected readonly Realm _realm;
    internal ModuleEnvironmentRecord _environment;

    public string Location { get; }

    internal ModuleRecord(Engine engine, Realm realm, string location) : base(InternalTypes.Module)
    {
        _engine = engine;
        _realm = realm;
        Location = location;
    }

    public abstract List<string> GetExportedNames(List<CyclicModuleRecord> exportStarSet = null);
    internal abstract ResolvedBinding ResolveExport(string exportName, List<ExportResolveSetItem> resolveSet = null);
    public abstract void Link();
    public abstract JsValue Evaluate();

    protected internal abstract int InnerModuleLinking(Stack<CyclicModuleRecord> stack, int index);
    protected internal abstract Completion InnerModuleEvaluation(Stack<CyclicModuleRecord> stack, int index, ref int asyncEvalOrder);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getmodulenamespace
    /// </summary>
    public static ObjectInstance GetModuleNamespace(ModuleRecord module)
    {
        var ns = module._namespace;
        if (ns is null)
        {
            var exportedNames = module.GetExportedNames();
            var unambiguousNames = new List<string>();
            for (var i = 0; i < exportedNames.Count; i++)
            {
                var name = exportedNames[i];
                var resolution = module.ResolveExport(name);
                if (resolution is not null && resolution != ResolvedBinding.Ambiguous)
                {
                    unambiguousNames.Add(name);
                }
            }

            ns = CreateModuleNamespace(module, unambiguousNames);
        }

        return ns;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-modulenamespacecreate
    /// </summary>
    private static ObjectInstance CreateModuleNamespace(ModuleRecord module, List<string> unambiguousNames)
    {
        var m = new ModuleNamespace(module._engine, module, unambiguousNames);
        module._namespace = m;
        return m;
    }

    public override object ToObject()
    {
        ExceptionHelper.ThrowNotSupportedException();
        return null;
    }

    public override string ToString()
    {
        return $"{Type}: {Location}";
    }
}
