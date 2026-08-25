using Jint.Runtime.Modules;

namespace Jint.Runtime;

internal static class ScriptOrModuleExtensions
{
    public static ModuleRecord AsModule(this IScriptOrModule? scriptOrModule, Engine engine, in SourceLocation location)
    {
        if (scriptOrModule is not ModuleRecord module)
        {
            Throw.SyntaxError(engine.Realm, "Cannot use import/export statements outside a module", in location);
            return default!;
        }
        return module;
    }
}
