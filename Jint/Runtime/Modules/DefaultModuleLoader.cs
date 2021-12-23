#nullable enable

using System;
using System.IO;
using Esprima;
using Esprima.Ast;

namespace Jint.Runtime.Modules;

public class DefaultModuleLoader : IModuleLoader
{
    private readonly string _basePath;

    public DefaultModuleLoader(string basePath)
    {
        _basePath = basePath;
    }

    public virtual ModuleLoaderResult LoadModule(Engine engine, string location, string? referencingLocation)
    {
        // If no referencing location is provided, ensure location is absolute

        var locationUri = referencingLocation == null 
            ? new Uri(location, UriKind.Absolute) 
            : new Uri(new Uri(referencingLocation, UriKind.Absolute), location)
            ;

        // Ensure the resulting resource is under the base path if it is provided

        if (!String.IsNullOrEmpty(_basePath) && !locationUri.AbsolutePath.StartsWith(_basePath, StringComparison.Ordinal))
        {
            ExceptionHelper.ThrowArgumentException("Invalid file location.");
        }

        return LoadModule(engine, locationUri);
    }

    protected virtual ModuleLoaderResult LoadModule(Engine engine, Uri location)
    {
        var code = LoadModuleSourceCode(location);

        Module module;
        try
        {
            var parserOptions = new ParserOptions(location.ToString())
            {
                AdaptRegexp = true,
                Tolerant = true
            };

            module = new JavaScriptParser(code, parserOptions).ParseModule();
        }
        catch (ParserException ex)
        {
            ExceptionHelper.ThrowSyntaxError(engine.Realm, $"Error while loading module: error in module '{location}': {ex.Error}");
            module = null;
        }

        return new ModuleLoaderResult(module, location);
    }

    protected virtual string LoadModuleSourceCode(Uri location)
    {
        if (!location.IsFile)
        {
            ExceptionHelper.ThrowArgumentException("Only file loading is supported");
        }

        return File.ReadAllText(location.AbsolutePath);
    }
}
