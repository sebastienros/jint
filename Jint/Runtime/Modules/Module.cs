#nullable disable

using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Modules;

internal sealed record ExportResolveSetItem(
    CyclicModule Module,
    string ExportName
);

/// <summary>
/// https://tc39.es/ecma262/#sec-abstract-module-records
/// </summary>
public abstract class Module : JsValue, IScriptOrModule
{
    private ObjectInstance _namespace;
    private ObjectInstance _deferredNamespace;
    protected internal readonly Engine _engine;
    protected internal readonly Realm _realm;
    internal ModuleEnvironment _environment;

    ParsingConstraints IScriptOrModule.ParsingConstraints => ParsingConstraints;
    ParserOptions IScriptOrModule.ParserOptions => ParserOptions;
    internal virtual ParsingConstraints ParsingConstraints => default;
    internal virtual ParserOptions ParserOptions => null;

    /// <summary>
    /// [[LoadedModules]] — the module records this one has already resolved a specifier to. Per
    /// <see href="https://tc39.es/ecma262/#sec-HostLoadImportedModule">HostLoadImportedModule</see> the host
    /// must be asked at most once for a given referrer/specifier pair and must answer consistently, so this
    /// is the memo that makes both true: once a request has an entry here, no loader call and not even a
    /// <see cref="IModuleLoader.Resolve"/> call is made for it again.
    /// </summary>
    private Dictionary<ModuleRequest, Module> _loadedModules;

    /// <summary>
    /// The module's [[ModuleSource]] internal slot — an %AbstractModuleSource% instance for module types
    /// that have a source representation (e.g. WebAssembly), or null otherwise. Used by source-phase
    /// imports (<c>import source x from "..."</c>). Populated by the host via
    /// <see cref="ModuleLoader.GetModuleSource"/>.
    /// </summary>
    internal ObjectInstance ModuleSource { get; set; }

    public string Location { get; }

    internal Module(Engine engine, Realm realm, string location) : base(InternalTypes.Module)
    {
        _engine = engine;
        _realm = realm;
        Location = location;
    }

    /// <summary>
    /// The UTF-8 encoded byte length of the module's original source, set at creation time. Modules whose
    /// source size is unknowable — exports-only builders, prepared modules, and custom <see cref="Module"/>
    /// records — report 0. Raw byte modules use their exact byte length; string sources use their UTF-8
    /// byte count.
    /// </summary>
    internal long SourceByteLength { get; set; }

    public abstract List<string> GetExportedNames(List<CyclicModule> exportStarSet = null);
    internal abstract ResolvedBinding ResolveExport(string exportName, List<ExportResolveSetItem> resolveSet = null);
    public abstract void Link();
    public abstract JsValue Evaluate();

    /// <summary>
    /// The spec's asynchronous load phase,
    /// <see href="https://tc39.es/ecma262/#sec-LoadRequestedModules">LoadRequestedModules</see>: transitively
    /// loads everything this module imports and returns a promise that fulfils once the whole graph is
    /// present, or rejects with the first loading failure. Must run to fulfilment before
    /// <see cref="Link"/>.
    /// </summary>
    /// <remarks>
    /// A module record with no dependencies of its own — anything that is not a
    /// <see cref="CyclicModule"/> — has nothing to load, so the base implementation hands back an
    /// already-fulfilled promise.
    /// </remarks>
    public virtual JsValue LoadRequestedModules()
    {
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        capability.Resolve(Undefined);
        return capability.PromiseInstance;
    }

    /// <summary>
    /// Looks a request up in this module's <c>[[LoadedModules]]</c>.
    /// </summary>
    internal bool TryGetLoadedModule(ModuleRequest request, out Module module)
    {
        if (_loadedModules is null)
        {
            module = null;
            return false;
        }

        return _loadedModules.TryGetValue(request, out module);
    }

    /// <summary>
    /// Step 1 of <see href="https://tc39.es/ecma262/#sec-FinishLoadingImportedModule">FinishLoadingImportedModule</see>:
    /// records the module the host produced for a request, and asserts the host's consistency requirement if
    /// an entry is already there.
    /// </summary>
    internal void RecordLoadedModule(ModuleRequest request, Module module)
    {
        _loadedModules ??= new Dictionary<ModuleRequest, Module>(LoadedModuleRequestComparer.Instance);

        if (_loadedModules.TryGetValue(request, out var existing))
        {
            if (!ReferenceEquals(existing, module))
            {
                Throw.InvalidOperationException(
                    $"Error while loading module: the module loader returned two different modules for specifier '{request.Specifier}' in '{Location ?? "(null)"}'. HostLoadImportedModule must be consistent for a given referrer and specifier.");
            }

            return;
        }

        _loadedModules[request] = module;
    }

    protected internal abstract int InnerModuleLinking(Stack<CyclicModule> stack, int index);
    protected internal abstract Completion InnerModuleEvaluation(Stack<CyclicModule> stack, int index, ref int asyncEvalOrder);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getmodulenamespace
    /// </summary>
    public static ObjectInstance GetModuleNamespace(Module module) => GetModuleNamespace(module, ModuleImportPhase.Evaluation);

    internal static ObjectInstance GetModuleNamespace(Module module, ModuleImportPhase phase)
    {
        if (phase == ModuleImportPhase.Defer)
        {
            var dns = module._deferredNamespace;
            if (dns is null)
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

                dns = CreateModuleNamespace(module, unambiguousNames, deferred: true);
            }

            return dns;
        }

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

            ns = CreateModuleNamespace(module, unambiguousNames, deferred: false);
        }

        return ns;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-modulenamespacecreate
    /// </summary>
    private static ModuleNamespace CreateModuleNamespace(Module module, List<string> unambiguousNames, bool deferred)
    {
        var m = new ModuleNamespace(module._engine, module, unambiguousNames, deferred);
        if (deferred)
        {
            module._deferredNamespace = m;
        }
        else
        {
            module._namespace = m;
        }
        return m;
    }

    public override object ToObject()
    {
        Throw.NotSupportedException();
        return null;
    }

    public override string ToString()
    {
        return $"{Type}: {Location}";
    }
}
