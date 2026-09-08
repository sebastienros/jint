using Jint.WebApi.Fetch;

namespace Jint.Browser.Runtime;

#pragma warning disable JINT0002 // the fetch observer is the engine's own network seam; the body read is part of it

/// <summary>
/// The one bounded read of a paused response's body, as the protocol layer above sees it: the bytes, and the
/// page's own allowance they are charged to.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is valid only while the listener is deciding.</b> The engine seals the read as soon as the response
/// callback returns, so this must not be captured past the pause it belongs to — the same rule
/// <c>IPageNetworkListener</c> already states about everything else it is handed.
/// </para>
/// <para>
/// <b>Nothing here is a page value.</b> No engine, no <c>JsValue</c>, no realm, no node — it is a
/// <c>ReadOnlyMemory&lt;byte&gt;</c> and a reservation, which is what lets it be used from the transport
/// thread the pause is held on while the page loop is blocked on that very fetch.
/// </para>
/// <para>
/// <b>The allowance is the page's, not the command's.</b> A protocol parameter never widens it: reading a
/// body and encoding a reply both spend <c>BrowserOptions.MaxCapturedResponseBytes</c>, the same figure the
/// <c>Network</c> domain's captured bodies spend.
/// </para>
/// </remarks>
internal sealed class PageResponseBodyReader
{
    private readonly FetchResponseInterceptionContext _context;
    private readonly PageNetworkRecorder _ledger;

    internal PageResponseBodyReader(FetchResponseInterceptionContext context, PageNetworkRecorder ledger)
    {
        _context = context;
        _ledger = ledger;
    }

    /// <summary>
    /// Reads the whole body, or answers <see langword="null"/> because the page's allowance refused it.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the fetch is abandoned or the client goes away.</param>
    /// <remarks>
    /// A refusal is sticky for this response, and whatever was read is replayed to the page either way — the
    /// engine's own promise, and the reason a read is invisible to the document.
    /// </remarks>
    internal ValueTask<ReadOnlyMemory<byte>?> ReadAsync(CancellationToken cancellationToken)
        => _context.TryReadBodyAsync(_ledger, cancellationToken);

    /// <summary>
    /// Reserves <paramref name="bytes"/> of the same allowance for something other than the body — the
    /// encoded copy a reply is built from.
    /// </summary>
    /// <param name="bytes">How many bytes are about to be allocated.</param>
    /// <param name="lease">What gives them back, or <see langword="null"/> when nothing was granted.</param>
    /// <returns><see langword="true"/> when the allowance had room.</returns>
    internal bool TryReserve(int bytes, out IDisposable? lease) => _ledger.TryReserve(bytes, out lease);
}
