using Jint.Runtime.Modules;

#nullable enable

namespace Jint.Runtime;

public static class ScriptOrModuleExtensions
{
    public static JsModule? AsModule(this IScriptOrModule? scriptOrModule) => scriptOrModule as JsModule;
}
