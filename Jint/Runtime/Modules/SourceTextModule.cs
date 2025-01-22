using System.Diagnostics;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Runtime.Modules;

/// <summary>
/// https://tc39.es/ecma262/#importentry-record
/// </summary>
internal sealed record ImportEntry(ModuleRequest ModuleRequest, string? ImportName, string LocalName);

/// <summary>
/// https://tc39.es/ecma262/#exportentry-record
/// </summary>
internal sealed record ExportEntry(string? ExportName, ModuleRequest? ModuleRequest, string? ImportName, string? LocalName);

/// <summary>
/// https://tc39.es/ecma262/#sec-source-text-module-records
/// </summary>
internal class SourceTextModule : CyclicModule
{
    internal readonly AstModule _source;
    private readonly ParserOptions _parserOptions;
    private ExecutionContext _context;
    private ObjectInstance? _importMeta;
    private readonly List<ImportEntry>? _importEntries;
    internal readonly List<ExportEntry> _localExportEntries;
    private readonly List<ExportEntry> _indirectExportEntries;
    private readonly List<ExportEntry> _starExportEntries;

    internal SourceTextModule(Engine engine, Realm realm, in Prepared<AstModule> source, string? location, bool async)
        : base(engine, realm, location, async)
    {
        Debug.Assert(source.IsValid);
        _source = source.Program!;
        _parserOptions = source.ParserOptions!;

        // https://tc39.es/ecma262/#sec-parsemodule

        HoistingScope.GetImportsAndExports(
            _source,
            out _requestedModules,
            out _importEntries,
            out _localExportEntries,
            out _indirectExportEntries,
            out _starExportEntries);

        //ToDo async modules
    }

