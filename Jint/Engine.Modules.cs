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

    public class ModuleOperations
    {
        private readonly Engine _engine;
        private readonly Dictionary<string, Module> _modules = new(StringComparer.Ordinal);
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

            if (_modules.TryGetValue(moduleResolution.Key, out var module))
            {
                return module;
            }

            if (_builders.TryGetValue(moduleResolution.Key, out var moduleBuilder))
            {
                module = LoadFromBuilder(moduleResolution.Key, moduleBuilder, moduleResolution);
            }
            else
            {
                module = LoadFromModuleLoader(moduleResolution);
            }

            if (module is SourceTextModule sourceTextModule)
            {
                _engine.Debugger.OnBeforeEvaluate(sourceTextModule._source);
            }

            return module;
        }

        private BuilderModule LoadFromBuilder(string specifier, ModuleBuilder moduleBuilder, ResolvedSpecifier moduleResolution)
        {
            var parsedModule = moduleBuilder.Parse();
            var module = new BuilderModule(_engine, _engine.Realm, parsedModule, location: parsedModule.Program!.Location.SourceFile, async: false);
            _modules[moduleResolution.Key] = module;
            moduleBuilder.BindExportedValues(module);
            _builders.Remove(specifier);
            return module;
        }

        private Module LoadFromModuleLoader(ResolvedSpecifier moduleResolution)
        {
            var module = ModuleLoader.LoadModule(_engine, moduleResolution);
            _modules[moduleResolution.Key] = module;
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

            if (!_modules.TryGetValue(moduleResolution.Key, out var module))
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
                    ExceptionHelper.ThrowNotSupportedException($"Error while evaluating module: Module is in an invalid state: '{cyclicModule.Status}'");
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
            _engine. _activeEvaluationContext ??= new EvaluationContext(_engine);
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
                ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module evaluation did not return a promise: {evaluationResult.Type}");
            }
            else if (promise.State == PromiseState.Rejected)
            {
                var location = module is CyclicModule cyclicModuleRecord
                    ? cyclicModuleRecord.AbnormalCompletionLocation
                    : SourceLocation.From(new Position(), new Position());

                var node = AstExtensions.CreateLocationNode(location);
                ExceptionHelper.ThrowJavaScriptException(_engine, promise.Value, node.Location);
            }
            else if (promise.State != PromiseState.Fulfilled)
            {
                ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module evaluation did not return a fulfilled promise: {promise.State}");
            }

            return evaluationResult;
        }
    }
}
