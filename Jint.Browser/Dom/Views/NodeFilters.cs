using AngleSharp.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// Turns a script's <c>NodeFilter</c> into the delegate AngleSharp's traversal takes.
/// </summary>
/// <remarks>
/// <para>
/// <a href="https://dom.spec.whatwg.org/#interface-nodefilter">DOM §6.3</a> declares <c>NodeFilter</c> as a
/// <em>legacy callback interface</em>, which means two accepted shapes: a bare function, and an object whose
/// <c>acceptNode</c> property is one. Both are common — a function in modern code, the object form in code
/// written against the original API — so both are handled here, and the object's <c>acceptNode</c> is read on
/// every call rather than captured, because WebIDL invokes a callback-interface operation by looking the
/// property up each time.
/// </para>
/// <para>
/// The generator could not project <c>createTreeWalker</c> and <c>createNodeIterator</c> at all: their filter
/// parameter is a CLR delegate, which the conversion table has no entry for and deliberately no general one —
/// a delegate parameter is a callback whose IDL shape only the standard knows. That is why they arrive
/// through <c>overrides.json</c>'s additions with a hand-written body instead.
/// </para>
/// <para>
/// A filter that throws propagates out of <c>nextNode()</c>, which is what the standard requires, and the
/// exception unwinds through the traversal on its way — safe because a traversal holds no state it has to
/// unwind and only an accepted node moves the walker, so it is left where the filter found it.
/// <see cref="DomTreeWalker"/> is that traversal for a <c>TreeWalker</c>; a <c>NodeIterator</c>'s is still
/// AngleSharp's.
/// </para>
/// </remarks>
internal static class NodeFilters
{
    /// <summary>https://dom.spec.whatwg.org/#dom-nodefilter-filter_accept.</summary>
    internal const int Accept = 1;

    /// <summary>https://dom.spec.whatwg.org/#dom-nodefilter-filter_reject.</summary>
    internal const int Reject = 2;

    /// <summary>https://dom.spec.whatwg.org/#dom-nodefilter-filter_skip.</summary>
    internal const int Skip = 3;

    /// <summary>
    /// The filter argument, or <see langword="null"/> when it was absent — which the traversal reads as
    /// "accept everything <c>whatToShow</c> let through".
    /// </summary>
    internal static NodeFilter? From(DomRealm realm, JsValue value, string member)
    {
        if (value.IsNullOrUndefined())
        {
            return null;
        }

        if (value is ICallable or ObjectInstance)
        {
            return node => Invoke(realm, value, node);
        }

        Throw.TypeError(
            realm.PrincipalRealm,
            "Failed to execute '" + member + "' on 'Document': parameter 3 is not of type 'NodeFilter'.");
        return null;
    }

    private static FilterResult Invoke(DomRealm realm, JsValue filter, INode node)
    {
        var callable = filter is ICallable ? filter : (filter as ObjectInstance)?.Get("acceptNode");

        if (callable is not ICallable)
        {
            // WebIDL: an object with no callable operation is a TypeError at call time, not at conversion
            // time, which is exactly when a page notices it passed the wrong thing.
            Throw.TypeError(realm.PrincipalRealm, "Failed to execute 'acceptNode' on 'NodeFilter': the filter is neither a function nor an object with an acceptNode method.");
        }

        var answer = realm.Engine.Call(callable!, filter, [realm.WrapNode(node)]);

        return TypeConverter.ToUint32(answer) switch
        {
            Reject => FilterResult.Reject,
            Skip => FilterResult.Skip,
            Accept => FilterResult.Accept,

            // https://dom.spec.whatwg.org/#concept-node-filter step 5: anything but FILTER_ACCEPT and
            // FILTER_REJECT is FILTER_SKIP, so an unknown number does not accept by accident.
            _ => FilterResult.Skip,
        };
    }
}
