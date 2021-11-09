using System;
using System.Collections.Generic;
using Esprima;
using Esprima.Ast;
using Jint.Runtime;
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

        internal readonly struct ModuleCacheKey : IEquatable<ModuleCacheKey>
        {
            internal readonly string ReferencingModuleLocation;
            internal readonly string Specifier;
            private readonly int _hashCode;

            internal ModuleCacheKey(string referencingModuleLocation, string specifier)
            {
                ReferencingModuleLocation = referencingModuleLocation;
                Specifier = specifier;
                unchecked
                {
                    _hashCode = 31 * ReferencingModuleLocation.GetHashCode() + Specifier.GetHashCode();
                }
            }

            public override bool Equals(object obj)
            {
                return (obj is ModuleCacheKey other) && Equals(other);
            }

            public bool Equals(ModuleCacheKey obj)
            {
                return ReferencingModuleLocation.Equals(obj.ReferencingModuleLocation) && Specifier.Equals(obj.Specifier);
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }
        }
    }
}
