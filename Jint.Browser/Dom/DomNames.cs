using AngleSharp.Dom;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#namespaces">DOM §1.4's name validation</a>: the three predicates a
/// name is held to, and <a href="https://dom.spec.whatwg.org/#validate-and-extract">validate and extract</a>,
/// which is the algorithm every namespaced creation runs before it does anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is ours rather than AngleSharp's.</b> The algorithm chooses between two <c>DOMException</c>
/// names and they are not interchangeable, because <c>e.name</c> is a page's whole vocabulary for a refused
/// DOM operation: a name whose code points are not allowed is an <c>InvalidCharacterError</c>, while a
/// <i>prefixed</i> name with a null namespace, an <c>xml</c> prefix outside the XML namespace or an
/// <c>xmlns</c> name outside the XMLNS namespace is a <c>NamespaceError</c>. AngleSharp's
/// <c>Document.GetPrefixAndLocalName</c> makes some of those refusals, makes them under the other name, and
/// makes none at all for a prefixed name with no namespace — so an element or attribute is created carrying a
/// prefix nothing declares. <c>AGENTS.md</c>'s divergence table has the rows.
/// </para>
/// <para>
/// <b>Where it runs.</b> In <see cref="DomFailures.Guard"/>, which already wraps every emitted member body
/// and already knows the qualified member name — so the eight members that validate a name are a table here
/// rather than eight hand-written bodies, no generated file moves, and a member the table does not name pays
/// nothing. It runs <i>before</i> the body, which is where WebIDL puts it, and only for a receiver the
/// member's own brand check will accept, so an illegal invocation is still a <c>TypeError</c> and a missing
/// argument is still the body's <c>TypeError</c> rather than a refusal about a name.
/// </para>
/// <para>
/// <b>These are not the XML productions any more.</b> DOM used to validate against XML's <c>Name</c> and
/// <c>QName</c>; it was loosened deliberately, because there were names the HTML parser could build and the
/// DOM API could not. What is left is the four predicates below, and the register of what each forbids is the
/// point: <c>=</c> is refused in an attribute local name and allowed in an element one, and a code point at
/// or above U+0080 is allowed everywhere.
/// </para>
/// </remarks>
internal static class DomNames
{
    /// <summary>https://infra.spec.whatwg.org/#xml-namespace.</summary>
    internal const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

    /// <summary>https://infra.spec.whatwg.org/#xmlns-namespace.</summary>
    internal const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

    /// <summary>
    /// Which local-name predicate a member's name is held to — the one difference between the two contexts
    /// <a href="https://dom.spec.whatwg.org/#validate-and-extract">validate and extract</a> takes.
    /// </summary>
    internal enum NameContext
    {
        /// <summary>https://dom.spec.whatwg.org/#valid-element-local-name.</summary>
        Element,

        /// <summary>https://dom.spec.whatwg.org/#valid-attribute-local-name.</summary>
        Attribute,
    }

    /// <summary>
    /// The members that validate a name, and where in their argument list the name is. The receiver kind is
    /// part of the entry so that a call with the wrong receiver is refused by the member's own brand check
    /// rather than by this.
    /// </summary>
    /// <remarks>
    /// <c>getAttributeNS</c>, <c>hasAttributeNS</c> and <c>removeAttributeNS</c> are deliberately absent:
    /// DOM validates nothing for a member that only reads or removes, and adding a refusal there would
    /// break the very feature detection a page writes.
    /// </remarks>
    private static readonly Dictionary<string, Validation> _validations = new(StringComparer.Ordinal)
    {
        ["Document.createElement"] = new(Receiver.Document, NamespaceIndex: -1, NameIndex: 0, Arity: 1, NameContext.Element),
        ["Document.createElementNS"] = new(Receiver.Document, NamespaceIndex: 0, NameIndex: 1, Arity: 2, NameContext.Element),
        ["Document.createAttribute"] = new(Receiver.Document, NamespaceIndex: -1, NameIndex: 0, Arity: 1, NameContext.Attribute),
        ["Document.createAttributeNS"] = new(Receiver.Document, NamespaceIndex: 0, NameIndex: 1, Arity: 2, NameContext.Attribute),
        ["Element.setAttribute"] = new(Receiver.Element, NamespaceIndex: -1, NameIndex: 0, Arity: 2, NameContext.Attribute),
        ["Element.setAttributeNS"] = new(Receiver.Element, NamespaceIndex: 0, NameIndex: 1, Arity: 3, NameContext.Attribute),
    };

