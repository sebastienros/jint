using Jint.NodeCompat;
using Jint.Runtime;

// ReSharper disable once CheckNamespace
namespace Jint;

/// <summary>
/// Enables the opt-in Node-style <c>process</c> global.
/// </summary>
public static class NodeProcessOptionsExtensions
{
    /// <summary>
    /// Exposes a minimal Node-compatible <c>process</c> object, so that a script written for Node can read
    /// the environment, ask which platform it is on and queue work with <c>process.nextTick</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No environment variable is exposed until you list one.</b>
    /// <see cref="NodeProcessOptions.EnvironmentVariableAllowlist"/> is empty by default, so
    /// <c>process.env</c> starts out an empty object — which is what makes this safe to enable for a script
    /// you did not write. Everything else is bounded the same way: <c>argv</c> is empty, <c>cwd()</c> answers
    /// <see cref="NodeProcessOptions.WorkingDirectory"/> and never the real directory, and there is no
    /// <c>exit</c>, <c>abort</c> or <c>kill</c> at all.
    /// </para>
    /// <para>
    /// The global is installed lazily and <b>non-clobbering</b>: a <c>process</c> the host registered itself,
    /// through <c>options.Configure(e =&gt; e.SetValue(...))</c> or <c>options.AddLazyGlobal(...)</c>, is left
    /// exactly as the host left it, whichever order the two calls were made in.
    /// </para>
    /// <para>
    /// Unlike <c>Options.WebApi</c>, this needs no particular target framework: it compiles for every one
    /// Jint targets. It is Node compatibility rather than a web standard, which is also why it is not a
    /// <c>WebApiFeatures</c> flag.
    /// </para>
    /// <para>
    /// Calling this twice is harmless — the second install finds the name taken and does nothing — but the
    /// first call's configuration is the one that wins. Every value is read here, when this method returns,
    /// so mutating <paramref name="configure"/>'s argument afterwards changes nothing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new Engine(options => options.UseNodeProcess(p =>
    /// {
    ///     p.EnvironmentVariableAllowlist = ["NODE_ENV", "TZ"];
    ///     p.EnvironmentOverrides = new Dictionary&lt;string, string&gt; { ["NODE_ENV"] = "production" };
    /// }));
    ///
    /// engine.Evaluate("process.env.NODE_ENV"); // "production"
    /// </code>
    /// </example>
    /// <param name="options">Options to modify.</param>
    /// <param name="configure">
    /// Configures what <c>process</c> reports. Omitting it leaves every default in place, which is a
    /// <c>process</c> whose <c>env</c> is empty.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static Options UseNodeProcess(this Options options, Action<NodeProcessOptions>? configure = null)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        var processOptions = new NodeProcessOptions();
        configure?.Invoke(processOptions);

        // Snapshotted here rather than at engine build: the host owns the collections it handed over and may
        // go on mutating them, and Options is meant to be shared by concurrently constructed engines.
        var configuration = NodeProcessConfiguration.Snapshot(processOptions);

        options.Configure(engine => NodeProcess.Install(engine, configuration));
        return options;
    }
}
