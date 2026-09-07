using AngleSharp.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>
/// <a href="https://w3c.github.io/aria/#ARIAMixin">ARIA §10.1</a>'s element-reflecting half — the eight IDL
/// attributes that reflect an <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#attr-associated-element">attr-associated element</a>
/// rather than a string: <c>ariaActiveDescendantElement</c> and the seven <c>…Elements</c> arrays.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is not reflection in <see cref="AriaReflection"/>'s sense, and that is the whole difficulty.</b> The
/// string half is a view of one content attribute and holds nothing; these hold an <i>explicitly set
/// attr-element</i> per member, because a page may hand the IDL attribute an element that has no <c>id</c> at
/// all — or one whose <c>id</c> is not unique — and the relationship still has to survive. So the content
/// attribute stops being the state and becomes a flag: it says whether there is a relationship, and the
/// element on the other end of it is either what was explicitly set or, when nothing was, whatever the
/// attribute's IDs resolve to <i>now</i>.
/// </para>
/// <para>
/// <b>Three rules carry every one of the standard's cases.</b> A member with no content attribute answers
/// <see langword="null"/>, which is how a page tells "no relationship" from "a relationship to nothing".
/// Setting an element sets the content attribute to the <b>empty string</b> — never to an id, because an id
/// could not name the element in general — and stores the reference. And a stored reference is filtered at
/// <i>get</i> time rather than at set time, so moving either end in or out of a shadow tree, out of the
/// document, or into another document changes what the getter answers without changing what is stored. That
/// is what lets a reference come back when the element does.
/// </para>
/// <para>
/// <b>The state itself is not here.</b> <see cref="AriaElementReferences"/> holds it, because the
/// accessibility tree needs the same relationships and has no engine to reach a realm with; what stays here
/// is the half that is engine-bound by definition — the <c>FrozenArray</c> a getter last answered, which is
/// a JavaScript object and belongs to the engine that made it. The references themselves are weak, as the
/// standard says, so a relationship does not keep its target alive.
/// </para>
/// <para>
/// <b>The array members answer the same array until their value changes</b>, which WebIDL requires of a
/// <c>FrozenArray</c> attribute and the corpus checks per member and per element. The cache holds its
/// elements strongly for as long as it stands; it is dropped the moment the computed list differs, which is
/// the same bargain a browser's FrozenArray cache makes.
/// </para>
/// </remarks>
internal static class AriaElementReflection
{
    /// <summary>How many cache slots an element has — one per member of the mixin's element half.</summary>
    internal static int MemberCount => AriaElementReferences.Reflected.Length;

    /// <summary>Declares one accessor pair per element-reflecting attribute on the <c>Element</c> shape.</summary>
    internal static void Declare(JsObjectShape.Builder builder)
    {
        var reflected = AriaElementReferences.Reflected;

        for (var i = 0; i < reflected.Length; i++)
        {
            var (member, attribute, single) = reflected[i];
            var accessor = new Reference(i, member, attribute, single);

            builder.Accessor(
                member,
                DomFailures.Guard(accessor.Member, accessor.Get),
                DomFailures.Guard(accessor.Member, accessor.Set));
        }
    }

    /// <summary>
    /// The first element of <paramref name="root"/>'s tree whose <c>id</c> is <paramref name="id"/>, in tree
    /// order, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The lookup is in the referring element's <b>root</b> and not in its document, which is what makes an
    /// idref keep working while the subtree holding both ends is out of the document. A document or a
    /// fragment can answer it directly; a detached element is a root with no such member, so the walk is the
    /// fallback rather than the rule.
    /// </remarks>
    private static IElement? ElementById(INode root, string id)
    {
        if (root is INonElementParentNode parent)
        {
            return parent.GetElementById(id);
        }

        if (root is IElement self && string.Equals(self.Id, id, StringComparison.Ordinal))
        {
            return self;
        }

        foreach (var descendant in root.Descendants<IElement>())
        {
            if (string.Equals(descendant.Id, id, StringComparison.Ordinal))
            {
                return descendant;
            }
        }

        return null;
    }

    /// <summary>The frozen array one member last answered, and the elements it was built from.</summary>
    internal sealed class Cache
    {
        private readonly Entry[] _entries = new Entry[MemberCount];

        internal Entry At(int index) => _entries[index] ??= new Entry();

        internal sealed class Entry
        {
            internal ObjectInstance? Array;

            internal IElement[]? From;
        }
    }

    /// <summary>
    /// One element-reflecting attribute's get/set pair. It captures two strings, an index and a flag and
    /// nothing else, so it is engine-independent — the requirement a shape member body carries, because one
    /// shape is instantiated for every engine.
    /// </summary>
    private sealed class Reference(int index, string member, string attribute, bool single)
    {
        private readonly int _index = index;
        private readonly string _attribute = attribute;
        private readonly bool _single = single;

