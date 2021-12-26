#nullable enable

using System;
using System.IO;
using Esprima;
using Esprima.Ast;

namespace Jint.Runtime.Modules;

public class DefaultModuleLoader : IModuleLoader
{
    private readonly Uri _basePath;

    public DefaultModuleLoader(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            ExceptionHelper.ThrowArgumentException("Base path must be provided");
        }

        if (!Directory.Exists(basePath))
        {
            ExceptionHelper.ThrowArgumentException($"Base path {basePath} does not exist");
        }

        // ensure we have trailing slash to ensure uri combining works
        var path = new Uri(basePath.TrimEnd('/', '\\') + "/", UriKind.RelativeOrAbsolute);
        if (!path.IsAbsoluteUri)
        {
            ExceptionHelper.ThrowArgumentException("Base path must be an absolute path");
        }
        _basePath = path;
    }

    public virtual ModuleLoaderResult LoadModule(Engine engine, string location, string? referencingLocation)
    {
        // If no referencing location is provided, ensure location is absolute

        var locationUri = referencingLocation == null
            ? new Uri(location, UriKind.RelativeOrAbsolute)
            : new Uri(new Uri(referencingLocation, UriKind.RelativeOrAbsolute), location);

        if (!locationUri.IsAbsoluteUri)
        {
            locationUri = new Uri(_basePath, locationUri);
        }

        // Ensure the resulting resource is under the base path if it is provided

        if (!_basePath.IsBaseOf(locationUri))
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

        var path = ToFilePath(location);
        return File.ReadAllText(path);
    }

    protected virtual string ToFilePath(Uri location)
    {
        var path = location.AbsolutePath;

        if (!File.Exists(path) && !Path.HasExtension(path))
        {
            path += ".js";
        }

        return path;
    }
}
