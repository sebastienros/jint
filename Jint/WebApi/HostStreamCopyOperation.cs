#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi;

/// <summary>
/// A copy of a script's <c>ReadableStream</c> into a host <see cref="Stream"/> in progress, handed back by
/// <c>Engine.Advanced.StartReadableStreamCopy</c>. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// <b>The engine makes progress on the copy only when it is given turns.</b> Every chunk comes out of the
/// engine, so the copy advances on the event loop and nowhere else: a host with a thread it must not block —
/// a game loop, a UI thread — drives it by calling <c>engine.Advanced.ProcessTasks()</c> and watching
/// <see cref="IsCompleted"/>. A host that would rather await something has
/// <c>Engine.Advanced.CopyReadableStreamAsync</c>, which drives the same operation while it waits. This is
/// the contract <c>Engine.Modules.StartImport</c> and <c>ImportAsync</c> already have, and it is deliberately
/// the same one.
/// </para>
/// <para>
/// <b>Every member here is engine-thread-only</b>, including the plain property reads: they are read against
/// engine state and one of them can complete the operation (see <see cref="IsCompleted"/>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // once
/// _copy = engine.Advanced.StartReadableStreamCopy(stream, File.Create(path));
///
/// // every frame
/// engine.Advanced.ProcessTasks();
/// if (_copy.IsCompleted)
/// {
///     var written = _copy.GetResult();   // throws PromiseRejectedException if the copy failed
///     _copy = null;
/// }
/// </code>
/// </example>
public sealed class HostStreamCopyOperation
{
    private readonly Engine _engine;

    /// <summary>
    /// The evaluation cycle the copy was started in. Once the engine has moved past it, no turn of the event
    /// loop can finish this copy; see <see cref="ObserveAbandonment"/>.
    /// </summary>
    private readonly int _generation;

    private bool _completed;
    private bool _faulted;
    private long _bytesWritten;
    private JsValue? _error;

    internal HostStreamCopyOperation(Engine engine, JsValue promise)
    {
        _engine = engine;
        _generation = engine.EventLoopGeneration;
        Promise = promise;
    }

    /// <summary>
    /// The promise the copy settles into: fulfilled with <c>undefined</c>, or rejected with the error the
    /// copy failed with. Useful for handing the copy to script; a host tracking it from .NET wants
    /// <see cref="IsCompleted"/>.
    /// </summary>
    /// <remarks>
    /// The promise is marked as handled when the copy starts, because this operation — not the promise — is
    /// the channel a host reads the outcome from, and a copy nobody attached a handler to must not read as an
    /// unhandled rejection. A copy abandoned by <c>Engine.Advanced.RestoreGlobalSnapshot</c> leaves it
    /// pending forever: settling it would run the ended cycle's reactions against the restored globals, which
    /// is the very thing the restore fenced off.
    /// </remarks>
    public JsValue Promise { get; }

    /// <summary>
    /// Whether the copy has finished, successfully or not. Becomes true during a turn of the event loop, so
    /// it is only worth re-reading after the engine has been given one.
    /// </summary>
    /// <remarks>
    /// There is one way for a copy to end without a turn: <c>Engine.Advanced.RestoreGlobalSnapshot</c> ends
    /// the evaluation cycle the copy was started in, and nothing fenced off that way can settle into the
    /// engine again. Such a copy is reported here as completed and <see cref="IsFaulted"/>, so a host polling
    /// this cannot poll forever. Its destination has already been released.
    /// </remarks>
    public bool IsCompleted
    {
        get
        {
            ObserveAbandonment();
            return _completed;
        }
    }

    /// <summary>Whether the copy finished by failing.</summary>
    public bool IsFaulted
    {
        get
        {
            ObserveAbandonment();
            return _faulted;
        }
    }

    /// <summary>
    /// How many bytes have reached the host's stream so far. Final once <see cref="IsCompleted"/> is true;
    /// for a failed copy it is how far the copy got before it failed.
    /// </summary>
    public long BytesWritten
    {
        get
        {
            ObserveAbandonment();
            return _bytesWritten;
        }
    }

    /// <summary>
    /// The error the copy failed with once it has failed, otherwise null. The script's own stream error, a
    /// <c>TypeError</c> carrying the host stream's <see cref="Exception"/> (readable with
    /// <c>JintException.TryGetClrException</c>), or an <c>AbortError</c> <c>DOMException</c> for a cancelled
    /// copy.
    /// </summary>
    public JsValue? Error
    {
        get
        {
            ObserveAbandonment();
            return _error;
        }
    }

    /// <summary>
    /// How many bytes the copy wrote.
    /// </summary>
    /// <exception cref="InvalidOperationException">The copy has not finished yet.</exception>
    /// <exception cref="PromiseRejectedException">The copy failed.</exception>
    public long GetResult()
    {
        if (!IsCompleted)
        {
            Throw.InvalidOperationException("The stream copy has not completed. Give the engine turns with engine.Advanced.ProcessTasks() until IsCompleted is true, or await Engine.Advanced.CopyReadableStreamAsync instead.");
        }

        if (IsFaulted)
        {
            throw new PromiseRejectedException(_error!);
        }

        return _bytesWritten;
    }

    /// <summary>
    /// Fails a copy the engine has fenced off, deriving the fact from the engine's generation on read.
    /// </summary>
    /// <remarks>
    /// <c>Engine.Advanced.RestoreGlobalSnapshot</c> ends the evaluation cycle, and every job registered in it
    /// is discarded at dequeue rather than run — so the reads and writes that would finish this copy are
    /// exactly the ones that can no longer happen. Nothing pushes that news: the documented contract is that
    /// the host polls, and deriving it on read is what keeps that poll from being a poll forever. It is the
    /// same reasoning, and the same shape, as <c>ModuleImportOperation</c>'s.
    /// </remarks>
    private void ObserveAbandonment()
    {
        if (_completed || _engine.EventLoopGeneration == _generation)
        {
            return;
        }

        Fail(_engine.Realm.Intrinsics.Error.Construct(
            "The stream copy was abandoned: Engine.Advanced.RestoreGlobalSnapshot ended the evaluation cycle it was started in, so nothing it is waiting for can reach this engine any more. The destination stream has been released. Start the copy again on the restored engine."));
    }

    internal void Fulfil()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
    }

    internal void Fail(JsValue error)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _faulted = true;
        _error = error;
    }

    /// <summary>
    /// Records progress as the copy runs, so <see cref="BytesWritten"/> is meaningful before the end.
    /// </summary>
    internal void Advance(long bytes) => _bytesWritten += bytes;
}
#endif
