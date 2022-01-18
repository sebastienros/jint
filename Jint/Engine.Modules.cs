using System;
using System.Collections.Generic;
using Esprima;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Modules;

namespace Jint
{
    public partial class Engine
    {
        internal IModuleLoader ModuleLoader { get; set; }

        private readonly Dictionary<ModuleCacheKey, JsModule> _modules = new();

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
            var key = new ModuleCacheKey(referencingModuleLocation ?? string.Empty, specifier);

            if (_modules.TryGetValue(key, out var module))
            {
                return module;
            }

            var (loadedModule, location) = ModuleLoader.LoadModule(this, specifier, referencingModuleLocation);
            module = new JsModule(this, _host.CreateRealm(), loadedModule, location.AbsoluteUri, false);

            _modules[key] = module;

            return module;
        }

        public JsModule DefineModule(string source, string specifier)
        {
            var module = new JavaScriptParser(source).ParseModule();

            return DefineModule(module, specifier);
        }

        public JsModule DefineModule(Module source, string specifier)
        {
            var key = new ModuleCacheKey(string.Empty, specifier);

            var module = new JsModule(this, _host.CreateRealm(), source, null, false);

            _modules[key] = module;

            return module;
        }

        public ObjectInstance ImportModule(string specifier)
        {
            var key = new ModuleCacheKey(string.Empty, specifier);

            if (!_modules.TryGetValue(key, out var module))
                throw new ArgumentOutOfRangeException(nameof(specifier), "No module was found for this specified");

            if (module.Status == ModuleStatus.Unlinked)
            {
                module.Link();
            }

            if (module.Status == ModuleStatus.Linked)
            {
                var evaluationResult = module.Evaluate();
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
                return JsModule.GetModuleNamespace(module);
            }

            throw new NotSupportedException($"Error while evaluating module: Module is in an invalid state: '{module.Status}'");
        }

        internal readonly record struct ModuleCacheKey(string ReferencingModuleLocation, string Specifier);
    }
}