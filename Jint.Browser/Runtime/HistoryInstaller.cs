using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.StructuredClone;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime;

/// <summary>
/// <c>history</c>: the page's session history, as the object a client-side router is built on.
/// <para>
/// https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-history-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>The history is the page's, not the engine's.</b> A navigation builds a new engine, and the entries
/// have to outlive it — so <see cref="SessionHistory"/> hangs off the page and this object is a view onto
/// it. The state a script attached is a serialization record for the same reason: it is read back on an
/// engine that did not create it.
/// </para>
/// <para>
/// <b>The members are own properties of the one object</b> rather than accessors on a <c>History.prototype</c>,
/// which is what <c>screen</c> does and for the same reason: there is one history per document, nothing
/// inherits from it, and a shaped object is what the inline caches want. The visible consequence is that
/// <c>history.hasOwnProperty('length')</c> answers <see langword="true"/> where a browser answers
/// <see langword="false"/>.
/// </para>
/// <para>
/// <b>Every traversal is asynchronous</b>, which is HTML's own model: <c>back()</c>, <c>forward()</c> and
/// <c>go()</c> queue the traversal and return, and <c>popstate</c> fires on a later turn of the event loop.
/// A script that reads <c>location.href</c> on the line after <c>history.back()</c> sees the old URL, in a
/// browser and here.
/// </para>
/// </remarks>
internal static class HistoryInstaller
{
    private static readonly JsObjectShape _shape = BuildShape();

    /// <summary>The <c>history</c> object for an engine, or <c>null</c> when it has no page.</summary>
    internal static JsValue Create(Engine engine)
    {
        if (PageRuntime.Find(engine) is null)
        {
            return JsValue.Null;
        }

        return _shape.Instantiate(engine, engine._mainRealm.Intrinsics.Object.PrototypeObject);
    }

    private static JsObjectShape BuildShape() => new JsObjectShape.Builder()
        .ToStringTag("History")
        .Accessor("length", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "length").Page.History.Length))
        .Accessor("state", static (t, _) => State(PageRuntime.Of(t, "state")))
        .Accessor("scrollRestoration",
            static (t, _) => JsString.Create(PageRuntime.Of(t, "scrollRestoration").ScrollRestoration),
            static (t, args) =>
            {
                // A WebIDL enumeration with two members; anything else is ignored rather than thrown at,
                // because the conversion happens in the attribute setter and an unknown value is a no-op.
                var value = TypeConverter.ToString(args.At(0));
                if (value is "auto" or "manual")
                {
                    PageRuntime.Of(t, "scrollRestoration").ScrollRestoration = value;
                }

                return JsValue.Undefined;
            })
        .Method("back", static (t, _) => Traverse(PageRuntime.Of(t, "back"), -1))
        .Method("forward", static (t, _) => Traverse(PageRuntime.Of(t, "forward"), 1))
        .Method("go", static (t, args) =>
        {
            var delta = args.At(0).IsUndefined() ? 0 : TypeConverter.ToInt32(args.At(0));
            return Traverse(PageRuntime.Of(t, "go"), delta);
        }, length: 1)
        .Method("pushState", static (t, args) => Update(PageRuntime.Of(t, "pushState"), args, replace: false), length: 3)
        .Method("replaceState", static (t, args) => Update(PageRuntime.Of(t, "replaceState"), args, replace: true), length: 3)
        .Build();

    private static JsValue State(PageRuntime runtime)
    {
        if (runtime.Page.History.Current?.State is not { } record)
        {
            return JsValue.Null;
        }

        // sharedRecord: the same entry can be travelled to any number of times, and each read has to answer
        // a graph of its own rather than adopt the storage the record holds.
        return new StructuredDeserializer(runtime.Engine, runtime.Engine._mainRealm, sharedRecord: true).Deserialize(record);
    }

    private static JsValue Traverse(PageRuntime runtime, int delta)
    {
        runtime.Page.RequestTraversal(delta);
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/nav-history-apis.html#shared-history-push/replace-state-steps.
    /// </summary>
    /// <remarks>
    /// The <c>title</c> argument is read and discarded, which is what every browser does and what the
    /// standard now says to do. The <c>url</c> argument is resolved against the document's URL and must be
    /// same-origin with it, which is the one thing here that can throw: a page must not be able to rewrite
    /// its own address to another site's.
    /// </remarks>
    private static JsValue Update(PageRuntime runtime, JsValue[] arguments, bool replace)
    {
        var page = runtime.Page;
        var current = runtime.DocumentUrl;
        var target = current;

        var urlArgument = arguments.At(2);
        if (!urlArgument.IsUndefined() && !urlArgument.IsNull())
        {
            var text = TypeConverter.ToString(urlArgument);
            var resolved = PageUrl.Parse(text, current);

            if (resolved is null)
            {
                ThrowSecurityError(
                    runtime,
                    replace ? "replaceState" : "pushState",
                    "'" + text + "' cannot be parsed as a URL.");
            }

            var serialized = resolved!.Serialize();
            if (!string.Equals(PageUrl.OriginOf(serialized), PageUrl.OriginOf(current), StringComparison.Ordinal))
            {
                ThrowSecurityError(
                    runtime,
                    replace ? "replaceState" : "pushState",
                    "A history state object with URL '" + serialized + "' cannot be created in a document with origin '"
                    + PageUrl.OriginOf(current) + "'.");
            }

            target = serialized;
        }

        // StructuredSerialize, before anything is recorded: a state that cannot be serialized is a
        // DataCloneError and must leave the history untouched.
        var state = arguments.At(0).IsUndefined() || arguments.At(0).IsNull()
            ? null
            : (SerializationRecord?) new StructuredSerializer(runtime.Engine, runtime.Engine._mainRealm)
                .Serialize(arguments.At(0), transferList: null);

        if (replace)
        {
            page.History.ReplaceState(target, state);
        }
        else
        {
            page.History.PushState(target, state);
        }

        page.CommitSameDocumentUrl(runtime, target);
        return JsValue.Undefined;
    }

    private static void ThrowSecurityError(PageRuntime runtime, string member, string detail)
    {
        var engine = runtime.Engine;
        var exception = engine._mainRealm.Intrinsics.DomException.CreateException(
            DomExceptionNames.Security,
            "Failed to execute '" + member + "' on 'History': " + detail);

        var location = engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(engine, exception, in location);
    }
}
