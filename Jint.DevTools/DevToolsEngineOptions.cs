namespace Jint.DevTools;

/// <summary>
/// What <see cref="DevToolsOptionsExtensions.UseDevTools"/> turns on beyond the parts every session needs.
/// </summary>
public sealed class DevToolsEngineOptions
{
    /// <summary>Creates a set of options, each at its default.</summary>
    public DevToolsEngineOptions()
    {
    }

    /// <summary>
    /// Gets or sets whether the engine counts what it executes, so a client can ask for coverage. Defaults
    /// to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Off unless asked because it is the one switch here with a running cost the whole engine pays: it
    /// arms the interpreter's per-statement lane for every script, attached or not. The rest of what
    /// <see cref="DevToolsOptionsExtensions.UseDevTools"/> sets costs a session that never attaches nothing.
    /// </remarks>
    public bool Coverage { get; set; }
}

/// <summary>
/// Configures an <see cref="Options"/> so that an engine built from it can be attached to.
/// </summary>
public static class DevToolsOptionsExtensions
{
    /// <summary>
    /// Turns on everything a DevTools session needs from the engine, and nothing a client cannot use.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="configure">What else to turn on, or <see langword="null"/> for the defaults.</param>
    /// <returns><paramref name="options"/>, so the call chains.</returns>
    /// <remarks>
    /// <para>
    /// Sets <see cref="Options.DebuggerOptions.Enabled"/> so a client can pause,
    /// <see cref="Options.RetainFunctionSourceText"/> so it can read the source it is paused in, and
    /// <see cref="Options.ProfilingOptions.Enabled"/> so it can record a profile. Coverage is off unless
    /// <see cref="DevToolsEngineOptions.Coverage"/> asks for it.
    /// </para>
    /// <para>
    /// <b>All three are construction-time.</b> There is no way to turn them on later, so an engine built
    /// without this can be listed and evaluated in but not paused in, and the domains that need what is
    /// missing say so with an explicit refusal rather than answering something untrue.
    /// </para>
    /// <para>
    /// <b>They are not free.</b> Debug mode disarms the interpreter's tight-loop lane, and retained source
    /// text keeps one string per parsed program alive. That is the price of an attachable engine; an engine
    /// the host never intends to attach to should not be built with it.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// var engine = new Engine(options => options.UseDevTools());
    /// </code>
    /// </example>
    public static Options UseDevTools(this Options options, Action<DevToolsEngineOptions>? configure = null)
    {
        if (options is null)
        {
            Throw.ArgumentNull(nameof(options));
        }

        var devTools = new DevToolsEngineOptions();
        configure?.Invoke(devTools);

        options.Debugger.Enabled = true;
        options.RetainFunctionSourceText = true;
        options.Profiling.Enabled = true;

        if (devTools.Coverage)
        {
            options.Coverage.Enabled = true;
        }

        return options;
    }
}
