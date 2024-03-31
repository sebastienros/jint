using Module = Jint.Runtime.Modules.Module;

namespace Jint.Runtime;

internal static class ScriptOrModuleExtensions
{
    public static Module AsModule(this IScriptOrModule? scriptOrModule, Engine engine, in SourceLocation location)
    {
        if (scriptOrModule is not Module module)
        {
            ExceptionHelper.ThrowSyntaxError(engine.Realm, "Cannot use import/export statements outside a module", location);
            return default!;
        }
        return module;
    }
}
