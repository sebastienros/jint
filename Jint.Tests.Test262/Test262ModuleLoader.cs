#nullable enable

using Jint.Runtime;
using Jint.Runtime.Modules;
using Zio;

namespace Jint.Tests.Test262;

internal sealed class Test262ModuleLoader : ModuleLoader
{
    private readonly IFileSystem _fileSystem;
    private readonly string _basePath;

    public Test262ModuleLoader(IFileSystem fileSystem, string basePath)
    {
        _fileSystem = fileSystem;
        _basePath = "/test/" + basePath.TrimStart('\\').TrimStart('/');
    }

    public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        return new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);
    }

    protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
    {
        lock (_fileSystem)
        {
            var fileName = Path.Combine(_basePath, resolved.Key).Replace('\\', '/');
            if (!_fileSystem.FileExists(fileName))
            {
                ExceptionHelper.ThrowModuleResolutionException("Module Not Found", resolved.ModuleRequest.Specifier, parent: null, fileName);
            }
            using var stream = new StreamReader(_fileSystem.OpenFile(fileName, FileMode.Open, FileAccess.Read));
            return stream.ReadToEnd();
        }
    }
}