    /// <summary>
    /// The validation <paramref name="member"/> runs before its body, or <see langword="null"/> when it runs
    /// none — which is every member but the six above, and is why the wrapper this feeds is chosen once when
    /// the shape is built rather than tested on every call.
    /// </summary>
    internal static Validation? ValidationOf(string member)
        => _validations.TryGetValue(member, out var validation) ? validation : null;

    /// <summary>
    /// https://dom.spec.whatwg.org/#valid-namespace-prefix — at least one code point, and none of ASCII
    /// whitespace, U+0000, U+002F (/) or U+003E (&gt;).
    /// </summary>
    internal static bool IsValidNamespacePrefix(string value)
        => value.Length > 0 && !ContainsAny(value, equals: false);

    /// <summary>
    /// https://dom.spec.whatwg.org/#valid-attribute-local-name — the prefix rule plus U+003D (=), which an
    /// attribute name may not carry because serializing it would produce a second attribute.
    /// </summary>
    internal static bool IsValidAttributeLocalName(string value)
        => value.Length > 0 && !ContainsAny(value, equals: true);

    /// <summary>
    /// https://dom.spec.whatwg.org/#valid-element-local-name — everything the HTML parser can build, which is
    /// the whole intent: a name starting with an ASCII alpha may carry anything but whitespace, U+0000,
    /// <c>/</c> and <c>&gt;</c>, and a name starting with anything else is held to the narrower set the
    /// standard's own regular expression spells.
    /// </summary>
    internal static bool IsValidElementLocalName(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        // The branch the standard's note is about: a name the HTML parser could have produced. Only the four
        // code points that would end the tag name are refused.
        if (char.IsAsciiLetter(value[0]))
        {
            return !ContainsAny(value, equals: false);
        }

        if (value[0] is not (':' or '_') && value[0] < 0x80)
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];

