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

    /// <summary>
    /// The program <paramref name="node"/> was parsed as part of, or <see langword="null"/> when
    /// <paramref name="scriptOrModule"/> is not the one the node came from.
    /// </summary>
    /// <remarks>
    /// An execution context names the script or module whose code it runs, but code the engine reaches
    /// through <c>eval</c> or the <c>Function</c> constructor is a program of its own that no context names
    /// — so a node from such a program would otherwise be attributed to whichever script ran it, at a
    /// position that script does not have. The node has to lie in the program, by source name and by range,
    /// before the answer is given at all.
    /// </remarks>
    internal static Program? OwningProgramOf(this IScriptOrModule? scriptOrModule, Node node)
    {
        if (scriptOrModule?.Program is not { } program)
        {
            return null;
        }

        if (!string.Equals(node.Location.SourceFile, program.Location.SourceFile, StringComparison.Ordinal))
        {
            return null;
        }

        return node.Start >= program.Start && node.End <= program.End ? program : null;
    }
}
