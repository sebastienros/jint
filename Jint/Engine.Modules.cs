using Esprima;
using Esprima.Ast;
using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint
{
    public partial class Engine
    {
        public IModuleLoader ModuleLoader { get; internal set; }

        private readonly Dictionary<ModuleCacheKey, JsModule> _modules = new Dictionary<ModuleCacheKey, JsModule>();

        public JsModule LoadModule(string specifier) => LoadModule(null, specifier);

        internal JsModule LoadModule(string referencingModuleLocation, string specifier)
        {
            var key = new ModuleCacheKey(referencingModuleLocation ?? string.Empty, specifier);

            if(_modules.TryGetValue(key, out var module))
            {
                return module;
            }

            if(!ModuleLoader.TryLoadModule(specifier, referencingModuleLocation, out var moduleSourceCode, out var moduleLocation))
            {
                ExceptionHelper.ThrowSyntaxError(Realm, "Error while loading module: module with specifier '" + specifier + "' could not be located");
            }

            Module moduleSource;
            try
            {
                var parserOptions = new ParserOptions(moduleLocation)
                {
                    AdaptRegexp = true,
                    Tolerant = true
                };

                moduleSource = new JavaScriptParser(moduleSourceCode, parserOptions).ParseModule();
            }
            catch (ParserException ex)
            {
                ExceptionHelper.ThrowSyntaxError(Realm, "Error while loading module: error in module '" + specifier + "': " + ex.Error?.ToString() ?? ex.Message);
                moduleSource = null;
            }

            module = new JsModule(this, _host.CreateRealm(), moduleSource, moduleLocation, false);

            _modules[key] = module;

            return module;

        }

        //https://tc39.es/ecma262/#sec-hostresolveimportedmodule
        internal JsModule ResolveImportedModule(JsModule referencingModule, string specifier)
        {
            return LoadModule(referencingModule._location, specifier);
        }

        //https://tc39.es/ecma262/#sec-hostimportmoduledynamically
        internal void ImportModuleDynamically(JsModule referencingModule, string specifier, PromiseCapability promiseCapability)
        {

            var promise = RegisterPromise();

            try
            {
                LoadModule(referencingModule._location, specifier);
                promise.Resolve(JsValue.Undefined);
                
            }
            catch (JavaScriptException ex)
            {
                promise.Reject(ex.Error);
            }

            FinishDynamicImport(referencingModule, specifier, promiseCapability, (PromiseInstance)promise.Promise);
        }

        //https://tc39.es/ecma262/#sec-finishdynamicimport
        internal void FinishDynamicImport(JsModule referencingModule, string specifier, PromiseCapability promiseCapability, PromiseInstance innerPromise)
        {
            var onFulfilled = new ClrFunctionInstance(this, "", (thisObj, args) =>
            {
                var moduleRecord = ResolveImportedModule(referencingModule, specifier);
                try
                {
                    var ns = JsModule.GetModuleNamespace(moduleRecord);
                    promiseCapability.Resolve.Call(ns);
                }
                catch (JavaScriptException ex)
                {
                    promiseCapability.Reject.Call(ex.Error);
                }
                return JsValue.Undefined;
            }, 0, PropertyFlag.Configurable);

            var onRejected = new ClrFunctionInstance(this, "", (thisObj, args) =>
            {
                var error = args.At(0);
                promiseCapability.Reject.Call(error);
                return JsValue.Undefined;
            }, 0, PropertyFlag.Configurable);

            PromiseOperations.PerformPromiseThen(this, innerPromise, onFulfilled, onRejected, null);
        }

        internal readonly struct ModuleCacheKey
        {
            internal readonly Key ReferencingModuleLocation;
            internal readonly Key Specifier;
            private readonly int _hashCode;

            internal ModuleCacheKey(string referencingModuleLocation, string specifier)
            {
                ReferencingModuleLocation = referencingModuleLocation;
                Specifier = specifier;
                unchecked
                {
                    _hashCode = 31 * ReferencingModuleLocation.HashCode + Specifier.HashCode;
                }
            }

            public override bool Equals(object obj)
            {
                return (obj is ModuleCacheKey other) && (ReferencingModuleLocation.Equals(other.ReferencingModuleLocation) && Specifier.Equals(other.Specifier));
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }
        }
    }
}
