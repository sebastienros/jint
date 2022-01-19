#nullable enable

using System;
using System.IO;
using Esprima;
using Esprima.Ast;

namespace Jint.Runtime.Modules;

public class DefaultModuleLoader : IModuleLoader
{
    public virtual Module LoadModule(Engine engine, ResolvedSpecifier moduleResolution)
    {
        if (moduleResolution.Type != SpecifierType.File)
            throw new InvalidOperationException($"The default module loader can only resolve files. You can define modules directly to allow imports using Engine.DefineModule(). Attempted to resolve: '{moduleResolution.Specifier}'.");

        if (moduleResolution.Path == null)
            throw new NullReferenceException($"Cannot load a module that doesn't resolve to a path. Attempted to resolve: '{moduleResolution.Specifier}'.");

        var code = File.ReadAllText(moduleResolution.Path);

        Module module;
        try
        {
            var parserOptions = new ParserOptions(moduleResolution.Path)
            {
                AdaptRegexp = true,
                Tolerant = true
            };

            module = new JavaScriptParser(code, parserOptions).ParseModule();
        }
        catch (ParserException ex)
        {
            ExceptionHelper.ThrowSyntaxError(engine.Realm, $"Error while loading module: error in module '{moduleResolution.Path ?? moduleResolution.Specifier}': {ex.Error}");
            module = null;
        }

        return module;
    }
}
