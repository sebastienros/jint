namespace Jint.Runtime.Modules;

public class DefaultModuleLoader : ModuleLoader
{
    private readonly Uri _basePath;
    private readonly bool _restrictToBasePath;

    public DefaultModuleLoader(string basePath, bool restrictToBasePath = true)
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

        if (_basePath.AbsolutePath[^1] != '/')
        {
            var uriBuilder = new UriBuilder(_basePath);
            uriBuilder.Path += '/';
            _basePath = uriBuilder.Uri;
        }
    }

    public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        var specifier = moduleRequest.Specifier;
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
            var baseUri = BuildBaseUri(referencingModuleLocation);
            resolved = new Uri(baseUri, specifier);
        }
        else if (specifier[0] == '#')
        {
            ExceptionHelper.ThrowNotSupportedException($"PACKAGE_IMPORTS_RESOLVE is not supported: '{specifier}'");
            return default;
        }
        else
        {
            return new ResolvedSpecifier(
                moduleRequest,
                specifier,
                Uri: null,
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
            moduleRequest,
            resolved.AbsoluteUri,
            resolved,
            SpecifierType.RelativeOrAbsolute
        );
    }

    private Uri BuildBaseUri(string? referencingModuleLocation)
    {
        if (referencingModuleLocation is not null)
        {
            /*
              "referencingModuleLocation" might be relative or an invalid URI when a module imports other
               modules and the importing module is called directly from .NET code.
               e.g. "engine.Modules.Import("my-module")" and "my-module" imports other modules.

               Path traversal prevention is not a concern here because it is checked later
               (if _restrictToBasePath is set to true).
            */
            if (Uri.TryCreate(referencingModuleLocation, UriKind.Absolute, out var referencingLocation) ||
                Uri.TryCreate(_basePath, referencingModuleLocation, out referencingLocation))
                return referencingLocation;
        }
        return _basePath;
    }

    protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
    {
        var specifier = resolved.ModuleRequest.Specifier;
        if (resolved.Type != SpecifierType.RelativeOrAbsolute)
        {
            ExceptionHelper.ThrowNotSupportedException($"The default module loader can only resolve files. You can define modules directly to allow imports using {nameof(Engine)}.{nameof(Engine.Modules.Add)}(). Attempted to resolve: '{specifier}'.");
        }

        if (resolved.Uri == null)
        {
            ExceptionHelper.ThrowInvalidOperationException($"Module '{specifier}' of type '{resolved.Type}' has no resolved URI.");
        }

        var fileName = Uri.UnescapeDataString(resolved.Uri.AbsolutePath);
        if (!File.Exists(fileName))
        {
            ExceptionHelper.ThrowModuleResolutionException("Module Not Found", specifier, parent: null, fileName);
        }

        return File.ReadAllText(fileName);
    }

    private static bool IsRelative(string specifier)
    {
        return specifier.StartsWith('.') || specifier.StartsWith('/');
    }
}