        internal string Member { get; } = "Element." + member;

        internal JsValue Get(JsValue thisObject, JsValue[] arguments)
        {
            var self = DomBindings.Bind<IElement>(thisObject, Member);
            var entry = self.Realm.AriaCacheFor(self.Target).At(_index);
            var computed = Computed(self.Target);

            if (computed is null)
            {
                entry.Array = null;
                entry.From = null;
                return JsValue.Null;
            }

            if (_single)
            {
                return computed.Length == 0 ? JsValue.Null : self.Realm.WrapNode(computed[0]);
            }

            if (entry.Array is { } cached && entry.From is { } from && Same(from, computed))
            {
                return cached;
            }

            var values = new JsValue[computed.Length];
            for (var i = 0; i < computed.Length; i++)
            {
                values[i] = self.Realm.WrapNode(computed[i]);
            }

            var array = self.Realm.PrincipalRealm.Intrinsics.Array.Construct(values);
            array.SetIntegrityLevel(ObjectInstance.IntegrityLevel.Frozen);

            entry.Array = array;
            entry.From = computed;
            return array;
        }

        internal JsValue Set(JsValue thisObject, JsValue[] arguments)
        {
            var self = DomBindings.Bind<IElement>(thisObject, Member);
            var entry = self.Realm.AriaCacheFor(self.Target).At(_index);
            var value = arguments.Length > 0 ? arguments[0] : JsValue.Undefined;

            entry.Array = null;
            entry.From = null;

            if (value.IsNullOrUndefined())
            {
                AriaElementReferences.Set(self.Target, _index, null);
                self.Target.RemoveAttribute(_attribute);
                return JsValue.Undefined;
            }

            // The conversion happens before anything is written, so a value the IDL type refuses leaves both
            // the content attribute and the stored reference exactly as they were.
            var references = _single
                ? [new WeakReference<IElement>(DomBindings.Argument<IElement>(arguments, 0, Member))]
                : Sequence(self.Realm, value);

            AriaElementReferences.Set(self.Target, _index, references);

            // https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#reflecting-content-attributes-in-idl-attributes:
            // the content attribute becomes the empty string whatever was set, because the elements are held
            // by reference and an id could not name them in general. It is written even when nothing set is
            // currently visible, so the relationship exists and the getter can start answering the moment the
            // elements move into scope.
            self.Target.SetAttribute(_attribute, string.Empty);
            return JsValue.Undefined;
        }

        /// <summary>
        /// The computed attr-associated elements: <see langword="null"/> when there is no content attribute,
        /// otherwise the visible half of what was explicitly set, or what the attribute's IDs resolve to.
        /// </summary>
        private IElement[]? Computed(IElement element)
        {
            var slots = AriaElementReferences.SlotsFor(element);

            // The attribute change steps run inside this call, which is why it is the one door and the field
            // is never read directly — AriaElementReferences says what that costs a reader who does.
            if (AriaElementReferences.Explicit(element, slots, _index, _attribute) is { } references)
            {
                return references;
            }

            return element.GetAttribute(_attribute) is { } value ? ById(element, value) : null;
        }

        private IElement[] ById(IElement element, string value)
        {
            var root = DomNodeMembers.Root(element);
            var found = new List<IElement>();

            foreach (var id in value.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (ElementById(root, id) is { } candidate)
                {
                    found.Add(candidate);
                }

                if (_single)
                {
                    break;
                }
            }

            return [.. found];
        }

        /// <summary>
        /// WebIDL's <c>sequence&lt;Element&gt;</c> conversion: anything that is not iterable, and any member
        /// of it that is not an <c>Element</c>, is a <c>TypeError</c> — which is what makes
        /// <c>el.ariaControlsElements = otherElement</c> and <c>= "a string"</c> refusals rather than silent
        /// nonsense.
        /// </summary>
        private WeakReference<IElement>[] Sequence(DomRealm realm, JsValue value)
        {
            var references = new List<WeakReference<IElement>>();
            var iterator = value.GetIterator(realm.PrincipalRealm);

            try
            {
                while (iterator.TryIteratorStepValue(out var item))
                {
                    if (item is not IDomWrapper { DomTarget: IElement element })
                    {
                        Throw.TypeError(
                            realm.PrincipalRealm,
                            "Failed to set the '" + Member + "' property: the provided value is not of type 'Element'.");
                        return [];
                    }

                    references.Add(new WeakReference<IElement>(element));
                }
            }
            catch
            {
                iterator.CloseIfNotDone(CompletionType.Throw);
                throw;
            }

            return [.. references];
        }

        private static bool Same(IElement[] left, IElement[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (!ReferenceEquals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
