using System;
using System.Reflection;
using System.Collections.Generic;

namespace Jint.Runtime.Modules;

internal sealed class DefaultModuleLoader : IModuleLoader
{
    private readonly List<IModuleSource> _moduleSources = new();
    private readonly IModuleSource _fileModuleSource = FileModuleSource.Instance;

    public void AddModuleSource(params IModuleSource[] moduleSources)
    {
        if (moduleSources is null)
        {
            ExceptionHelper.ThrowArgumentNullException(nameof(moduleSources));
        }

        if (moduleSources.Length == 0)
        {
            return;
        }

        for (var i = 0; i < moduleSources.Length; i++)
        {
            var moduleSource = moduleSources[i];

            if (_moduleSources.Contains(moduleSource))
            {
                continue;
            }

            _moduleSources.Add(moduleSource);
        }
    }

    public bool TryLoadModule(string location, string referencingLocation, out string moduleSource, out string moduleLocation)
    {
        moduleSource = null;
        Uri referencingUri;
        if (referencingLocation is null)
        {
            referencingUri = new Uri(Assembly.GetEntryAssembly().CodeBase);
        }
        else
        {
            referencingUri = new Uri(referencingLocation);
        }

        var locationUri = CalculateAbsolutePath(referencingUri, location);

        var loaded = false;
        if (locationUri.Scheme == Uri.UriSchemeFile)
        {
            loaded = _fileModuleSource.TryLoadModuleSource(locationUri, out moduleSource);
        }

        if (loaded)
        {
            moduleLocation = locationUri.AbsoluteUri;
            return true;
        }

        if (_moduleSources.Count == 0)
        {
            moduleLocation = null;
            return false;
        }

        for (var i = 0; i < _moduleSources.Count; i++)
        {
            var moduleSourceLoader = _moduleSources[i];

            if (moduleSourceLoader.TryLoadModuleSource(locationUri, out moduleSource))
            {
                moduleLocation = locationUri.AbsoluteUri;
                return true;
            }
        }

        moduleLocation = null;
        return false;
    }

    private static Uri CalculateAbsolutePath(Uri referencingUri, string location)
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