    internal ObjectInstance ImportMeta
    {
        get
        {
            if (_importMeta is null)
            {
                _importMeta = _realm.Intrinsics.Object.Construct(1);
                _importMeta.CreateDataProperty("url", Location);
            }
            return _importMeta;
        }
        set => _importMeta = value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getexportednames
    /// </summary>
    public override List<string?> GetExportedNames(List<CyclicModule>? exportStarSet = null)
    {
        exportStarSet ??= new List<CyclicModule>();
        if (exportStarSet.Contains(this))
        {
            //Reached the starting point of an export * circularity
            return new List<string?>();
        }

        exportStarSet.Add(this);
        var exportedNames = new List<string?>();
        for (var i = 0; i < _localExportEntries.Count; i++)
        {
            var e = _localExportEntries[i];
            exportedNames.Add(e.ExportName);
        }

        for (var i = 0; i < _indirectExportEntries.Count; i++)
        {
            var e = _indirectExportEntries[i];
            exportedNames.Add(e.ExportName);
        }

        for (var i = 0; i < _starExportEntries.Count; i++)
        {
            var e = _starExportEntries[i];
            var requestedModule = _engine._host.GetImportedModule(this, e.ModuleRequest!.Value);
            var starNames = requestedModule.GetExportedNames(exportStarSet);

            for (var j = 0; j < starNames.Count; j++)
            {
                var n = starNames[j];
                if (!"default".Equals(n, StringComparison.Ordinal) && !exportedNames.Contains(n))
                {
                    exportedNames.Add(n);
                }
            }
        }

        return exportedNames;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-resolveexport
    /// </summary>
    internal override ResolvedBinding? ResolveExport(string? exportName, List<ExportResolveSetItem>? resolveSet = null)
    {
        resolveSet ??= new List<ExportResolveSetItem>();

        for (var i = 0; i < resolveSet.Count; i++)
        {
            var r = resolveSet[i];
            if (ReferenceEquals(this, r.Module) && string.Equals(exportName, r.ExportName, StringComparison.Ordinal))
            {
                // circular import request
                return null;
            }
        }

        resolveSet.Add(new ExportResolveSetItem(this, exportName));
        for (var i = 0; i < _localExportEntries.Count; i++)
        {
            var e = _localExportEntries[i];
            if (string.Equals(exportName, e.ExportName, StringComparison.Ordinal))
            {
                // i. Assert: module provides the direct binding for this export.
                return new ResolvedBinding(this, e.LocalName);
            }
        }

        for (var i = 0; i < _indirectExportEntries.Count; i++)
        {
            var e = _indirectExportEntries[i];
            if (string.Equals(exportName, e.ExportName, StringComparison.Ordinal))
            {
                var importedModule = _engine._host.GetImportedModule(this, e.ModuleRequest!.Value);
                if (string.Equals(e.ImportName, "*", StringComparison.Ordinal))
                {
                    // 1. Assert: module does not provide the direct binding for this export.
                    return new ResolvedBinding(importedModule, "*namespace*");
                }
                else
                {
                    // 1. Assert: module imports a specific binding for this export.
                    return importedModule.ResolveExport(e.ImportName, resolveSet);
                }
            }
        }

        if ("default".Equals(exportName, StringComparison.Ordinal))
        {
            // Assert: A default export was not explicitly defined by this module
            return null;
        }

        ResolvedBinding? starResolution = null;

        for (var i = 0; i < _starExportEntries.Count; i++)
        {
            var e = _starExportEntries[i];
            var importedModule = _engine._host.GetImportedModule(this, e.ModuleRequest!.Value);
            var resolution = importedModule.ResolveExport(exportName, resolveSet);
            if (resolution == ResolvedBinding.Ambiguous)
            {
                return resolution;
            }

            if (resolution is not null)
            {
                if (starResolution is null)
                {
                    starResolution = resolution;
                }
                else
                {
                    if (resolution.Module != starResolution.Module || !string.Equals(resolution.BindingName, starResolution.BindingName, StringComparison.Ordinal))
                    {
                        return ResolvedBinding.Ambiguous;
                    }
                }
            }
        }

        return starResolution;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-source-text-module-record-initialize-environment
    /// </summary>
    protected override void InitializeEnvironment()
    {
        for (var i = 0; i < _indirectExportEntries.Count; i++)
        {
            var e = _indirectExportEntries[i];
            var resolution = ResolveExport(e.ExportName);
            if (resolution is null || resolution == ResolvedBinding.Ambiguous)
            {
                ExceptionHelper.ThrowSyntaxError(_realm, "Ambiguous import statement for identifier: " + e.ExportName);
            }
        }

        var realm = _realm;
        var env = JintEnvironment.NewModuleEnvironment(_engine, realm.GlobalEnv);
        _environment = env;

        if (_importEntries != null)
        {
            for (var i = 0; i < _importEntries.Count; i++)
            {
                var ie = _importEntries[i];
                var importedModule = _engine._host.GetImportedModule(this, ie.ModuleRequest);
                if (string.Equals(ie.ImportName, "*", StringComparison.Ordinal))
                {
                    var ns = GetModuleNamespace(importedModule);
                    env.CreateImmutableBinding(ie.LocalName, strict: true);
                    env.InitializeBinding(ie.LocalName, ns);
                }
                else
                {
                    var resolution = importedModule.ResolveExport(ie.ImportName);
                    if (resolution is null || resolution == ResolvedBinding.Ambiguous)
                    {
                        ExceptionHelper.ThrowSyntaxError(_realm, "Ambiguous import statement for identifier " + ie.ImportName);
                    }

                    if (string.Equals(resolution.BindingName, "*namespace*", StringComparison.Ordinal))
                    {
                        var ns = GetModuleNamespace(resolution.Module);
                        env.CreateImmutableBinding(ie.LocalName, strict: true);
                        env.InitializeBinding(ie.LocalName, ns);
                    }
                    else
                    {
                        env.CreateImportBinding(ie.LocalName, resolution.Module, resolution.BindingName);
                    }
                }
            }
        }

        var moduleContext = new ExecutionContext(this, _environment, _environment, null, realm, null);
        _context = moduleContext;

        _engine.EnterExecutionContext(_context);

        var hoistingScope = HoistingScope.GetModuleLevelDeclarations(_source);

        var varDeclarations = hoistingScope._variablesDeclarations;
        var declaredVarNames = new HashSet<Key>();
        if (varDeclarations != null)
        {
            var boundNames = new List<Key>();
            for (var i = 0; i < varDeclarations.Count; i++)
            {
                var d = varDeclarations[i];
                boundNames.Clear();
                d.GetBoundNames(boundNames);
                for (var j = 0; j < boundNames.Count; j++)
                {
                    var dn = boundNames[j];
                    if (declaredVarNames.Add(dn))
                    {
                        env.CreateMutableBinding(dn);
                        env.InitializeBinding(dn, Undefined);
                    }
                }
            }
        }

        if (hoistingScope._lexicalDeclarations != null)
        {
            var cache = DeclarationCacheBuilder.Build(hoistingScope._lexicalDeclarations);
            for (var i = 0; i < cache.Declarations.Count; i++)
            {
                var declaration = cache.Declarations[i];
                foreach (var bn in declaration.BoundNames)
                {
                    if (declaration.IsConstantDeclaration)
                    {
                        env.CreateImmutableBinding(bn);
                    }
                    else
                    {
                        env.CreateMutableBinding(bn);
                    }
                }
            }
        }

        var functionDeclarations = hoistingScope._functionDeclarations;

        if (functionDeclarations != null)
        {
            for (var i = 0; i < functionDeclarations.Count; i++)
            {
                var d = functionDeclarations[i];
                var fn = d.Id?.Name ?? "*default*";
                var fd = new JintFunctionDefinition(d);
                env.CreateMutableBinding(fn, canBeDeleted: true);
                var fo = realm.Intrinsics.Function.InstantiateFunctionObject(fd, env, privateEnv: null);
                if (string.Equals(fn, "*default*", StringComparison.Ordinal))
                {
                    fo.SetFunctionName("default");
                }
                env.InitializeBinding(fn, fo);
            }
        }

        _engine.LeaveExecutionContext();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-source-text-module-record-execute-module
    /// </summary>
    internal override Completion ExecuteModule(PromiseCapability? capability = null)
    {
        var moduleContext = new ExecutionContext(this, _environment, _environment, privateEnvironment: null, _realm, parserOptions: _parserOptions);
        if (!_hasTLA)
        {
            using (new StrictModeScope(strict: true, force: true))
            {
                _engine.EnterExecutionContext(moduleContext);
                try
                {
                    var statementList = new JintStatementList(statement: null, _source.Body);
                    var context = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);
                    var result = statementList.Execute(context); //Create new evaluation context when called from e.g. module tests
                    return result;
                }
                finally
                {
                    _engine.LeaveExecutionContext();
                }
            }
        }
        else
        {
            ExceptionHelper.ThrowNotImplementedException("async modules not implemented");
            return default;
        }
    }
}
