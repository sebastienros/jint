using Jint.NodeCompat;
using Jint.Runtime;

// ReSharper disable once CheckNamespace
namespace Jint;

/// <summary>
/// Enables the opt-in <c>node:</c> builtin modules.
/// </summary>
public static class NodeBuiltinModuleOptionsExtensions
{
    /// <summary>
    /// Makes Node's pure-string builtin modules importable: <c>node:path</c> (with <c>node:path/posix</c> and
    /// <c>node:path/win32</c>), <c>node:querystring</c> and <c>node:url</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After module resolution, the absence of these is the next wall a package published for Node runs into,
    /// and they are the ones it is cheapest to provide honestly: every function here computes its answer from
    /// its arguments, with no file system, process, network or clock behind it.
    /// </para>
    /// <para>
    /// <b>Nothing that touches a platform resource is provided</b>, and that is not a gap to be filled later:
    /// <c>node:fs</c>, <c>node:buffer</c>, <c>node:crypto</c>, <c>node:os</c>, <c>node:child_process</c>,
    /// <c>node:http</c> and their kind are deliberately absent, so a script feature-detecting one takes its
    /// other branch instead of walking into a stub. An unknown <c>node:</c> specifier fails with a message
    /// naming what is available.
    /// </para>
    /// <para>
    /// <b>It composes rather than replaces.</b> The engine's configured module loader keeps answering
    /// everything else, whichever loader that is and whichever order the calls were made in — pair it with
    /// <c>options.UseModules(new NodeStyleModuleLoader(dir))</c> and a package in <c>node_modules</c> can
    /// import <c>node:path</c>. It also needs no loader at all: an engine that enabled nothing else can still
    /// import the builtins, and everything else keeps failing as it did.
    /// </para>
    /// <para>
    /// <b>A module the host registered wins.</b> <c>engine.Modules.Add("node:path", …)</c> — or
    /// <c>Add("path", …)</c>, which resolves to the same key — replaces the builtin, and is also how a host
    /// supplies one of the modules Jint does not provide.
    /// </para>
    /// <para>
    /// <c>node:querystring</c> and <c>node:url</c> need .NET 8 or newer, because both build on the engine's
    /// WHATWG URL implementation. <c>node:path</c> is available on every target framework.
    /// </para>
    /// <para>
    /// Calling this twice is harmless; the last call's configuration is the one that wins. Every value is read
    /// here, when this method returns, so mutating <paramref name="configure"/>'s argument afterwards changes
    /// nothing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new Engine(options => options
    ///     .UseModules(new NodeStyleModuleLoader(@"C:\app"))
    ///     .UseNodeBuiltinModules());
    ///
    /// engine.Modules.Add("main", "import { join } from 'node:path'; export const p = join('a', 'b');");
    /// engine.Modules.Import("main").Get("p"); // "a/b" on a POSIX host
    /// </code>
    /// </example>
    /// <param name="options">Options to modify.</param>
    /// <param name="configure">
    /// Configures which platform <c>node:path</c> follows and what stands in for <c>process.cwd()</c>.
    /// Omitting it leaves every default in place.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static Options UseNodeBuiltinModules(this Options options, Action<NodeBuiltinModuleOptions>? configure = null)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        var moduleOptions = new NodeBuiltinModuleOptions();
        configure?.Invoke(moduleOptions);

        // Snapshotted here rather than at engine build: the host owns what it configured and may go on
        // changing it, and Options is meant to be shared by concurrently constructed engines.
        options._nodeBuiltinModules = NodeBuiltinModuleConfiguration.Snapshot(moduleOptions);
        return options;
    }
}
