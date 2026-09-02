using Jint.DevTools.Protocol;

namespace Jint.DevTools.Session;

/// <summary>
/// What brings a command from the thread that read it to the thread that may answer it.
/// </summary>
/// <remarks>
/// <para>
/// The one seam the thread rule needs. A transport thread has a request and a session; it may not touch the
/// engine those domains hold, so it hands both to a gateway and waits for the finished JSON. The only
/// implementation is <see cref="EngineDispatcher"/>, which posts the work onto the engine's own event loop;
/// a session with no engine behind it has no gateway and answers in place.
/// </para>
/// <para>
/// The request's <see cref="ProtocolRequest.Parameters"/> is a <c>JsonElement</c> into the document the
/// caller is still holding open, so a gateway may read it from another thread but may not keep it past the
/// call.
/// </para>
/// </remarks>
internal interface ICommandGateway
{
    /// <summary>Answers one command, wherever it has to run to be answered.</summary>
    ValueTask<string> DispatchAsync(DevToolsSession session, ProtocolRequest request, CommandContext context);
}
