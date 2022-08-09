using Esprima;
using Jint.Runtime.Modules;

namespace Jint.Runtime;

internal static class ScriptOrModuleExtensions
{
    public static ModuleRecord AsModule(this IScriptOrModule? scriptOrModule, Engine engine, Location location)
    {
        if (scriptOrModule is not ModuleRecord module)
        {
            ExceptionHelper.ThrowSyntaxError(engine.Realm, "Cannot use import/export statements outside a module", location);
            return default!;
        }
        return module;
    }
}
