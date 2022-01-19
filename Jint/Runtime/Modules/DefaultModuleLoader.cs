#nullable enable

using System;
using System.IO;
using Esprima;
using Esprima.Ast;

namespace Jint.Runtime.Modules;

public class DefaultModuleLoader : IModuleLoader
{
    public Uri BasePath { get; }

    public DefaultModuleLoader(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(basePath));
        if (!Path.IsPathRooted(basePath)) throw new ArgumentException("Path must be rooted", nameof(basePath));

        basePath = Path.GetFullPath(basePath);
        if (basePath[basePath.Length - 1] != Path.DirectorySeparatorChar) basePath += Path.DirectorySeparatorChar;

        BasePath = new Uri(basePath, UriKind.Absolute);
    }

    public virtual ModuleLoaderResult LoadModule(Engine engine, string location)
    {
        if (!location.StartsWith("/"))
            throw new InvalidOperationException($"Cannot load module from location '{location}' because it does not start with '/'. Was this path resolved before being loaded?");

        var locationUri = new Uri(BasePath, $".{location}");

        // Ensure the resulting resource is under the base path if it is provided
        if (!BasePath.IsBaseOf(locationUri))
            ExceptionHelper.ThrowArgumentException($"Cannot import files outside of the base path '{BasePath}'. Imported location: '{locationUri.LocalPath}'");

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
