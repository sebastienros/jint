#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Modules;
using Zio;

namespace Jint.Tests.Test262;

internal sealed class Test262ModuleLoader : ModuleLoader
{
    // Per test262 INTERPRETING.md, source-phase-import tests use the specifier "<module source>", which the
    // host must resolve to a module that exposes a [[ModuleSource]] (e.g. a WebAssembly module). Jint has no
    // WebAssembly support, so we resolve it to a trivial module and attach a synthetic %AbstractModuleSource%.
    private const string ModuleSourceSpecifier = "<module source>";

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

    protected override ObjectInstance? GetModuleSource(Engine engine, ResolvedSpecifier resolved)
    {
        if (!string.Equals(resolved.ModuleRequest.Specifier, ModuleSourceSpecifier, StringComparison.Ordinal))
        {
            return null;
        }

        // $262 (and thus $262.AbstractModuleSource) is installed before any module is imported. The synthetic
        // [[ModuleSource]] must have %AbstractModuleSource%.prototype on its chain so that the source-phase
        // binding passes `x instanceof $262.AbstractModuleSource`.
        if (engine.GetValue("$262") is not ObjectInstance dollar262
            || dollar262.Get("AbstractModuleSource") is not ObjectInstance ctor
            || ctor.Get("prototype") is not ObjectInstance prototype)
        {
            return null;
        }

        var source = engine.Realm.Intrinsics.Object.Construct(System.Array.Empty<JsValue>());
        source.SetPrototypeOf(prototype);
        return source;
    }

    protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
    {
        if (string.Equals(resolved.ModuleRequest.Specifier, ModuleSourceSpecifier, StringComparison.Ordinal))
        {
            // The body is irrelevant — only the attached [[ModuleSource]] is observed by the tests.
            return "export {};";
        }

        lock (_fileSystem)
        {
            var fileName = Path.Combine(_basePath, resolved.Key).Replace('\\', '/');
            if (!_fileSystem.FileExists(fileName))
            {
                Throw.ModuleResolutionException("Module Not Found", resolved.ModuleRequest.Specifier, parent: null, fileName);
            }
            using var stream = new StreamReader(_fileSystem.OpenFile(fileName, FileMode.Open, FileAccess.Read));
            return stream.ReadToEnd();
        }
    }

    protected override byte[] LoadModuleContentsAsBytes(Engine engine, ResolvedSpecifier resolved)
    {
        lock (_fileSystem)
        {
            var fileName = Path.Combine(_basePath, resolved.Key).Replace('\\', '/');
            if (!_fileSystem.FileExists(fileName))
            {
                Throw.ModuleResolutionException("Module Not Found", resolved.ModuleRequest.Specifier, parent: null, fileName);
            }
            using var stream = _fileSystem.OpenFile(fileName, FileMode.Open, FileAccess.Read);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
