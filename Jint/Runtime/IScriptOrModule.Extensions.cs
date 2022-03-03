#nullable enable

using Esprima;
using Jint.Runtime.Modules;

namespace Jint.Runtime;

internal static class ScriptOrModuleExtensions
{
    public static JsModule AsModule(this IScriptOrModule? scriptOrModule, Engine engine, Location location)
    {
        var module = scriptOrModule as JsModule;
        if (module == null)
        {
            ExceptionHelper.ThrowSyntaxError(engine.Realm, "Cannot use import/export statements outside a module", location);
            return default!;
        }
        return module;
    }
}
