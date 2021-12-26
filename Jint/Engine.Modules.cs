#nullable enable

using System.Collections.Generic;
using Jint.Runtime.Modules;

namespace Jint
{
    public partial class Engine
    {
        internal IModuleLoader ModuleLoader { get; set; }

        private readonly Dictionary<ModuleCacheKey, JsModule> _modules = new();

        internal JsModule LoadModule(string specifier) => LoadModule(null, specifier);

        internal JsModule LoadModule(string? referencingModuleLocation, string specifier)
        {
            var key = new ModuleCacheKey(referencingModuleLocation ?? string.Empty, specifier);

            if (_modules.TryGetValue(key, out var module))
            {
                return module;
            }

            var (loadedModule, location) = ModuleLoader.LoadModule(this, specifier, referencingModuleLocation);
            module = new JsModule(this, _host.CreateRealm(), loadedModule, location.AbsoluteUri, false);
            module.Link();
            module.Evaluate();

            _modules[key] = module;

            return module;
        }

        internal readonly record struct ModuleCacheKey(string ReferencingModuleLocation, string Specifier);
    }
}