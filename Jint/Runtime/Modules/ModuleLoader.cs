#nullable enable

using System;
using System.IO;
using Esprima;
using Esprima.Ast;

namespace Jint.Runtime.Modules;

public class ModuleLoader : IModuleLoader
{
    private readonly Uri _defaultReferencingLocation;

    public ModuleLoader(string defaultBasePath)
    {
        _defaultReferencingLocation = new Uri(defaultBasePath);
    }

    public virtual ModuleLoaderResult LoadModule(Engine engine, string location, string? referencingLocation)
    {
        var referencingUri = referencingLocation != null
            ? new Uri(referencingLocation)
            : _defaultReferencingLocation;

        var locationUri = CalculateAbsolutePath(referencingUri, location);

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
            ExceptionHelper.ThrowNotImplementedException("Only file loading is supported");
        }

        return File.ReadAllText(location.AbsolutePath);
    }

    protected virtual Uri CalculateAbsolutePath(Uri referencingUri, string location)
    {
        // Check whether we have a network url or a file url
        var locationUri = new Uri(location, UriKind.RelativeOrAbsolute);
        if (locationUri.IsAbsoluteUri)
        {
            return locationUri;
        }

        return new Uri(referencingUri, locationUri);
    }
}