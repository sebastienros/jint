# Agent instructions: fetch, cookies and the observer

> **Read this when:** You are touching `Jint/WebApi/Fetch/` — `fetch`, `Request`/`Response`/`Headers`, the transport, redirects, cookies, or `FetchObserver`.
>
> This is one of the co-located instruction files indexed from the repository-root [`AGENTS.md`](../../../AGENTS.md). Read that first, then [`Jint/WebApi/AGENTS.md`](../AGENTS.md) for the four subtree conventions every web API follows. Nothing below is repeated in either.

### Fetch as a browsing position

**Five settings under `Options.WebApi.Fetch` turn `fetch` from "an HTTP client a script can call" into "a
document's fetch", and every one of them is off by default**, so an engine whose host names none behaves
byte-for-byte as it did before they existed: `BaseUrl` (the API base URL a relative input resolves against),
`Referrer` + `ReferrerPolicy`, `Origin`, `CookieJar`, and `Observer`. They exist for `Jint.Browser`, and they
are ordinary settings any embedder can use. Three things about them are decisions rather than
implementations.

**`UserAgent` is a sixth setting and is deliberately not one of the five**: it is on by default, because the
standard's *default `User-Agent` value* is what a user agent appends when the request named none
([#3720](https://github.com/sebastienros/jint/issues/3720)), and an engine that answered `Jint/<version>` to
`navigator.userAgent` while sending nothing said two different things. Every lane reads it — `fetch`, XHR,
`EventSource`, a `WebSocket` handshake — so it lives on `FetchPolicy` rather than being appended per lane,
and a host driving its own pipeline through `FetchTransport` (`Jint.Browser`'s document and subresource
fetches) puts its own there.

**The referrer, the `Origin` header and the `Cookie` header are recomputed per redirect hop, not per fetch.**
That is [main fetch](https://fetch.spec.whatwg.org/#concept-main-fetch) step 6 being re-run because a
redirect re-enters main fetch, and it is what makes a chain that leaves the referrer's origin narrow the
header from that hop on: the value a hop computed becomes the next hop's *source*, so a policy that has
already reduced a URL to its origin never widens it again. `FetchTransport.Append` is the one place all three
are decided, and it appends each **only when the script did not set that header itself** — a divergence from
the standard, which appends unconditionally because its forbidden-request-header list has already stopped a
script setting them, and Jint deliberately does not enforce that list (see `HeadersGuard`). The one thing
that is *not* recomputed is a `Continue` interception's rewrites: they apply to the hop that was answered,
because the observer is asked again for the next one.

**`CookieContainerCookieJar` parses `Set-Cookie` itself and hands `System.Net.CookieContainer` a finished
`Cookie`.** `CookieContainer.SetCookies(Uri, string)` takes one comma-joined header and has to guess where
one value ends, which an `Expires=Wed, 09 Jun 2021 10:18:14 GMT` breaks. What the container gets right and is
left alone: domain matching with or without the leading dot, host-only cookies not matching subdomains, the
default-path derivation, `Secure` filtering, `HttpOnly` still being *sent* (that attribute is about
`document.cookie`, never the wire), longest-path-first ordering, deletion by a past `Expires`, and
port-insensitivity. Four things are patched or accepted, for different reasons. **`Domain` and `Path` are
assigned only when the header carried them**: assigning either at all — `string.Empty` included — clears the
container's "implicit" flag and it then refuses the cookie outright, which silently dropped every one.
**`__Secure-` and `__Host-` are enforced in the parser**, since the container knows nothing about them.
**A value its own grammar refuses is dropped** with the `CookieException` swallowed, because 6265bis's answer
to a `Domain` the host does not match is "ignore the cookie"; the one real loss is a value containing a
comma, which RFC 6265's `cookie-octet` grammar excludes anyway. And **`Version` is never set**, because a
non-zero one emits an RFC 2965 header no modern server reads. The caps (300 cookies, 20 per domain, 4096
bytes) are kept as a bound, and the missing public suffix list is left alone: the container is *stricter*
than 6265bis there, refusing a `Domain=com` the RFC's own domain-match would accept. Same-site is decided
nowhere and cannot be — no top-level site, no PSL — so the jar takes a `Uri` and a host that knows its own
browsing context enforces `SameSite` inside its own.

**`FetchObserver` is a preview surface, and its whole point is that it is engine-free.** Every callback may
run on a transport thread — the same rule `Options.WebApi.Fetch.UrlFilter` already carries, and for the same
reason. Nothing it is handed is a `JsValue`, an `Engine` or a realm, and
`WebApiFetchDocumentTests.TheObserverSurfaceMentionsNoEngineType` walks the whole surface to keep it so.
Terminality is enforced in `FetchObservation` rather than trusted to the call sites, because a request can
fail in the redirect loop, in the body stream *and* in `FetchOperation`'s own classification; the
compare-and-swap is what makes `OnCompleted`/`OnFailed` fire exactly once between them. A notification that
throws is ignored — there is no engine thread to report it to — while a throw from `OnRequestAsync` or
`OnResponseAsync` fails the fetch, because those are the callbacks that were asked to decide.

**Three callbacks answer, and where each is asked is the decision.** `OnRequestAsync` is asked per hop,
inside the redirect loop. `OnAuthRequiredAsync` is asked beside it, in the loop's core, because a `401` on a
document fetch, a subresource and an `XMLHttpRequest` is the case it is for. `OnResponseAsync` is asked once,
for the response that *ends* the chain, in `SendForStreamAsync` — deliberately not in `SendAsync`, because a
document fetch, a subresource, an `XMLHttpRequest` and an `EventSource` all take the stream lane and only
`fetch()` takes the other, so an ask placed there would have served the one caller that needed it least. **The ask is not the notification**: who
tells the observer about the final response is still per lane (`BeginResponseAsync` for `SendAsync`,
`FetchObservation.FinalResponse` for everyone else), so both report what the answer produced rather than what
the socket said. What `Continue` may not rewrite is the body — it has not been read and is still coming — and
that is the whole difference from `Fulfill`, which discards the socket's bytes unread. Two ordering facts matter before mapping it
onto a protocol: **a refusal before the transport** (a `UrlFilter` denial, the concurrency cap, an
already-aborted signal) reports `OnFailed` with no `OnRequest` before it, because `fetch`'s synchronous half
cannot await one; and **a body nobody reads never completes**, because it is only pulled when script consumes
it. **An authentication challenge is asked about and only `Basic` can be answered, and the second half is a
*stated* behaviour rather than a shortfall.** A `401` carrying a `WWW-Authenticate` reaches
`OnAuthRequiredAsync` whatever scheme it names, because being asked is the only way an observer tells "this
engine does not do `Digest`" from "the server never challenged" — and `ObservedFetchAuthChallenge`
`.CanProvideCredentials` answers which it is *before* the observer decides. `Basic` is honoured with one
`Authorization` header built in `FetchTransport.AuthorizeAsync`; `Digest` needs a nonce exchange and
`Negotiate` and `NTLM` a handshake bound to the connection, none of which a transport that hands the socket
back after every response can hold. Credentials offered for one of those are **refused, not dropped**: a host
driving this through a protocol answers `Fetch.continueWithAuth` and gets an error naming the scheme, because
an ask that cannot be honoured must fail visibly — an ask that silently does nothing is the defect this seam
removes. **Exactly one retry per request** (`authRetried`), since an observer has one credential to offer and
a loop is worse than a `401`; the retry is not a redirect and spends none of `MaxRedirects`. A `407` is not
reported at all — the proxy is the host's `HttpClient`'s — so `Source` is always `Server`. The header the
retry carries is stripped by `_crossOriginHeaderNames` if a later redirect leaves the origin, which is
[HTTP-redirect fetch](https://fetch.spec.whatwg.org/#http-redirect-fetch) step 13 and needed no new code
([#3828](https://github.com/sebastienros/jint/issues/3828)).

**`EventSource` is observed whole, and a `WebSocket` has an observer of its own; the line between them is
whether *this* observer can be told everything.** A stream reaches `FetchTransport`, so the request and every
redirect hop cost nothing to report — but it reads its own body, which is why it was left unobserved until
`EventSourceConnection` paid the same two debts a document fetch pays: `FinalResponse`, which every caller of
`SendForStreamAsync` owes, and `Data` per chunk from inside the read loop. Each connection is its own
observation, so a reconnect is a second request with its own id, and the terminal call is `OnCompleted` for a
stream the server ended and `OnFailed` for every other ending, an abandoned one included. A socket stays out
of *this* observer and takes `Options.WebApi.Fetch.WebSocketObserver` instead: its handshake never reaches
this transport (`ClientWebSocket` makes its own HTTP request, and the headers that matter —
`Sec-WebSocket-Key`, the negotiated extensions — are added inside it and cannot be enumerated), an
interception's `Fulfill` could not be honoured at all, and the frames afterwards are not a body. So that
observer watches and never answers, and carries a `WebSocketId` rather than a `FetchRequestId` — **a socket
must not be an outstanding request**, or a page with one open is a page whose network never goes quiet. What
it can honestly report is four moments: created, the handshake request with the headers *this engine* set,
the handshake response (`ClientWebSocket.CollectHttpResponseDetails`, turned on only when somebody is
watching), and one terminal close
([#3621](https://github.com/sebastienros/jint/issues/3621),
[#3701](https://github.com/sebastienros/jint/issues/3701) item 2).
