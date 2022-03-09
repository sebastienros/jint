#nullable enable

using System;
using System.Collections.Generic;
using Esprima;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Modules;

namespace Jint
{
    public partial class Engine
    {
        internal IModuleLoader ModuleLoader { get; set; }

        private readonly Dictionary<string, ModuleRecord> _modules = new();
        private readonly Dictionary<string, ModuleBuilder> _builders = new();

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getactivescriptormodule
        /// </summary>
        internal IScriptOrModule? GetActiveScriptOrModule()
        {
            return _executionContexts?.GetActiveScriptOrModule();
        }

        internal ModuleRecord LoadModule(string? referencingModuleLocation, string specifier)
        {
            var moduleResolution = ModuleLoader.Resolve(referencingModuleLocation, specifier);

            if (_modules.TryGetValue(moduleResolution.Key, out var module))
            {
                return module;
            }

            if (_builders.TryGetValue(specifier, out var moduleBuilder))
            {
                module = LoadFromBuilder(specifier, moduleBuilder, moduleResolution);
            }
            else
            {
                module = LoaderFromModuleLoader(moduleResolution);
            }

            return module;
        }

        private CyclicModuleRecord LoadFromBuilder(string specifier, ModuleBuilder moduleBuilder, ResolvedSpecifier moduleResolution)
        {
            var parsedModule = moduleBuilder.Parse();
            var module = new BuilderModuleRecord(this, Realm, parsedModule, null, false);
            _modules[moduleResolution.Key] = module;
            moduleBuilder.BindExportedValues(module);
            _builders.Remove(specifier);
            return module;
        }

        private CyclicModuleRecord LoaderFromModuleLoader(ResolvedSpecifier moduleResolution)
        {
            var parsedModule = ModuleLoader.LoadModule(this, moduleResolution);
            var module = new SourceTextModuleRecord(this, Realm, parsedModule, moduleResolution.Uri?.LocalPath, false);
            _modules[moduleResolution.Key] = module;
            return module;
        }

        public void AddModule(string specifier, string code)
        {
            var moduleBuilder = new ModuleBuilder(this, specifier);
            moduleBuilder.AddSource(code);
            AddModule(specifier, moduleBuilder);
        }

        public void AddModule(string specifier, Action<ModuleBuilder> buildModule)
        {
            var moduleBuilder = new ModuleBuilder(this,specifier);
            buildModule(moduleBuilder);
            AddModule(specifier, moduleBuilder);
        }

        public void AddModule(string specifier, ModuleBuilder moduleBuilder)
        {
            _builders.Add(specifier, moduleBuilder);
        }

        public ObjectInstance ImportModule(string specifier)
        {
            return ImportModule(specifier, null);
        }

        internal ObjectInstance ImportModule(string specifier, string? referencingModuleLocation)
        {
            var moduleResolution = ModuleLoader.Resolve(referencingModuleLocation, specifier);

            if (!_modules.TryGetValue(moduleResolution.Key, out var module))
            {
                module = LoadModule(null, specifier);
            }

            if (module is not CyclicModuleRecord cyclicModule)
            {
                module.Link();
                EvaluateModule(specifier, module);
            }
            else if (cyclicModule.Status == ModuleStatus.Unlinked)
            {
                try
                {
                    cyclicModule.Link();
                }
                catch (JavaScriptException ex)
                {
                    if (ex.Location.Source == null)
                        ex.SetLocation(new Location(new Position(), new Position(), specifier));
                    throw;
                }

                if (cyclicModule.Status == ModuleStatus.Linked)
                {
                    EvaluateModule(specifier, cyclicModule);
                }

                if (cyclicModule.Status != ModuleStatus.Evaluated)
                {
                    ExceptionHelper.ThrowNotSupportedException($"Error while evaluating module: Module is in an invalid state: '{cyclicModule.Status}'");
                }
            }

            RunAvailableContinuations();

            return ModuleRecord.GetModuleNamespace(module);
        }

        private void EvaluateModule(string specifier, ModuleRecord cyclicModule)
        {
            var ownsContext = _activeEvaluationContext is null;
            _activeEvaluationContext ??= new EvaluationContext(this);
            JsValue evaluationResult;
            try
            {
                evaluationResult = cyclicModule.Evaluate();
            }
            finally
            {
                if (ownsContext)
                {
                    _activeEvaluationContext = null;
                }
            }

            // This should instead be returned and resolved in ImportModule(specifier) only so Host.ImportModuleDynamically can use this promise
            if (evaluationResult is not PromiseInstance promise)
            {
                ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module evaluation did not return a promise: {evaluationResult.Type}");
            }
            else if (promise.State == PromiseState.Rejected)
            {
                ExceptionHelper.ThrowJavaScriptException(this, promise.Value, new Completion(CompletionType.Throw, promise.Value, null, new Location(new Position(), new Position(), specifier)));
            }
            else if (promise.State != PromiseState.Fulfilled)
            {
                ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module evaluation did not return a fulfilled promise: {promise.State}");
            }
        }
    }
}
