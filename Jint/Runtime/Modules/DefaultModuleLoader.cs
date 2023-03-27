using Esprima;
using Esprima.Ast;

namespace Jint.Runtime.Modules;

public sealed class DefaultModuleLoader : IModuleLoader
{
    private readonly Uri _basePath;
    private readonly bool _restrictToBasePath;

    public DefaultModuleLoader(string basePath) : this(basePath, true)
    {

    }

    public DefaultModuleLoader(string basePath, bool restrictToBasePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            ExceptionHelper.ThrowArgumentException("Value cannot be null or whitespace.", nameof(basePath));
        }

        _restrictToBasePath = restrictToBasePath;

        if (!Uri.TryCreate(basePath, UriKind.Absolute, out var temp))
        {
            if (!Path.IsPathRooted(basePath))
            {
                ExceptionHelper.ThrowArgumentException("Path must be rooted", nameof(basePath));
            }

            basePath = Path.GetFullPath(basePath);
            _basePath = new Uri(basePath, UriKind.Absolute);
        }
        else
        {
            _basePath = temp;
        }

        if (_basePath.AbsolutePath[_basePath.AbsolutePath.Length - 1] != '/')
        {
            var uriBuilder = new UriBuilder(_basePath);
            uriBuilder.Path += '/';
            _basePath = uriBuilder.Uri;
        }
    }

    public ResolvedSpecifier Resolve(string? referencingModuleLocation, string specifier)
    {
        if (string.IsNullOrEmpty(specifier))
        {
            ExceptionHelper.ThrowModuleResolutionException("Invalid Module Specifier", specifier, referencingModuleLocation);
            return default;
        }

        // Specifications from ESM_RESOLVE Algorithm: https://nodejs.org/api/esm.html#resolution-algorithm

        Uri resolved;
        if (Uri.TryCreate(specifier, UriKind.Absolute, out var uri))
        {
            resolved = uri;
        }
        else if (IsRelative(specifier))
        {
            resolved = new Uri(referencingModuleLocation != null ? new Uri(referencingModuleLocation, UriKind.Absolute) : _basePath, specifier);
        }
        else if (specifier[0] == '#')
        {
            ExceptionHelper.ThrowNotSupportedException($"PACKAGE_IMPORTS_RESOLVE is not supported: '{specifier}'");
            return default;
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

        if (resolved.IsFile)
        {
            if (resolved.UserEscaped)
            {
                ExceptionHelper.ThrowModuleResolutionException("Invalid Module Specifier", specifier, referencingModuleLocation);
                return default;
            }

            if (!Path.HasExtension(resolved.LocalPath))
            {
                ExceptionHelper.ThrowModuleResolutionException("Unsupported Directory Import", specifier, referencingModuleLocation);
                return default;
            }
        }

        if (_restrictToBasePath && !_basePath.IsBaseOf(resolved))
        {
            ExceptionHelper.ThrowModuleResolutionException($"Unauthorized Module Path", specifier, referencingModuleLocation);
            return default;
        }

        return new ResolvedSpecifier(
            specifier,
            resolved.AbsoluteUri,
            resolved,
            SpecifierType.RelativeOrAbsolute
        );
    }

    public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        if (resolved.Type != SpecifierType.RelativeOrAbsolute)
        {
            ExceptionHelper.ThrowNotSupportedException($"The default module loader can only resolve files. You can define modules directly to allow imports using {nameof(Engine)}.{nameof(Engine.AddModule)}(). Attempted to resolve: '{resolved.Specifier}'.");
            return default;
        }

        if (resolved.Uri == null)
        {
            ExceptionHelper.ThrowInvalidOperationException($"Module '{resolved.Specifier}' of type '{resolved.Type}' has no resolved URI.");
        }
        var fileName = Uri.UnescapeDataString(resolved.Uri.AbsolutePath);
        if (!File.Exists(fileName))
        {
            ExceptionHelper.ThrowArgumentException("Module Not Found: ", resolved.Specifier);
            return default;
        }

        var code = File.ReadAllText(fileName);

        var source = resolved.Uri.LocalPath;
        Module module;
        try
        {
            module = new JavaScriptParser().ParseModule(code, source);
        }
        catch (ParserException ex)
        {
            ExceptionHelper.ThrowSyntaxError(engine.Realm, $"Error while loading module: error in module '{source}': {ex.Error}");
            module = null;
        }
        catch (Exception)
        {
            ExceptionHelper.ThrowJavaScriptException(engine, $"Could not load module {source}", (Location) default);
            module = null;
        }

        return module;
    }

    private static bool IsRelative(string specifier)
    {
        return specifier.StartsWith(".") || specifier.StartsWith("/");
    }
}
