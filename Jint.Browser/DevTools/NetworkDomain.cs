using System.Text;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Network;
using Jint.DevTools.Session;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Network</c> domain: every request the page makes, and the few things a client may change about
/// them.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a second reader of the page's own request log, never a second observer on the transport.</b>
/// <c>Runtime/PageNetworkRecorder</c> is the engine's <c>FetchObserver</c> and it already sees the document,
/// every subresource the parser driver loads, every <c>fetch</c> and <c>XMLHttpRequest</c>, and a worker's
/// module loads; this domain is told about each step through <c>IPageNetworkListener</c> and turns it into an
/// event. <c>Page.Requests</c> and this domain therefore say the same thing about the same request, and the
/// identifiers agree with <c>Fetch</c>'s.
/// </para>
/// <para>
/// <b>Every method here runs on a transport thread, not the page loop.</b> A command reaches it on the loop
/// like any other, but the events do not — see <c>PageTarget.Network</c> — so the state it keeps is a couple
/// of flags and everything else lives on the target.
/// </para>
/// <para>
/// <b>What this browser cannot report, and says so rather than inventing.</b> HTTP cache hits are reported and can be bypassed or cleared.
/// There is no connection pool, so <c>connectionId</c> is zero and <c>connectionReused</c> false. Timing is
/// published for what the transport can see exactly and for nothing else: <c>requestTime</c> is when the hop
/// was handed over and <c>receiveHeadersStart</c>/<c>receiveHeadersEnd</c> when its headers were in, so a
/// client reads a real time to first byte, while proxy, DNS, connect, TLS, worker and push are <c>-1</c> —
/// the protocol's value for a phase that did not happen here — because a document of zeros would read as a
/// page that loaded instantly. A <c>WebSocket</c> takes the four events the protocol gives a socket — its
/// creation, both handshakes and its close — over the engine's own socket observer, and no
/// <c>webSocketFrame*</c> event, because that observer is never told about a frame. <c>EventSource</c>
/// produces none of its own: its stream is observed as bytes rather than as the events they decode into, so
/// its request is in the log as <c>ResourceType: EventSource</c> and its messages are nowhere.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Network/"/>.
/// </para>
/// </remarks>
internal sealed partial class NetworkDomain : NetworkDomainBase, IDetachableDomain
{
    private readonly PageTarget _target;

    internal NetworkDomain(PageTarget target)
    {
        _target = target;
    }

    /// <summary>Whether this attachment wants the log to keep bodies for it.</summary>
    /// <remarks>
    /// The protocol's own rule: a client that has not enabled the domain is not promised a body, and Chrome
    /// buffers only while it is enabled. It is what keeps a page with no client attached free of the copies.
    /// </remarks>
    internal bool WantsBodies => IsEnabled;

    private PageNetwork Network => _target.Page.Network;

