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

        private readonly Dictionary<string, JsModule> _modules = new();
        private readonly Dictionary<string, ModuleBuilder> _builders = new();

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getactivescriptormodule
        /// </summary>
        internal IScriptOrModule? GetActiveScriptOrModule()
        {
            return _executionContexts?.GetActiveScriptOrModule();
        }

        internal JsModule LoadModule(string specifier) => LoadModule(null, specifier);

        internal JsModule LoadModule(string? referencingModuleLocation, string specifier)
        {
            var moduleResolution = ModuleLoader.Resolve(referencingModuleLocation, specifier);

            if (_modules.TryGetValue(moduleResolution.Key, out var module))
            {
                return module;
            }

            if (_builders.TryGetValue(specifier, out var moduleBuilder))
            {
                var parsedModule = moduleBuilder.Parse();
                module = new JsModule(this, _host.CreateRealm(), parsedModule, null, false);
                // Early link is required because we need to bind values before returning
                module.Link();
                moduleBuilder.BindExportedValues(module);
                _builders.Remove(specifier);
            }
            else
            {
                var parsedModule = ModuleLoader.LoadModule(this, moduleResolution);
                module = new JsModule(this, _host.CreateRealm(), parsedModule, moduleResolution.Uri?.LocalPath, false);
            }

            _modules[moduleResolution.Key] = module;

            return module;
        }

        public void AddModule(string specifier, string source)
        {
            var moduleBuilder = new ModuleBuilder(this);
            moduleBuilder.AddSource(source);
            AddModule(specifier, moduleBuilder);
        }

        public void AddModule(string specifier, Action<ModuleBuilder> buildModule)
        {
            var moduleBuilder = new ModuleBuilder(this);
            buildModule(moduleBuilder);
            AddModule(specifier, moduleBuilder);
        }

        public void AddModule(string specifier, ModuleBuilder moduleBuilder)
        {
            _builders.Add(specifier, moduleBuilder);
        }

        public ObjectInstance ImportModule(string specifier)
        {
            var moduleResolution = ModuleLoader.Resolve(null, specifier);

            if (!_modules.TryGetValue(moduleResolution.Key, out var module))
            {
                module = LoadModule(null, specifier);
            }

            if (module.Status == ModuleStatus.Unlinked)
            {
                module.Link();
            }

            if (module.Status == ModuleStatus.Linked)
            {
                var ownsContext = _activeEvaluationContext is null;
                _activeEvaluationContext ??= new EvaluationContext(this);
                JsValue evaluationResult;
                try
                {
                    evaluationResult = module.Evaluate();
                }
                finally
                {
                    if (ownsContext)
                    {
                        _activeEvaluationContext = null;
                    }
                }

                if (evaluationResult == null)
                {
                    ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module evaluation did not return a promise");
                }
                else if (evaluationResult is not PromiseInstance promise)
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

            if (module.Status == ModuleStatus.Evaluated)
            {
                // TODO what about callstack and thrown exceptions?
                RunAvailableContinuations(_eventLoop);

                return JsModule.GetModuleNamespace(module);
            }

            ExceptionHelper.ThrowNotSupportedException($"Error while evaluating module: Module is in an invalid state: '{module.Status}'");
            return default;
        }
    }
}