namespace Jint.NodeCompat;

/// <summary>
/// Configuration for the opt-in <c>node:</c> builtin modules, installed by
/// <see cref="NodeBuiltinModuleOptionsExtensions.UseNodeBuiltinModules(Options, Action{NodeBuiltinModuleOptions}?)"/>.
/// </summary>
/// <remarks>
/// <para>
/// The modules provided are the ones npm code reaches for that are <b>pure string utilities</b>:
/// <c>node:path</c> (with <c>node:path/posix</c> and <c>node:path/win32</c>), <c>node:querystring</c> and
/// <c>node:url</c>. Nothing here touches a platform resource, which is the line drawn deliberately:
/// <c>node:fs</c>, <c>node:buffer</c>, <c>node:crypto</c>, <c>node:os</c>, <c>node:child_process</c>,
/// <c>node:http</c> and everything else that would hand a script the host's file system, memory or network are
/// <b>not</b> provided and will not be. An unknown <c>node:</c> specifier fails with a message naming what is
/// available.
/// </para>
/// <para>
/// <c>node:querystring</c> and <c>node:url</c> need .NET 8 or newer, because both are built on the engine's
/// WHATWG URL implementation, which is. <c>node:path</c> depends on nothing but <see cref="string"/> and is
/// available on every target framework Jint has. The failure message lists what the running build actually
/// provides, so it stays truthful either way.
/// </para>
/// <para>
/// Every value here is read once, when <c>UseNodeBuiltinModules</c> returns, and copied into an immutable
/// snapshot. Mutating this object afterwards changes nothing, which is what makes one <see cref="Options"/>
/// instance shared by concurrently constructed engines safe.
/// </para>
/// </remarks>
public sealed class NodeBuiltinModuleOptions
{
    private string _platform = NodePlatform.Default();
    private string _workingDirectory = "/";

    /// <summary>
    /// Which flavour <c>node:path</c> defaults to, as one of Node's platform strings. Defaults to the platform
    /// this process is running on, which is the same answer <c>process.platform</c> gives.
    /// </summary>
    /// <remarks>
    /// <c>win32</c> selects the Windows flavour and every other value selects the POSIX one, exactly as Node's
    /// own module does. <c>path.posix</c> and <c>path.win32</c> are reachable whatever this says, so a script
    /// that has to reason about the other platform's paths still can. Assigning <see langword="null"/> is read
    /// back as the detected default.
    /// </remarks>
    /// <seealso cref="NodeProcessOptions.Platform"/>
    public string Platform
    {
        get => _platform;
        set => _platform = value ?? NodePlatform.Default();
    }

    /// <summary>
    /// What <c>path.resolve()</c> and <c>path.relative()</c> use where Node reads <c>process.cwd()</c>.
    /// Defaults to <c>"/"</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The real current directory is never used</b>, for the reason
    /// <see cref="NodeProcessOptions.WorkingDirectory"/> gives: it names a deployment layout and often a user
    /// account, and a script resolving against it would be building absolute paths that mean something to the
    /// host and nothing to it. A host that wants the script to see a directory names one here — typically the
    /// same directory its module loader is rooted at, and the same string it gave the <c>process</c> shim.
    /// </para>
    /// <para>
    /// The value is used verbatim by the Windows flavour and, by the POSIX one, exactly as Node transforms it:
    /// when <see cref="Platform"/> is <c>win32</c> the separators are turned around and the drive letter
    /// dropped, so <c>C:\app</c> is seen as <c>/app</c>. Assigning <see langword="null"/> is read back as
    /// <c>"/"</c>.
    /// </para>
    /// </remarks>
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => _workingDirectory = value ?? "/";
    }

    /// <summary>
    /// Whether the un-prefixed spellings — <c>import 'path'</c> rather than <c>import 'node:path'</c> — name
    /// the builtins too. <see langword="true"/> by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what Node does: PACKAGE_RESOLVE step 3 of
    /// <see href="https://nodejs.org/api/esm.html#resolution-algorithm-specification">ESM_RESOLVE</see> says
    /// "if <em>specifier</em> is a Node.js builtin module name, return the string <em>'node:'</em> concatenated
    /// with <em>specifier</em>", so a builtin outranks any package of that name in <c>node_modules</c>. Published
    /// packages are full of both spellings, and the un-prefixed one is the older and still the commoner.
    /// </para>
    /// <para>
    /// Both spellings name <b>one</b> module either way: <c>path</c> resolves to the key <c>node:path</c>, so
    /// the two imports share a module record, and a host that registers its own module under <em>either</em>
    /// name with <c>Engine.Modules.Add</c> is found by both and takes precedence over the builtin.
    /// </para>
    /// <para>
    /// Turn it off for a tree that really does depend on a <c>node_modules</c> package called <c>path</c>,
    /// <c>url</c> or <c>querystring</c> — all three exist on npm as browser polyfills — or to keep the
    /// builtins reachable only by a spelling no package manager can shadow.
    /// </para>
    /// </remarks>
    public bool AllowUnprefixedSpecifiers { get; set; } = true;
}
