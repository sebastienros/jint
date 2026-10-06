using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.Object;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>
/// A script-visible StaticRange with immutable native boundary points.
/// </summary>
/// <remarks>
/// Unlike a live Range, its boundary points do not move when the tree changes. The binding stores its
/// state separately and uses the declared WebIDL shape for brand checks and property access.
/// </remarks>
internal static class DomStaticRange
{
    /// <summary>
    /// The four values, which are the whole of a static range. A record so the wrapper has one immutable
    /// thing to read and nothing to keep in step with the tree.
    /// </summary>
    internal sealed record State(Node StartContainer, uint StartOffset, Node EndContainer, uint EndOffset)
    {
        /// <summary>
        /// https://dom.spec.whatwg.org/#dom-abstractrange-collapsed — derived rather than stored, because it
        /// is only ever a question about the two endpoints a page set.
        /// </summary>
        internal bool Collapsed
            => ReferenceEquals(StartContainer, EndContainer) && StartOffset == EndOffset;
    }

    /// <summary>
    /// The one required argument, which is what <c>StaticRange.length</c> reports. Every other constructible
    /// interface in the binding takes only optional arguments and so reports zero.
    /// </summary>
    internal const int ConstructorLength = 1;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-staticrange-staticrange. WebIDL converts the dictionary first, so a
    /// missing or non-<c>Node</c> member is a <c>TypeError</c> raised before step 1 runs; step 1's
    /// <c>InvalidNodeTypeError</c> is then the only refusal the constructor makes of its own.
    /// </summary>
    internal static ObjectInstance Construct(DomRealm realm, JsValue[] arguments)
    {
        var init = arguments.Length > 0 ? arguments[0] : JsValue.Undefined;

        // https://webidl.spec.whatwg.org/#es-dictionary: null and undefined convert to an empty dictionary,
        // whose required members are then missing, and anything else that is not an object is refused
        // outright. Both land on the same TypeError here, because every member of StaticRangeInit is
        // required and so an empty dictionary can never be accepted.
        var members = init as ObjectInstance;

        var startContainer = RequiredNode(realm, members, "startContainer");
        var startOffset = RequiredOffset(realm, members, "startOffset");
        var endContainer = RequiredNode(realm, members, "endContainer");
        var endOffset = RequiredOffset(realm, members, "endOffset");

        // Step 1. A DocumentType has no position inside the tree a range names, and an Attr is not in the
        // node tree at all, so neither can be a boundary point.
        RefuseBoundary(realm, startContainer, "startContainer");
        RefuseBoundary(realm, endContainer, "endContainer");

        return new DomObject(
            realm,
            DomManualInterfaces.StaticRange,
            new State((Node) startContainer, startOffset, (Node) endContainer, endOffset));
    }

    /// <summary>
    /// <c>StaticRange.prototype</c>: the five <c>AbstractRange</c> attributes as readonly accessors, which
    /// is where WebIDL puts them, and nothing else — <c>StaticRange</c> declares no member of its own.
    /// </summary>
    internal static JsObjectShape Shape()
        => new JsObjectShape.Builder()
            .ToStringTag("StaticRange")
            .PerRealmSlot("constructor", enumerable: false)
            .Accessor("startContainer", static (thisObject, _) =>
            {
                var (realm, state) = Self(thisObject, "startContainer");
                return realm.WrapNode(state.StartContainer);
            })
            .Accessor("startOffset", static (thisObject, _) => DomConvert.Number(Self(thisObject, "startOffset").State.StartOffset))
            .Accessor("endContainer", static (thisObject, _) =>
            {
                var (realm, state) = Self(thisObject, "endContainer");
                return realm.WrapNode(state.EndContainer);
            })
            .Accessor("endOffset", static (thisObject, _) => DomConvert.Number(Self(thisObject, "endOffset").State.EndOffset))
            .Accessor("collapsed", static (thisObject, _) => DomConvert.Bool(Self(thisObject, "collapsed").State.Collapsed))
            .Build();

    /// <summary>
    /// The receiver's realm and state, or WebIDL's <c>TypeError</c> — the brand check every attribute of a
    /// platform object makes before it reads anything.
    /// </summary>
    private static (DomRealm Realm, State State) Self(JsValue thisObject, string member)
    {
        if (thisObject is DomObject { DomTarget: State state } wrapper)
        {
            return (wrapper.DomRealm, state);
        }

        var qualified = "StaticRange." + member;

        if (thisObject is ObjectInstance instance)
        {
            Jint.Runtime.Throw.TypeError(instance.Engine.Realm, "Failed to execute '" + qualified + "': Illegal invocation");
        }

        Jint.Runtime.Throw.TypeErrorNoEngine("Failed to execute '" + qualified + "': Illegal invocation");
        return default;
    }

    /// <summary>
    /// A <c>required Node</c> member. WebIDL refuses an absent one and a value that is not a <c>Node</c>
    /// alike, and <c>null</c> with them, because the declared type is not nullable.
    /// </summary>
    private static object RequiredNode(DomRealm realm, ObjectInstance? members, string member)
    {
        if (members?.Get(member) is IDomWrapper { DomTarget: Node or Attr } wrapper)
        {
            return wrapper.DomTarget;
        }

        Jint.Runtime.Throw.TypeError(
            realm.OwningRealm,
            "Failed to construct 'StaticRange': member " + member + " is not of type Node.");

        return null!;
    }

    /// <summary>
    /// A <c>required unsigned long</c> member. The conversion is WebIDL's, so a negative number wraps rather
    /// than being refused — a static range is never held to a container's length, and the only thing that
    /// can fail here is the member being absent.
    /// </summary>
    private static uint RequiredOffset(DomRealm realm, ObjectInstance? members, string member)
    {
        var value = members?.Get(member) ?? JsValue.Undefined;

        if (value.IsUndefined())
        {
            Jint.Runtime.Throw.TypeError(
                realm.OwningRealm,
                "Failed to construct 'StaticRange': member " + member + " is required.");
        }

        return Jint.Runtime.TypeConverter.ToUint32(value);
    }

    /// <summary>Step 1's refusal, which is the only one this constructor makes.</summary>
    private static void RefuseBoundary(DomRealm realm, object node, string member)
    {
        if (node is not (DocumentType or Attr))
        {
            return;
        }

        DomFailures.Refuse(
            realm,
            "StaticRange",
            DomExceptionNames.InvalidNodeType,
            "member " + member + " is " + (node is Attr ? "an Attr" : "a DocumentType") + ", which cannot be a boundary point.");
    }
}
