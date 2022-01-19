using System;
using System.Collections.Generic;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Modules;

namespace Jint
{
    public partial class Engine
    {
        internal IModuleLoader ModuleLoader { get; set; }

        private readonly Dictionary<string, JsModule> _modules = new();

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getactivescriptormodule
        /// </summary>
        internal IScriptOrModule GetActiveScriptOrModule()
        {
            return _executionContexts.GetActiveScriptOrModule();
        }

        public JsModule LoadModule(string specifier) => LoadModule(null, specifier);

        internal JsModule LoadModule(string referencingModuleLocation, string specifier)
        {
            ResolveSpecifier(referencingModuleLocation, ref specifier);

            if (_modules.TryGetValue(specifier, out var module))
            {
                return module;
            }

            var (loadedModule, location) = ModuleLoader.LoadModule(this, specifier);
            module = new JsModule(this, _host.CreateRealm(), loadedModule, location.AbsoluteUri, false);

            _modules[specifier] = module;

            return module;
        }

        public JsModule DefineModule(string source, string specifier)
        {
            return DefineModule(new JavaScriptParser(source).ParseModule(), specifier);
        }

        public JsModule DefineModule(Module source, string specifier)
        {
            ResolveSpecifier(null, ref specifier);

            var module = new JsModule(this, _host.CreateRealm(), source, null, false);

            _modules[specifier] = module;

            return module;
        }

        public JsModule DefineModule<T>(string specifier)
        {
            return DefineModule(typeof(T).Name, TypeReference.CreateTypeReference<T>(this), specifier);
        }

        public JsModule DefineModule(string exportName, object exportValue, string specifier)
        {
            return DefineModule(exportName, JsValue.FromObject(this, exportValue), specifier);
        }

        public JsModule DefineModule(string exportName, JsValue exportValue, string specifier)
        {
            var module = DefineModule(new Module(NodeList.Create(System.Array.Empty<Statement>())), specifier);
            module.Link();
            module.BindExportedValue(exportName, exportValue);
            return module;
        }

        public ObjectInstance ImportModule(string specifier)
        {
            ResolveSpecifier(null, ref specifier);

            if (!_modules.TryGetValue(specifier, out var module))
                throw new ArgumentOutOfRangeException(nameof(specifier), $"No module was found for this specifier: {specifier}");

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
                    ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module evaluation did not return a promise");
                else if (evaluationResult is not PromiseInstance promise)
                    ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module evaluation did not return a promise: {evaluationResult.Type}");
                else if (promise.State == PromiseState.Rejected)
                    ExceptionHelper.ThrowJavaScriptException(this, promise.Value, new Completion(CompletionType.Throw, promise.Value, null, new Location(new Position(), new Position(), specifier)));
                else if (promise.State != PromiseState.Fulfilled)
                    ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module evaluation did not return a fulfilled promise: {promise.State}");
            }

            if (module.Status == ModuleStatus.Evaluated)
            {
                // TODO what about callstack and thrown exceptions?
                RunAvailableContinuations(_eventLoop);

                return JsModule.GetModuleNamespace(module);
            }

            throw new NotSupportedException($"Error while evaluating module: Module is in an invalid state: '{module.Status}'");
        }

        private void ResolveSpecifier(string referencingModuleLocation, ref string specifier)
        {
            // Relative module resolution
            // TODO: Not tested; add tests with the different variations of path resolution
            if (specifier.StartsWith("./") || specifier.StartsWith("../") || specifier.StartsWith("/"))
            {
                // TODO: Not tested; find official module resolve reference: https://www.typescriptlang.org/docs/handbook/module-resolution.html
                var uri = new Uri(referencingModuleLocation != null ? new Uri(referencingModuleLocation) : ModuleLoader.BasePath, specifier);
                specifier = $"/{ModuleLoader.BasePath.MakeRelativeUri(uri)}";
            }
        }
    }
}