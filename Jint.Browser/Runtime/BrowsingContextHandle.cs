using Jint.Browser.Dom;

namespace Jint.Browser.Runtime;

/// <summary>
/// An engine-free identity reserved before a popup's loop starts. Operations wait for registration and
/// enter the destination in FIFO order; neither a pending operation nor this handle carries a JsValue.
/// </summary>
internal sealed class BrowsingContextHandle
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource<Page?> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _tail = Task.CompletedTask;
    private Task _navigationTail = Task.CompletedTask;
    private volatile Page? _page;
    private volatile Page? _opener;
    private volatile string _name;
    private volatile DomDocumentOrigin _origin;
    private volatile bool _failed;

    internal BrowsingContextHandle(string name = "", Page? opener = null, bool scriptClosable = false,
        DomDocumentOrigin? origin = null)
    {
        _name = name;
        _origin = origin ?? DomDocumentOrigin.Opaque();
        _opener = opener;
        ScriptClosable = scriptClosable;
    }

    internal string Name { get => _name; set => _name = value; }
    internal Page? Opener => _opener;
    internal DomDocumentOrigin Origin { get => _origin; set => _origin = value; }
    internal bool ScriptClosable { get; }
    internal bool Closed => _failed || _page?.IsClosed == true;
    internal Task Opening { get; set; } = Task.CompletedTask;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/document-sequences.html#familiar-with
    /// https://html.spec.whatwg.org/multipage/browsing-the-web.html#allowed-to-navigate
    /// </summary>
    internal bool CanBeNamedTarget(Page source, DomDocumentOrigin sourceOrigin)
    {
        if (source.IsClosed || Closed) return false;

        // These handles represent unsandboxed top-level pages only. HTML's allowed-by-sandboxing
        // check permits their cross-origin navigation (step 4); origin is a familiarity check,
        // not an additional navigation restriction. Child-frame sandbox policy is not modeled here.
        // For top-level contexts familiarity is same origin, self, or familiarity with the target's
        // opener. Walk the opener chain without reading any other page's engine or mutable DOM.
        for (BrowsingContextHandle? target = this; target is not null && !target.Closed;
             target = target.Opener?.WindowHandle)
        {
            if (ReferenceEquals(source.WindowHandle, target) || sourceOrigin.IsSameOrigin(target.Origin))
                return true;
        }
        return false;
    }

    internal void Disown() => _opener = null;

    internal void Register(Page page) => _page = page;

    internal void Complete(Page? page)
    {
        _page = page;
        _failed = page is null;
        _ready.TrySetResult(page);
    }

    internal void Enqueue(Page source, Func<Page, Task> operation)
    {
        lock (_gate)
        {
            var previous = _tail;
            _tail = Task.Run(async () =>
            {
                await previous.ConfigureAwait(false);
                var page = await _ready.Task.ConfigureAwait(false);
                if (page is not null) await ApplyAsync(source, page, operation).ConfigureAwait(false);
            });
        }
    }

    internal void Navigate(Page source, CrossPageNavigation navigation)
        => Enqueue(source, page =>
        {
            // Navigation has a separate FIFO: a fetch must not hold up messages or script close.
            var previous = _navigationTail;
            _navigationTail = NavigateAfterAsync(source, page, previous, navigation);
            return Task.CompletedTask;
        });

    private static async Task NavigateAfterAsync(Page source, Page page, Task previous, CrossPageNavigation navigation)
    {
        await previous.ConfigureAwait(false);
        await ApplyAsync(source, page, target => target.RequestCrossPageNavigationAsync(navigation)).ConfigureAwait(false);
    }

    private static async Task ApplyAsync(Page source, Page page, Func<Page, Task> operation)
    {
        if (page.IsClosed) return;
        try
        {
            await operation(page).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) when (page.IsClosed)
        {
            // Closing wins over queued cross-page operations.
        }
        catch (OperationCanceledException) when (page.IsClosed)
        {
        }
        catch (Exception exception)
        {
            source.RecordPopupError(exception);
        }
    }

    internal void Close(Page source)
    {
        if (ScriptClosable) Enqueue(source, page => page.CloseFromScriptAsync(defer: ReferenceEquals(source, page)));
    }
}

/// <summary>The source document's frozen, engine-free navigation inputs.</summary>
internal sealed record CrossPageNavigation(
    string Url,
    string SourceUrl,
    string Referrer,
    DomDocumentOrigin Origin,
    string BaseUrl,
    bool Replace = false,
    byte[]? Body = null,
    string? ContentType = null);
