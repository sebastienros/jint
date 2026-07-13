using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Modules;
using Module = Jint.Runtime.Modules.Module;

namespace Jint;

public partial class Engine
{
    public ModuleOperations Modules { get; internal set; } = null!;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getactivescriptormodule
    /// </summary>
    internal IScriptOrModule? GetActiveScriptOrModule()
    {
        return _executionContexts?.GetActiveScriptOrModule();
    }

    // Cache key for the per-engine module map. Per the import-attributes spec,
    // two requests for the same resolved specifier with different attributes
    // are distinct module records. Import phase (defer/source) does NOT create
    // distinct records — defer and evaluation share the same underlying module.
    internal readonly record struct ModuleCacheKey(string Key, ModuleImportAttribute[] Attributes)
    {
        public static ModuleCacheKey From(ResolvedSpecifier resolved)
            => new(resolved.Key, resolved.ModuleRequest.Attributes ?? []);

        public bool Equals(ModuleCacheKey other)
        {
            if (!string.Equals(Key, other.Key, StringComparison.Ordinal))
            {
                return false;
            }
            var a = Attributes;
            var b = other.Attributes;
            if (a.Length != b.Length)
            {
                return false;
            }
            for (var i = 0; i < a.Length; i++)
            {
                if (Array.IndexOf(b, a[i]) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Key) * 397) ^ Attributes.Length;
            }
        }
    }

    public class ModuleOperations
    {
        private readonly Engine _engine;
        private readonly Dictionary<ModuleCacheKey, Module> _modules = new();
        private readonly Dictionary<string, ModuleBuilder> _builders = new(StringComparer.Ordinal);

        public ModuleOperations(Engine engine, IModuleLoader moduleLoader)
        {
            ModuleLoader = moduleLoader;
            _engine = engine;
        }

        internal IModuleLoader ModuleLoader { get; }

        internal Module Load(string? referencingModuleLocation, ModuleRequest request)
        {
            var moduleResolution = ModuleLoader.Resolve(referencingModuleLocation, request);
            var cacheKey = ModuleCacheKey.From(moduleResolution);

            if (_modules.TryGetValue(cacheKey, out var module))
            {
                return module;
            }

            if (_builders.TryGetValue(moduleResolution.Key, out var moduleBuilder))
            {
                module = LoadFromBuilder(moduleResolution.Key, moduleBuilder, moduleResolution, cacheKey);
            }
            else
            {
                module = LoadFromModuleLoader(moduleResolution, cacheKey);
            }

            if (module is SourceTextModule sourceTextModule)
            {
                _engine.Debugger.OnBeforeEvaluate(sourceTextModule._source);
            }

            return module;
        }

        private BuilderModule LoadFromBuilder(string specifier, ModuleBuilder moduleBuilder, ResolvedSpecifier moduleResolution, ModuleCacheKey cacheKey)
        {
            var parsedModule = moduleBuilder.Parse();
            var hasTopLevelAwait = HoistingScope.HasTopLevelAwait(parsedModule.Program!);
            var module = new BuilderModule(_engine, _engine.Realm, parsedModule, location: parsedModule.Program!.Location.SourceFile, async: hasTopLevelAwait);
            _modules[cacheKey] = module;
            moduleBuilder.BindExportedValues(module);
            _builders.Remove(specifier);
            return module;
        }

        private Module LoadFromModuleLoader(ResolvedSpecifier moduleResolution, ModuleCacheKey cacheKey)
        {
            var module = ModuleLoader.LoadModule(_engine, moduleResolution);
            _modules[cacheKey] = module;
            return module;
        }

        public void Add(string specifier, string code)
        {
            var moduleBuilder = new ModuleBuilder(_engine, specifier);
            moduleBuilder.AddSource(code);
            Add(specifier, moduleBuilder);
        }

        public void Add(string specifier, Action<ModuleBuilder> buildModule)
        {
            var moduleBuilder = new ModuleBuilder(_engine, specifier);
            buildModule(moduleBuilder);
            Add(specifier, moduleBuilder);
        }

        public void Add(string specifier, ModuleBuilder moduleBuilder)
        {
            _builders.Add(specifier, moduleBuilder);
        }

        public ObjectInstance Import(string specifier)
        {
            return Import(specifier, referencingModuleLocation: null);
        }

        internal ObjectInstance Import(string specifier, string? referencingModuleLocation)
        {
            return Import(new ModuleRequest(specifier, []), referencingModuleLocation);
        }

        internal ObjectInstance Import(ModuleRequest request, string? referencingModuleLocation)
        {
            var moduleResolution = ModuleLoader.Resolve(referencingModuleLocation, request);

            if (!_modules.TryGetValue(ModuleCacheKey.From(moduleResolution), out var module))
            {
                module = Load(referencingModuleLocation, request);
            }

            if (module is not CyclicModule cyclicModule)
            {
                LinkModule(request.Specifier, module);
                EvaluateModule(request.Specifier, module);
            }
            else if (cyclicModule.Status == ModuleStatus.Unlinked)
            {
                LinkModule(request.Specifier, cyclicModule);

                if (cyclicModule.Status == ModuleStatus.Linked)
                {
                    _engine.ExecuteWithConstraints(true, () => EvaluateModule(request.Specifier, cyclicModule));
                }

                if (cyclicModule.Status != ModuleStatus.Evaluated)
                {
                    Throw.NotSupportedException($"Error while evaluating module: Module is in an invalid state: '{cyclicModule.Status}'");
                }
            }

            _engine.RunAvailableContinuations();

            return Module.GetModuleNamespace(module);
        }

        private static void LinkModule(string specifier, Module module)
        {
            module.Link();
        }

        private JsValue EvaluateModule(string specifier, Module module)
        {
            var ownsContext = _engine._activeEvaluationContext is null;
            _engine._activeEvaluationContext ??= new EvaluationContext(_engine);
            JsValue evaluationResult;
            try
            {
                evaluationResult = module.Evaluate();
            }
            finally
            {
                if (ownsContext)
                {
                    _engine._activeEvaluationContext = null!;
                }
            }

            // This should instead be returned and resolved in ImportModule(specifier) only so Host.ImportModuleDynamically can use this promise
            if (evaluationResult is not JsPromise promise)
            {
                Throw.InvalidOperationException($"Error while evaluating module: Module evaluation did not return a promise: {evaluationResult.Type}");
                return null;
            }

            // For async modules (top-level await), drive the event loop until the module's top-level
            // capability promise settles. A .NET Task awaited at the top level (task interop) completes
            // on a ThreadPool thread and enqueues its resolve job only after a delay, so we must poll
            // rather than spin — a tight loop finishes in microseconds while the queue is still empty
            // and gives up before the Task ever completes (issue #2663). Bounded by PromiseTimeout so a
            // genuinely never-settling top-level await cannot block Import forever.
            _engine.DrainEventLoopUntilSettled(promise, _engine.Options.Constraints.PromiseTimeout);

            if (promise.State == PromiseState.Rejected)
            {
                var location = module is CyclicModule cyclicModuleRecord
                    ? cyclicModuleRecord.AbnormalCompletionLocation
                    : SourceLocation.From(new Position(), new Position());

                var node = AstExtensions.CreateLocationNode(location);
                Throw.JavaScriptException(_engine, promise.Value, node.Location);
            }
            else if (promise.State != PromiseState.Fulfilled)
            {
                Throw.InvalidOperationException($"Error while evaluating module: Module evaluation did not return a fulfilled promise: {promise.State}");
            }

            return evaluationResult;
        }
    }
}
