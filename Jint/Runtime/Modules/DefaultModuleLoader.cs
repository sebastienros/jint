#nullable enable

using System;
using System.IO;
using Esprima;
using Esprima.Ast;

namespace Jint.Runtime.Modules;

public class DefaultModuleLoader : IModuleLoader
{
    private readonly Uri _basePath;
    private readonly bool _restrictToBasePath;

    public DefaultModuleLoader(string basePath, bool restrictToBasePath = true)
    {
        if (string.IsNullOrWhiteSpace(basePath)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(basePath));
        _restrictToBasePath = restrictToBasePath;

        if (!Uri.TryCreate(basePath, UriKind.Absolute, out _basePath))
        {
            if (!Path.IsPathRooted(basePath)) throw new ArgumentException("Path must be rooted", nameof(basePath));
            basePath = Path.GetFullPath(basePath);
            _basePath = new Uri(basePath, UriKind.Absolute);
        }

        if (_basePath.AbsolutePath[_basePath.AbsolutePath.Length - 1] != '/')
        {
            var uriBuilder = new UriBuilder(_basePath);
            uriBuilder.Path += '/';
            _basePath = uriBuilder.Uri;
        }
    }

    public virtual ResolvedSpecifier Resolve(string? referencingModuleLocation, string specifier)
    {
        if (string.IsNullOrEmpty(specifier)) throw new ArgumentException("Value cannot be null or empty.", nameof(specifier));

        // Specifications from ESM_RESOLVE Algorithm: https://nodejs.org/api/esm.html#resolution-algorithm

        Uri resolved;
        if (Uri.TryCreate(specifier, UriKind.Absolute, out var uri))
            resolved = uri;
        else if (IsRelative(specifier))
            resolved = new Uri(referencingModuleLocation != null ? new Uri(referencingModuleLocation, UriKind.Absolute) : _basePath, specifier);
        else if (specifier[0] == '#')
            throw new ModuleResolutionException($"PACKAGE_IMPORTS_RESOLVE is not supported", specifier, referencingModuleLocation);
        else
            return new ResolvedSpecifier(
                specifier,
                specifier,
                null,
                SpecifierType.Bare
            );

        if (resolved.IsFile)
        {
            if (resolved.UserEscaped)
                throw new ModuleResolutionException("Invalid Module Specifier", specifier, referencingModuleLocation);
            if(!Path.HasExtension(resolved.LocalPath))
                throw new ModuleResolutionException("Unsupported Directory Import", specifier, referencingModuleLocation);
            if(!FileExists(resolved))
                throw new ModuleResolutionException("Module Not Found", specifier, referencingModuleLocation);
        }

        if (_restrictToBasePath && !_basePath.IsBaseOf(resolved))
            throw new ModuleResolutionException($"Unauthorized Module Path", specifier, referencingModuleLocation);

        return new ResolvedSpecifier(
            specifier,
            resolved.AbsoluteUri,
            resolved,
            SpecifierType.RelativeOrAbsolute
        );
    }

    protected virtual bool FileExists(Uri uri)
    {
        return File.Exists(uri.LocalPath);
    }

    public virtual Module LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        if (resolved.Type != SpecifierType.RelativeOrAbsolute)
            throw new InvalidOperationException($"The default module loader can only resolve files. You can define modules directly to allow imports using Engine.DefineModule(). Attempted to resolve: '{resolved.Specifier}'.");

        if (resolved.Uri == null)
            throw new NullReferenceException($"Module '{resolved.Specifier}' of type '{resolved.Type}' has no resolved URI.");

        var code = ReadAllText(resolved.Uri);

        Module module;
        try
        {
            var parserOptions = new ParserOptions(resolved.Uri.LocalPath)
            {
                AdaptRegexp = true,
                Tolerant = true
            };

            module = new JavaScriptParser(code, parserOptions).ParseModule();
        }
        catch (ParserException ex)
        {
            ExceptionHelper.ThrowSyntaxError(engine.Realm, $"Error while loading module: error in module '{resolved.Uri.LocalPath}': {ex.Error}");
            module = null;
        }

        return module;
    }

    protected virtual string ReadAllText(Uri uri)
    {
        return File.ReadAllText(uri.LocalPath);
    }

    private static bool IsRelative(string specifier)
    {
        return specifier.StartsWith("./") || specifier.StartsWith("../") || specifier.StartsWith("/");
    }
}
