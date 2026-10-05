using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.Fetch;
using Jint.WebApi.Streams;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Cookies;

/// <summary>https://cookiestore.spec.whatwg.org/#cookiestore - one non-constructible EventTarget per window.</summary>
internal sealed class JsCookieStore : JsEventTarget
{
    private readonly DomRealm _dom;
    private readonly PageRuntime? _runtime;
    private Dictionary<CookieKey, SetCookie>? _snapshot;

    internal JsCookieStore(CookieRealm owner) : base(owner.Dom.Engine, owner.Dom.OwningRealm)
    {
        _dom = owner.Dom;
        _runtime = PageRuntime.Find(_engine);
        _prototype = owner.Prototype;
    }

    /// <summary>https://cookiestore.spec.whatwg.org/#CookieStore-get and the other operation algorithms.</summary>
    internal JsValue Invoke(string operation, JsValue[] args)
    {
        using var scope = new RealmScope(_engine, _realm);
        if (operation is "get" or "getAll")
        {
            var argument = args.At(0);
            string? name;
            string? url = null;
            if (argument is ObjectInstance || argument.IsNullOrUndefined())
            {
                var dictionary = CookieValues.Dictionary(_realm, argument);
                name = CookieValues.Text(dictionary, "name");
                url = CookieValues.Text(dictionary, "url");
            }
            else
            {
                name = UrlValues.ToUsvString(argument);
            }
            var uri = RequestUri();
            if (operation == "get" && name is null && url is null) Throw.TypeError(_realm, "get requires a name or URL.");
            if (url is not null)
            {
                var parsed = UrlParser.Parse(url, UrlParser.Parse(_dom.Document is { } document
                    ? DomDocumentState.BaseUri(document) : _dom.CreationUrl));
                var creation = UrlParser.Parse(_dom.CreationUrl)!;
                if (parsed is null || parsed.Serialize(excludeFragment: true) != creation.Serialize(excludeFragment: true)
                    || parsed.SerializeOrigin() != creation.SerializeOrigin())
                {
                    Throw.TypeError(_realm, "Cookie URL must equal the document's creation URL.");
                }
            }
            var cookies = ScriptCookies.Read(_runtime!.Network.CookieJar, uri);
            var values = new List<JsValue>();
            var normalized = name is null ? null : CookieValues.Normalize(name);
            foreach (var cookie in cookies)
            {
                if (normalized is not null && normalized != cookie.Name) continue;
                values.Add(CookieValues.Item(_engine, _realm, cookie.Name, cookie.Value));
                if (operation == "get") break;
            }
            return ResolveLater(operation == "get"
                ? values.Count == 0 ? Null : values[0]
                : _realm.Intrinsics.Array.ConstructFast(values.ToArray()));
        }

        if (args.Length == 0) Throw.TypeError(_realm, operation + " requires an argument.");
        CookieOptions options;
        if (operation == "set" && args.Length >= 2)
        {
            options = new CookieOptions(UrlValues.ToUsvString(args[0]), UrlValues.ToUsvString(args[1]));
        }
        else if (operation == "delete" && args[0] is not ObjectInstance && !args[0].IsNullOrUndefined())
        {
            var name = UrlValues.ToUsvString(args[0]);
            options = new CookieOptions(name, CookieValues.Normalize(name).Length == 0 ? "deleted" : "", MaxAge: 0, Partitioned: true);
        }
        else
        {
            options = CookieValues.Options(_realm, CookieValues.Dictionary(_realm, args[0]), operation == "delete");
        }

        var requestUri = RequestUri();
        // https://cookiestore.spec.whatwg.org/#restrict and #secure-cookies: writes require secure access.
        // The host exposes the API more broadly, so refuse an insecure write rather than report success.
        if (requestUri.Scheme != "https" && !requestUri.IsLoopback)
        {
            throw new JavaScriptException(_realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.Security, "Cookie Store writes require HTTPS or HTTP loopback."));
        }
        SetCookie parsedCookie;
        try
        {
            parsedCookie = CookieValues.Validate(_realm, requestUri, options);
        }
        catch (JavaScriptException exception)
        {
            // Unlike WebIDL conversion and origin/URL checks, these are the algorithm's parallel steps.
            return SettleLater(exception.Error, reject: true);
        }
        var jar = _runtime!.Network.CookieJar;
        ScriptCookies.Write(jar, requestUri, parsedCookie,
            jar is CookieContainerCookieJar ? "" : ScriptCookies.Serialize(parsedCookie, options.SameSite, options.Partitioned));
        return ResolveLater(Undefined);
    }

    private Uri RequestUri()
    {
        if (_runtime is null || _dom.Document is not { } document || DomDocumentState.Of(document).Origin.IsOpaque
            || ScriptCookies.HttpUri(_dom.CreationUrl) is not { } uri)
        {
            throw new JavaScriptException(_realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.Security, "This document cannot access cookies."));
        }
        return uri;
    }

    private JsValue ResolveLater(JsValue value)
        => SettleLater(value, reject: false);

    private JsValue SettleLater(JsValue value, bool reject)
    {
        var promise = StreamPromises.NewPromise(_engine, _realm);
        _engine.Tasks.Post(() =>
        {
            if (reject) promise.Reject(value);
            else promise.Resolve(value);
        });
        return promise.PromiseInstance;
    }

    internal override void ListenerChanged(string type)
    {
        if (type != "change" || _runtime is null) return;
        if (HasListenerOfType("change"))
        {
            if (_snapshot is null)
            {
                _snapshot = Snapshot();
                _runtime.ObserveCookies(this, observe: true);
            }
        }
        else if (_snapshot is not null)
        {
            _snapshot = null;
            _runtime.ObserveCookies(this, observe: false);
        }
    }

    private Dictionary<CookieKey, SetCookie> Snapshot()
    {
        var snapshot = new Dictionary<CookieKey, SetCookie>();
        if (_runtime is not null && _dom.Document is { } document && !DomDocumentState.Of(document).Origin.IsOpaque
            && ScriptCookies.HttpUri(_dom.CreationUrl) is { } uri)
        {
            foreach (var cookie in ScriptCookies.Read(_runtime.Network.CookieJar, uri))
            {
                snapshot[new CookieKey(cookie.Name, cookie.Domain, cookie.Path)] = cookie;
            }
        }
        return snapshot;
    }

    /// <summary>
    /// https://cookiestore.spec.whatwg.org/#process-cookie-changes - snapshot only while observed,
    /// coalesce each processing interval, and dispatch as a DOM-manipulation task.
    /// </summary>
    internal void ProcessChanges()
    {
        if (_snapshot is not { } previous) return;
        var current = Snapshot();
        _snapshot = current;
        var changed = new List<SetCookie>();
        var deleted = new List<SetCookie>();
        foreach (var (key, cookie) in current)
        {
            if (!previous.TryGetValue(key, out var old) || cookie.Value != old.Value
                || cookie.Expires != old.Expires || cookie.Secure != old.Secure)
            {
                changed.Add(cookie);
            }
        }
        foreach (var (key, cookie) in previous)
        {
            if (!current.ContainsKey(key)) deleted.Add(cookie);
        }
        if (changed.Count == 0 && deleted.Count == 0) return;
        _engine.Tasks.Post(() =>
        {
            try
            {
                try
                {
                    DispatchEvent(JsCookieChangeEvent.CreateTrusted(_dom, changed, deleted));
                }
                finally
                {
                    _engine.CleanUpAfterRunningScript();
                }
            }
            catch (JavaScriptException exception)
            {
                _runtime!.Recorder.Add(PageErrorKind.UncaughtCallbackError,
                    PageRecorder.Diagnostics.Describe(exception.Error, exception), "CookieStore");
            }
        });
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct CookieKey(string Name, string? Domain, string? Path);
}