    /// <inheritdoc/>
    /// <remarks>
    /// <c>maxTotalBufferSize</c>, <c>maxResourceBufferSize</c> and <c>maxPostDataSize</c> are accepted and
    /// not honoured: the buffer is the page's, bounded by <c>BrowserOptions.MaxCapturedResponseBytes</c>,
    /// which is the host's decision rather than a client's. A client that asked for less gets no more than it
    /// asked for by accident; one that asked for more is not given the page's memory.
    /// </remarks>
    protected override async ValueTask<EmptyResult> EnableAsync(EnableRequest parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        _target.AddNetworkDomain(this);
        _target.RefreshBodyCapture();
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        _target.RemoveNetworkDomain(this);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    void IDetachableDomain.Detach() => _target.RemoveNetworkDomain(this);

    /// <summary>Bypasses HTTP cache reads and writes for this page's requests.</summary>
    protected override ValueTask<EmptyResult> SetCacheDisabledAsync(SetCacheDisabledRequest parameters, CommandContext context)
    {
        _target.Emulation.CacheDisabled = parameters.CacheDisabled;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Clears the browser context's HTTP response cache.</summary>
    protected override ValueTask<EmptyResult> ClearBrowserCacheAsync(EmptyParameters parameters, CommandContext context)
    {
        _target.Page.Context.ClearHttpCache();
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Makes every request the page sends carry the headers a client named.</summary>
    /// <remarks>
    /// <b>The client's value wins and everything else is left alone.</b> A name it did not mention keeps
    /// whatever the transport computed — the <c>Cookie</c> the jar answered, the <c>Referer</c> the policy
    /// narrowed, the <c>Origin</c> the document has — because an override is a header a client wants sent,
    /// not a header list it wants substituted. It applies to the document's own request and to every
    /// subresource, <c>fetch</c> and <c>XMLHttpRequest</c>, and it is re-applied per redirect hop.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetExtraHTTPHeadersAsync(SetExtraHTTPHeadersRequest parameters, CommandContext context)
    {
        var headers = new List<PageHeader>(parameters.Headers.Count);
        foreach (var header in parameters.Headers)
        {
            headers.Add(new PageHeader(header.Key.ToLowerInvariant(), header.Value));
        }

        _target.UpdateNetworkPolicy(policy => policy with { ExtraHeaders = headers });
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Sets the user agent every request reports, and what <c>navigator.platform</c> answers.</summary>
    /// <remarks>
    /// <para>
    /// The header half is effective at once and for every request. <c>acceptLanguage</c> becomes an
    /// <c>Accept-Language</c> header on the same terms.
    /// </para>
    /// <para>
    /// <b><c>platform</c> reaches the document that is already loaded</b>, like the other two.
    /// <c>navigator.platform</c> is an accessor over the page's own <c>EmulationState</c> rather than a value
    /// captured when the engine was built, so it answers the new string on the next read and a client needs no
    /// navigation to see it. This paragraph used to say the opposite
    /// (<see href="https://github.com/sebastienros/jint/issues/3701">#3701</see> item 5), which would have
    /// cost a client a reload it never needed;
    /// <c>NetworkDomainTests.TheNetworkCommandsPlatformReachesTheDocumentAlreadyLoaded</c> is what now holds
    /// the claim to the code. <c>userAgentMetadata</c> becomes <c>navigator.userAgentData</c>, as it does
    /// through the <c>Emulation</c> command; it reaches no request header, because this browser sends no
    /// client hints.
    /// </para>
    /// </remarks>
    protected override ValueTask<EmptyResult> SetUserAgentOverrideAsync(SetUserAgentOverrideRequest parameters, CommandContext context)
    {
        // The page's EmulationState is the one place either command writes; PageNetworkPolicy reads it while
        // composing a request, and navigator reads it in script, so the two can never disagree.
        _target.Emulation.ApplyUserAgentOverride(
            parameters.UserAgent,
            parameters.AcceptLanguage,
            parameters.Platform,
            EmulationDomain.ClientHints(parameters.UserAgentMetadata));

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Pulls the plug: every request fails with <c>net::ERR_INTERNET_DISCONNECTED</c>, and
    /// <c>navigator.onLine</c> answers <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deprecated, and still both halves of the pair that replaced it.</b> Chrome splits what this did
    /// between <see cref="EmulateNetworkConditionsByRuleAsync"/>, the transport, and
    /// <see cref="OverrideNetworkStateAsync"/>, the navigator — and answers this one as a single global rule
    /// plus the navigator override. So it writes both states here, replacing whatever either of the other two
    /// set: a client that went offline through the pair and comes back through this, or the other way round,
    /// leaves nothing set behind it.
    /// </para>
    /// <para>
    /// <b>Only <c>offline</c> is honoured.</b> Latency and throughput would have to be simulated by the
    /// transport, which measures none of it and would be reporting numbers it invented; a client that sets
    /// them and then asserts on a load time would be asserting on a fiction. Offline is different in kind —
    /// it is a refusal, and a refusal is something this browser can make truthfully.
    /// </para>
    /// </remarks>
    protected override ValueTask<EmptyResult> EmulateNetworkConditionsAsync(EmulateNetworkConditionsRequest parameters, CommandContext context)
    {
        _target.UpdateNetworkPolicy(policy => policy with { Offline = parameters.Offline });
        OverrideNavigatorState(parameters.Offline);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Sets the network conditions every request is sent under, of which only offline is honoured.</summary>
    /// <remarks>
    /// <para>
    /// <b>The transport half of what <c>emulateNetworkConditions</c> did, and nothing of the navigator.</b>
    /// Each call replaces the conditions in force — whichever of this and <c>emulateNetworkConditions</c> set
    /// them — rather than adding to them, which is Chrome's rule. So an empty list is how a client goes back
    /// online, and <c>navigator.onLine</c> is left to <see cref="OverrideNetworkStateAsync"/>: PuppeteerSharp
    /// 25.14 sends the two together.
    /// </para>
    /// <para>
    /// <b>Only a global entry, one with an empty <c>urlPattern</c>, is accepted.</b> It matches every request
    /// and the first entry that matches wins, so whether the page is offline is the first entry's
    /// <c>offline</c> — or the deprecated top-level <c>offline</c>, which Chrome lets override every entry's
    /// own and which, with no entries, has nothing to apply to. An entry scoped to a URL is refused with
    /// <c>-32000</c> rather than applied to every request or to none: a request is decided on a transport
    /// thread, and the engine's <c>URLPattern</c> matches with the engine's own regular expressions, which
    /// only the page loop may run. PuppeteerSharp's <c>BlockList</c> and <c>Allowlist</c> connect options are
    /// what send such entries, and it logs the refusal and carries on, as it did with the <c>-32601</c> before
    /// this command was answered. Throttling is accepted and not honoured, for the reason
    /// <see cref="EmulateNetworkConditionsAsync"/> gives, and <c>emulateOfflineServiceWorker</c> has no service
    /// worker to apply to.
    /// </para>
    /// <para>
    /// One identifier is minted per entry, as Chrome mints one. Chrome also names the rule on each request it
    /// affected, in <c>requestWillBeSentExtraInfo</c>; that field is not sent here.
    /// </para>
    /// <para>
    /// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Network/#method-emulateNetworkConditionsByRule"/>.
    /// </para>
    /// </remarks>
    protected override ValueTask<EmulateNetworkConditionsByRuleResponse> EmulateNetworkConditionsByRuleAsync(
        EmulateNetworkConditionsByRuleRequest parameters,
        CommandContext context)
    {
        var entries = parameters.MatchedNetworkConditions;

        foreach (var entry in entries)
        {
            if (entry.UrlPattern is { Length: > 0 })
            {
                return Throw.ServerError<ValueTask<EmulateNetworkConditionsByRuleResponse>>(
                    "Network conditions scoped to a urlPattern are not supported",
                    "this browser applies network conditions to every request or to none; leave urlPattern empty for an entry that applies to every request");
            }
        }

        var offline = entries.Length > 0 && (parameters.Offline ?? entries[0].Offline ?? false);
        _target.UpdateNetworkPolicy(policy => policy with { Offline = offline });

        var ruleIds = new string[entries.Length];
        for (var i = 0; i < ruleIds.Length; i++)
        {
            ruleIds[i] = Guid.NewGuid().ToString("N").ToUpperInvariant();
        }

        return new ValueTask<EmulateNetworkConditionsByRuleResponse>(new EmulateNetworkConditionsByRuleResponse { RuleIds = ruleIds });
    }

    /// <summary>Sets what <c>navigator.onLine</c> answers, and fails no request.</summary>
    /// <remarks>
    /// <para>
    /// <b>The navigator half of what <c>emulateNetworkConditions</c> did, and nothing of the transport.</b>
    /// Chrome's renderer answers the old command with this very one, so the two write one state and the last
    /// of them wins. A request still goes out: a page that believes it is offline and is not is what a client
    /// sending only this asked for, and <see cref="EmulateNetworkConditionsByRuleAsync"/> is what fails one.
    /// </para>
    /// <para>
    /// The state is the page's, so it outlives the document; <c>connectionType</c>, latency and throughput
    /// would reach <c>navigator.connection</c>, which this browser does not have.
    /// </para>
    /// <para>
    /// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Network/#method-overrideNetworkState"/>.
    /// </para>
    /// </remarks>
    protected override ValueTask<EmptyResult> OverrideNetworkStateAsync(OverrideNetworkStateRequest parameters, CommandContext context)
    {
        OverrideNavigatorState(parameters.Offline);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Writes what <c>navigator.onLine</c> answers and, when that changes, fires <c>offline</c> or
    /// <c>online</c> at the window of the document showing.
    /// </summary>
    /// <remarks>
    /// https://html.spec.whatwg.org/multipage/system-state.html#dom-navigator-online — the event is for a
    /// <i>change</i>, so repeating a command fires nothing. Every command runs on the page loop, which is what
    /// lets this dispatch at all, as <c>Emulation.setFocusEmulationEnabled</c> dispatches
    /// <c>visibilitychange</c>.
    /// </remarks>
    private void OverrideNavigatorState(bool offline)
    {
        var emulation = _target.Emulation;
        if (emulation.Offline == offline)
        {
            return;
        }

        emulation.Offline = offline;

        if (PageRuntime.Find(_target.Runtime.Engine) is { } runtime
            && runtime.Engine._webApi?.GlobalEventTarget is { } window)
        {
            Jint.Browser.Runtime.PageEvents.Fire(runtime, window, offline ? "offline" : "online");
        }
    }

    /// <summary>Refuses every request whose URL matches one of the patterns.</summary>
    /// <remarks>
    /// The protocol's own pattern shape — literal text with <c>*</c> for any run of characters — and a match
    /// is <c>net::ERR_BLOCKED_BY_CLIENT</c> with <c>blockedReason: "inspector"</c>, which is what Chrome
    /// sends. An empty list clears the block. <c>urlPatterns</c>, the newer parameter, is read as the
    /// patterns it names.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetBlockedURLsAsync(SetBlockedURLsRequest parameters, CommandContext context)
    {
        var patterns = new List<string>();

        if (parameters.Urls is { } urls)
        {
            patterns.AddRange(urls);
        }

        if (parameters.UrlPatterns is { } declared)
        {
            foreach (var pattern in declared)
            {
                if (pattern.Block)
                {
                    patterns.Add(pattern.UrlPattern);
                }
            }
        }

        _target.UpdateNetworkPolicy(policy => policy with { BlockedUrls = patterns });
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>The body of one response, as text or as base64.</summary>
    /// <remarks>
    /// <b>Only while the domain is enabled, and only within the page's capture budget.</b> The log copies a
    /// body while a client is watching and holds at most
    /// <c>BrowserOptions.MaxCapturedResponseBytes</c> of them; a request it has forgotten, or one whose body
    /// was larger than the whole budget, answers Chrome's own <c>No data found for resource with given
    /// identifier</c> rather than an empty string a client would read as an empty response.
    /// </remarks>
    protected override ValueTask<GetResponseBodyResponse> GetResponseBodyAsync(GetResponseBodyRequest parameters, CommandContext context)
    {
        if (_target.NetworkLog.Body(parameters.RequestId) is { } body)
        {
            return new ValueTask<GetResponseBodyResponse>(Render(body));
        }

        return Throw.ServerError<ValueTask<GetResponseBodyResponse>>(
            "No data found for resource with given identifier",
            _target.NetworkLog.Knows(parameters.RequestId)
                ? "the response body was not kept: enable the Network domain before the request is made, and see BrowserOptions.MaxCapturedResponseBytes"
                : "no request with that identifier has been recorded for this page");
    }

    /// <summary>The request body of one request, when it carried one.</summary>
    protected override ValueTask<GetRequestPostDataResponse> GetRequestPostDataAsync(GetRequestPostDataRequest parameters, CommandContext context)
    {
        if (_target.NetworkLog.PostData(parameters.RequestId) is { } data)
        {
            return new ValueTask<GetRequestPostDataResponse>(new GetRequestPostDataResponse { PostData = data, Base64Encoded = false });
        }

        return Throw.ServerError<ValueTask<GetRequestPostDataResponse>>(
            "No post data available for the request",
            "the request carried no body, or the Network domain was not enabled when it was sent");
    }

    /// <inheritdoc/>
    protected override ValueTask<GetAllCookiesResponse> GetAllCookiesAsync(EmptyParameters parameters, CommandContext context)
        => new(new GetAllCookiesResponse { Cookies = PageCookies.All(Network) });

    /// <inheritdoc/>
    /// <remarks>
    /// With no <c>urls</c> the answer is what the jar would send to the document showing, which is what
    /// Chrome does; a client that wants everything sends <c>getAllCookies</c>.
    /// </remarks>
    protected override ValueTask<GetCookiesResponse> GetCookiesAsync(GetCookiesRequest parameters, CommandContext context)
    {
        var urls = parameters.Urls is { Length: > 0 } named ? named : [_target.Page.Url];
        return new ValueTask<GetCookiesResponse>(new GetCookiesResponse { Cookies = PageCookies.For(Network, urls) });
    }

    /// <inheritdoc/>
    protected override ValueTask<SetCookieResponse> SetCookieAsync(SetCookieRequest parameters, CommandContext context)
    {
        var cookie = new CookieParam
        {
            Name = parameters.Name,
            Value = parameters.Value,
            Url = parameters.Url,
            Domain = parameters.Domain,
            Path = parameters.Path,
            Secure = parameters.Secure,
            HttpOnly = parameters.HttpOnly,
            SameSite = parameters.SameSite,
            Expires = parameters.Expires,
        };

        var stored = PageCookies.Set(Network, cookie, _target.Page.Url);
        return new ValueTask<SetCookieResponse>(new SetCookieResponse { Success = stored });
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> SetCookiesAsync(SetCookiesRequest parameters, CommandContext context)
    {
        foreach (var cookie in parameters.Cookies)
        {
            PageCookies.Set(Network, cookie, _target.Page.Url);
        }

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> DeleteCookiesAsync(DeleteCookiesRequest parameters, CommandContext context)
    {
        PageCookies.Delete(Network, parameters.Name, parameters.Url, parameters.Domain, parameters.Path, _target.Page.Url);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> ClearBrowserCookiesAsync(EmptyParameters parameters, CommandContext context)
    {
        PageCookies.Clear(Network);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// A captured body as the protocol carries it: text where the type says text, base64 otherwise.
    /// </summary>
    /// <remarks>
    /// The rule is the same one <c>Runtime</c> uses to decide a preview: a <c>text/*</c> type, and the
    /// structured types whose syntax is text (<c>+json</c>, <c>+xml</c>, JavaScript), are decoded with the
    /// charset the response declared and answered as a string; everything else is bytes, and a client that
    /// asked for an image gets base64 rather than a string full of replacement characters.
    /// </remarks>
    private static GetResponseBodyResponse Render(CapturedBody body)
    {
        if (!IsText(body.MimeType))
        {
            return new GetResponseBodyResponse { Body = Convert.ToBase64String(body.Bytes.Span), Base64Encoded = true };
        }

        var encoding = Resolve(body.Charset) ?? Encoding.UTF8;
        var text = encoding.GetString(body.Bytes.Span);

        // A byte-order mark is a mark rather than content, and a client that pastes the answer into a parser
        // would otherwise get a stray U+FEFF at the top of every document.
        return new GetResponseBodyResponse
        {
            Body = text.Length != 0 && text[0] == '﻿' ? text[1..] : text,
            Base64Encoded = false,
        };
    }

    private static bool IsText(string mimeType)
    {
        if (mimeType.Length == 0)
        {
            // No Content-Type at all. The page's own sniffing already decided what to do with the bytes; for
            // a client asking to read them, text is the answer that is usable when it is right and legible
            // when it is not.
            return true;
        }

        if (mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (mimeType.EndsWith("+json", StringComparison.OrdinalIgnoreCase) || mimeType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return mimeType switch
        {
            "application/json" or "application/xml" or "application/xhtml+xml" => true,
            "application/javascript" or "application/x-javascript" or "application/ecmascript" => true,
            "image/svg+xml" => true,
            _ => false,
        };
    }

    private static Encoding? Resolve(string label)
    {
        if (label.Length == 0)
        {
            return null;
        }

        try
        {
            return Encoding.GetEncoding(label);
        }
        catch (ArgumentException)
        {
            // An unknown label is ignored and UTF-8 is used, which is what the encoding standard says to do.
            return null;
        }
    }
}