            if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '.' or ':' or '_') && c < 0x80)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#validate-and-extract, in the one place a refusal is the whole answer: the
    /// prefix and local name it extracts are AngleSharp's to compute again, and every step that throws is
    /// here.
    /// </summary>
    /// <remarks>
    /// The steps are in the standard's order, and the order is what decides which of the two names a page
    /// sees: <c>createElementNS(null, 'a:0')</c> is an <c>InvalidCharacterError</c> about the local name
    /// rather than a <c>NamespaceError</c> about the prefix, because the local-name test comes first.
    /// </remarks>
    private static void ValidateAndExtract(Engine engine, string member, string? namespaceUri, string qualifiedName, NameContext context)
    {
        // Step 1. The empty string is not a namespace; it is the absence of one, which is what makes
        // createElementNS('', 'f:oo') a NamespaceError rather than an element with an undeclared prefix.
        if (namespaceUri is { Length: 0 })
        {
            namespaceUri = null;
        }

        string? prefix = null;
        var localName = qualifiedName;
        var colon = qualifiedName.IndexOf(':', StringComparison.Ordinal);

        // Steps 2-4: the *first* colon splits, so 'f:o:o' has the prefix 'f' and the local name 'o:o'.
        if (colon >= 0)
        {
            prefix = qualifiedName[..colon];
            localName = qualifiedName[(colon + 1)..];

            if (!IsValidNamespacePrefix(prefix))
            {
                Refuse(engine, member, DomExceptionNames.InvalidCharacter, "the qualified name '" + qualifiedName + "' has no prefix before its colon.");
            }
        }

        // Steps 6-7.
        var valid = context == NameContext.Attribute
            ? IsValidAttributeLocalName(localName)
            : IsValidElementLocalName(localName);

        if (!valid)
        {
            Refuse(engine, member, DomExceptionNames.InvalidCharacter, "the name '" + qualifiedName + "' is not a valid " + (context == NameContext.Attribute ? "attribute" : "element") + " name.");
        }

        // Step 8: a prefix names a namespace, so there has to be one to name.
        if (prefix is not null && namespaceUri is null)
        {
            Refuse(engine, member, DomExceptionNames.Namespace, "the qualified name '" + qualifiedName + "' has a prefix and no namespace.");
        }

        // Step 9.
        if (string.Equals(prefix, "xml", StringComparison.Ordinal) && !string.Equals(namespaceUri, XmlNamespace, StringComparison.Ordinal))
        {
            Refuse(engine, member, DomExceptionNames.Namespace, "the 'xml' prefix is only allowed in the XML namespace.");
        }

        // Steps 10-11: the xmlns name and the XMLNS namespace imply each other, in both directions.
        var xmlns = string.Equals(qualifiedName, "xmlns", StringComparison.Ordinal)
            || string.Equals(prefix, "xmlns", StringComparison.Ordinal);

        if (xmlns && !string.Equals(namespaceUri, XmlnsNamespace, StringComparison.Ordinal))
        {
            Refuse(engine, member, DomExceptionNames.Namespace, "the 'xmlns' name is only allowed in the XMLNS namespace.");
        }

        if (string.Equals(namespaceUri, XmlnsNamespace, StringComparison.Ordinal) && !xmlns)
        {
            Refuse(engine, member, DomExceptionNames.Namespace, "the XMLNS namespace only holds the 'xmlns' name.");
        }
    }

    private static void Refuse(Engine engine, string member, string name, string detail)
        => DomFailures.Refuse(engine, member, name, detail);

    /// <summary>
    /// Whether <paramref name="value"/> holds one of the code points every predicate forbids — ASCII
    /// whitespace, U+0000, <c>/</c> and <c>&gt;</c> — or, for an attribute local name, <c>=</c> as well.
    /// </summary>
    /// <remarks>
    /// A UTF-16 scan answers the code-point question here because every code point the predicates name is
    /// below U+0080: a surrogate pair can only form a code point above U+FFFF, which none of them is, and a
    /// lone surrogate is above U+0080 either way.
    /// </remarks>
    private static bool ContainsAny(string value, bool equals)
    {
        foreach (var c in value)
        {
            if (c is '\t' or '\n' or '\f' or '\r' or ' ' or '\0' or '/' or '>' || (equals && c == '='))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The interface a validated member is declared on, so the brand check keeps its place.</summary>
    internal enum Receiver
    {
        Document,
        Element,
    }

    /// <summary>
    /// One row of the table: where the name and its namespace are in the argument list, how many arguments
    /// the member needs before WebIDL would get as far as validating one, and which local-name predicate
    /// applies.
    /// </summary>
    internal readonly record struct Validation(
        Receiver On,
        int NamespaceIndex,
        int NameIndex,
        int Arity,
        NameContext Context)
    {
        /// <summary>
        /// Runs the member's validation, or does nothing when this call would not get that far: a receiver
        /// the member's brand check will refuse, or an argument list too short for the conversion to happen.
        /// Both are the body's <c>TypeError</c> to raise, and raising a refusal about a name first would put
        /// them in the wrong order.
        /// </summary>
        internal void Run(JsValue thisObject, string member, JsValue[] arguments)
        {
            if (arguments.Length < Arity
                || thisObject is not (IDomWrapper wrapper and Native.Object.ObjectInstance receiver)
                || !Accepts(wrapper))
            {
                return;
            }

            var qualifiedName = TypeConverter.ToString(arguments[NameIndex]);

            // A DOMString? parameter, deliberately: DOM's algorithm turns on whether the namespace is null,
            // and `createElementNS(null, 'f:oo')` is the commonest way a page reaches that step. What the
            // member then hands AngleSharp is unchanged.
            var namespaceUri = NamespaceIndex >= 0 && !arguments[NamespaceIndex].IsNullOrUndefined()
                ? TypeConverter.ToString(arguments[NamespaceIndex])
                : null;

            if (NamespaceIndex >= 0)
            {
                ValidateAndExtract(receiver.Engine, member, namespaceUri, qualifiedName, Context);
                return;
            }

            // The unprefixed members validate a local name and nothing else: `setAttribute('a:b', …)` is an
            // ordinary attribute whose name holds a colon, and no NamespaceError can arise.
            var valid = Context == NameContext.Attribute
                ? IsValidAttributeLocalName(qualifiedName)
                : IsValidElementLocalName(qualifiedName);

            if (!valid)
            {
                Refuse(
                    receiver.Engine,
                    member,
                    DomExceptionNames.InvalidCharacter,
                    "the name '" + qualifiedName + "' is not a valid " + (Context == NameContext.Attribute ? "attribute" : "element") + " name.");
            }
        }

        private bool Accepts(IDomWrapper wrapper)
            => On == Receiver.Document ? wrapper.DomTarget is IDocument : wrapper.DomTarget is IElement;
    }
}
