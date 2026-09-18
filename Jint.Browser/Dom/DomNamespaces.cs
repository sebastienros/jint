using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Dom;

/// <summary>
/// An element's <a href="https://dom.spec.whatwg.org/#concept-element-namespace">namespace</a> — the one it
/// was <b>created with</b>, which is what <a href="https://dom.spec.whatwg.org/#dom-element-namespaceuri">DOM
/// §4.9's <c>namespaceURI</c></a> answers and what every algorithm phrased on "element's namespace" compares.
/// </summary>
/// <remarks>
/// <para>
/// <b>AngleSharp stores that value faithfully and exposes it as <c>IElement.GivenNamespaceUri</c>.</b> Its
/// <c>IElement.NamespaceUri</c> is a different quantity: <c>_namespace ?? this.GetNamespaceUri()</c>, where the
/// second half walks <c>ParentElement.LocateNamespaceFor</c> and answers an <b>ancestor's</b> namespace. DOM
/// has no such computation — an element's namespace is fixed when it is created and nothing, adoption
/// included, ever changes it — so reading <c>NamespaceUri</c> made a <c>createElementNS(null, 'body')</c>
/// appear to move into the XHTML namespace the moment it was appended to an HTML element
/// ([#3949](https://github.com/sebastienros/jint/issues/3949)). The field behind it is
/// <c>private readonly</c> and assigned only in the constructor; nothing mutates, only the read diverges.
/// </para>
/// <para>
/// <b>The stored value is not enough on its own, because AngleSharp's XML parser records none.</b>
/// <c>XmlDocument.CreateElementFrom</c> builds every parsed element with a null namespace and leaves the
/// resolution to that same ancestor walk, so <c>DOMParser.parseFromString('&lt;svg xmlns="…/2000/svg"/&gt;',
/// 'text/xml').documentElement.namespaceURI</c> is computed rather than stored. Taking the stored value alone
/// would answer <see langword="null"/> there and lose a namespace the parse really did resolve.
/// </para>
/// <para>
/// <b>Creation provenance distinguishes an explicit null from an unresolved XML name.</b> Binding factories
/// record the namespace before exposing a node; document observation captures parsed XML declarations before
/// a binding-driven move or declaration edit can change their answer. Clone/import carry that immutable value.
/// Metadata is weak and engine-free, and elements with a native namespace never consult it. A native host
/// that creates and moves a node before any binding observation remains outside this recorded boundary.
/// </para>
/// </remarks>
internal static class DomNamespaces
{
    /// <summary>The <c>xmlns</c> attribute name, which declares the default namespace for a subtree.</summary>
    private const string XmlNsPrefix = "xmlns";

    private sealed class NamespaceValue(string? value)
    {
        internal string? Value { get; } = value;
        internal static readonly NamespaceValue None = new(null);
    }

    private static readonly ConditionalWeakTable<IElement, NamespaceValue> _creationNamespaces = new();

    /// <summary>Records API creation while the requested namespace is still known.</summary>
    internal static IElement Created(IElement element, string? namespaceUri)
    {
        if (string.IsNullOrEmpty(element.GivenNamespaceUri))
        {
            _creationNamespaces.GetValue(element, _ => string.IsNullOrEmpty(namespaceUri)
                ? NamespaceValue.None : new NamespaceValue(namespaceUri));
        }
        return element;
    }

    /// <summary>Captures an unrecorded parsed element before binding-driven mutation or adoption.</summary>
    internal static void Capture(IElement element)
    {
        if (string.IsNullOrEmpty(element.GivenNamespaceUri) && !_creationNamespaces.TryGetValue(element, out _))
        {
            Created(element, Declared(element));
        }
    }

    /// <summary>https://dom.spec.whatwg.org/#concept-node-clone: copy each element's namespace.</summary>
    internal static void Copy(INode source, INode copy)
    {
        var pending = new Stack<(INode Source, INode Copy)>();
        pending.Push((source, copy));
        while (pending.TryPop(out var pair))
        {
            if (pair.Source is IElement original && pair.Copy is IElement cloned
                && string.IsNullOrEmpty(cloned.GivenNamespaceUri))
            {
                Created(cloned, Of(original));
            }
            var sources = pair.Source.ChildNodes;
            var copies = pair.Copy.ChildNodes;
            for (var i = 0; i < Math.Min(sources.Length, copies.Length); i++)
            {
                pending.Push((sources[i], copies[i]));
            }
            if (pair.Source is IHtmlTemplateElement template && pair.Copy is IHtmlTemplateElement templateCopy)
            {
                pending.Push((template.Content, templateCopy.Content));
            }
        }
    }

    /// <summary>
    /// DOM's namespace of <paramref name="element"/>: the namespace it was created with, normalized so that
    /// the empty string is <see langword="null"/> the way
    /// <a href="https://dom.spec.whatwg.org/#validate-and-extract">validate and extract</a> step 1 does.
    /// </summary>
    internal static string? Of(IElement element)
    {
        var given = element.GivenNamespaceUri;

        // The overwhelmingly common case: the HTML parser, the SVG and MathML factories and every
        // createElementNS with a namespace all record one, so this is a field read and a length test.
        return string.IsNullOrEmpty(given)
            ? _creationNamespaces.TryGetValue(element, out var recorded) ? recorded.Value : Declared(element)
            : given;
    }

    /// <summary>
    /// The namespace the <c>xmlns</c> declarations in scope give <paramref name="element"/>, which is what an
    /// XML parse resolves a name against and all that is left of AngleSharp's walk once an ancestor element's
    /// own namespace is taken out of it.
    /// </summary>
    private static string? Declared(IElement element)
    {
        var prefix = element.Prefix;

        for (IElement? current = element; current is not null; current = current.ParentElement)
        {
            foreach (var attribute in current.Attributes)
            {
                // A default declaration is the unprefixed `xmlns`; a prefixed one is `xmlns:p`, which
                // AngleSharp's own parser is the only producer of and which it does put in the XMLNS
                // namespace. Matching the same two shapes keeps a parsed subtree answering as it did.
                var declares = string.IsNullOrEmpty(prefix)
                    ? attribute.Prefix is null && string.Equals(attribute.LocalName, XmlNsPrefix, StringComparison.Ordinal)
                    : string.Equals(attribute.Prefix, XmlNsPrefix, StringComparison.Ordinal)
                      && string.Equals(attribute.LocalName, prefix, StringComparison.Ordinal);

                if (declares)
                {
                    // "Return its value if it is not the empty string, and null otherwise" — an xmlns set to
                    // the empty string undeclares the default namespace rather than declaring an empty one.
                    return string.IsNullOrEmpty(attribute.Value) ? null : attribute.Value;
                }
            }
        }

        return null;
    }
}
