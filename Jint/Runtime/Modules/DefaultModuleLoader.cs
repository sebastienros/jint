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
        if (string.IsNullOrWhiteSpace(basePath)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(basePath));
        if (!Path.IsPathRooted(basePath)) throw new ArgumentException("Path must be rooted", nameof(basePath));

        basePath = Path.GetFullPath(basePath);
        if (basePath[basePath.Length - 1] != Path.DirectorySeparatorChar) basePath += Path.DirectorySeparatorChar;

        _basePath = new Uri(basePath, UriKind.Absolute);
    }

    public virtual ResolvedSpecifier Resolve(string? referencingModuleLocation, string specifier)
    {
        if (string.IsNullOrEmpty(specifier)) throw new ArgumentException("Value cannot be null or empty.", nameof(specifier));

        // https://nodejs.org/api/esm.html#resolution-algorithm
        if (IsRelative(specifier))
        {
            // TODO: Not tested; find official module resolve reference: https://www.typescriptlang.org/docs/handbook/module-resolution.html
            Uri uri;
            if (specifier[0] == '/')
                uri = new Uri(_basePath, $".{specifier}");
            else if(referencingModuleLocation != null)
                uri = new Uri(new Uri(referencingModuleLocation), specifier);
            else
                uri = new Uri(_basePath, specifier);

            var key = $"/{_basePath.MakeRelativeUri(uri)}";
            string path;
            if (Path.HasExtension(uri.LocalPath))
            {
                key = key.Substring(0, key.LastIndexOf('.'));
                path = uri.LocalPath;
            }
            else
            {
                path = uri.LocalPath + ".js";
            }

            return new ResolvedSpecifier(
                specifier,
                key,
                path,
                SpecifierType.File
            );
        }
        else
        {
            return new ResolvedSpecifier(
                specifier,
                specifier,
                null,
                SpecifierType.Bare
            );
        }
    }

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

    private static bool IsRelative(string specifier)
    {
        return specifier.StartsWith("./") || specifier.StartsWith("../") || specifier.StartsWith("/");
    }
}
