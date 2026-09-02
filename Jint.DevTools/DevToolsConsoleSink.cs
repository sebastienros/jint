using Jint.WebApi;

namespace Jint.DevTools;

/// <summary>
/// The sink <see cref="DevToolsOptionsExtensions.UseDevTools"/> installs: it forwards everything to whatever
/// the host had, and hands the record to the target a client is attached to.
/// </summary>
/// <remarks>
/// <para>
/// <b>It wraps rather than replaces.</b> A host that set its own sink keeps every line it was getting, in
/// the same overload it was getting it in; attaching a client adds a reader and changes nothing about the
/// existing one.
/// </para>
/// <para>
/// <b>One sink reaches one engine.</b> The engine reads its sink out of <c>Options</c> on every emit and the
/// record carries no engine, so a sink shared by two engines could not tell which of them was talking. It
/// therefore binds to the first target built over an engine and refuses a second engine's — and
/// <c>UseDevTools</c> installs a fresh one per call, so the ordinary
/// <c>new Engine(options =&gt; options.UseDevTools())</c> gives every engine its own. A host that builds two
/// engines from one <see cref="Options"/> instance and attaches to both gets console events from the first;
/// calling <c>UseDevTools</c> per engine is what avoids that.
/// </para>
/// <para>
/// <b>Everything here runs on the engine thread</b>, inside the <c>console</c> call. The values in the
/// record are the engine's, and what leaves this class is a journal entry that holds them exactly as a
/// remote-object handle does.
/// </para>
/// </remarks>
internal sealed class DevToolsConsoleSink : ConsoleSink
{
    private readonly ConsoleSink _inner;

    private Engine? _engine;
    private EngineTarget? _target;

    internal DevToolsConsoleSink(ConsoleSink? inner)
    {
        _inner = inner ?? Null;
    }

    /// <summary>Binds this sink to <paramref name="target"/>, unless it already speaks for another engine.</summary>
    /// <returns><see langword="true"/> when console records from now on reach <paramref name="target"/>.</returns>
    internal bool TryBind(EngineTarget target)
    {
        if (_engine is not null && !ReferenceEquals(_engine, target.Engine))
        {
            return false;
        }

        _engine = target.Engine;
        _target = target;
        return true;
    }

    /// <summary>Stops speaking for <paramref name="target"/>, if it is the one bound.</summary>
    internal void Unbind(EngineTarget target)
    {
        if (ReferenceEquals(_target, target))
        {
            _target = null;
        }
    }

    /// <inheritdoc/>
    public override void Write(ConsoleLogLevel level, string message) => _inner.Write(level, message);

    /// <inheritdoc/>
    public override void Write(in ConsoleRecord record)
    {
        // The host's sink first and unconditionally: what it was getting before a client attached is what it
        // gets after, and a protocol failure must not cost the host a log line.
        _inner.Write(in record);

        _target?.Record(in record);
    }
}
