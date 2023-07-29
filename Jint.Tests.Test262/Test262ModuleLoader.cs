#nullable enable

using Esprima;
using Esprima.Ast;
using Jint.Runtime;
using Jint.Runtime.Modules;
using Zio;

namespace Jint.Tests.Test262;

internal sealed class Test262ModuleLoader : IModuleLoader
{
    private readonly IFileSystem _fileSystem;
    private readonly string _basePath;

    public Test262ModuleLoader(IFileSystem fileSystem, string basePath)
    {
        _fileSystem = fileSystem;
        _basePath = "/test/" + basePath.TrimStart('\\').TrimStart('/');
    }

    public ResolvedSpecifier Resolve(string? referencingModuleLocation, string specifier)
    {
        return new ResolvedSpecifier(referencingModuleLocation ?? "", specifier ?? "", null, SpecifierType.Bare);
    }

    public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        Module module;
        try
        {
            string code;
            lock (_fileSystem)
            {
                var fileName = Path.Combine(_basePath, resolved.Key).Replace('\\', '/');
                using var stream = new StreamReader(_fileSystem.OpenFile(fileName, FileMode.Open, FileAccess.Read));
                code = stream.ReadToEnd();
            }

            var parserOptions = new ParserOptions
            {
                RegExpParseMode = RegExpParseMode.AdaptToInterpreted,
                Tolerant = true
            };

            module = new JavaScriptParser(parserOptions).ParseModule(code, source: resolved.Uri?.LocalPath!);
        }
        catch (ParserException ex)
        {
            ExceptionHelper.ThrowSyntaxError(engine.Realm, $"Error while loading module: error in module '{resolved.Uri?.LocalPath}': {ex.Error}");
            module = null;
        }
        catch (Exception ex)
        {
            var message = $"Could not load module {resolved.Uri?.LocalPath}: {ex.Message}";
            ExceptionHelper.ThrowJavaScriptException(engine, message, (Location) default);
            module = null;
        }

        return module;
    }
}
