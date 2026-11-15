using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Jint.HtmlParser;

using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Styling;

namespace Jint.Browser.Accessibility;

/// <summary>
/// Computes an accessibility tree over a native document, with no layout and no script engine.
/// </summary>
/// <remarks>
/// <para>
/// The roles come from <see cref="ImplicitRole"/> (HTML-AAM's mapping table) with an author <c>role</c>
/// attribute overriding them, the names from <see cref="AccessibleName"/> (accname 1.2), and the hidden
/// verdict from <see cref="ElementVisibility"/>. What none of them can answer is anything positional — off
/// screen, clipped, covered, zero-sized — because those are layout facts.
/// </para>
/// <para>
/// Node identifiers are stable for as long as the document and the element live, so two builds of an
/// unchanged document produce the same identifiers and a protocol client can address a node across calls.
/// </para>
/// </remarks>
internal static class AccessibilityTree
{
    private static readonly ConditionalWeakTable<Document, NodeIdentifiers> s_identifiers = new();

    private static readonly AxProtocolJsonContext s_indented = new(new JsonSerializerOptions(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    });

    /// <summary>Builds the accessibility tree of <paramref name="document"/>.</summary>
    internal static AxNode Build(Document document, AccessibilityOptions? options = null,
        NativeCssQueryDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        options ??= AccessibilityOptions.Default;
        var builder = new Builder(document, options, diagnostics);

        var children = new List<AxNode>();
        if (document.DocumentElement is not null)
        {
            builder.Visit(document.DocumentElement, AxIgnoredReason.None, children, suppressText: false);
        }

        // https://w3c.github.io/html-aam/#el-html — the document is focusable: it is what holds the focus
        // when nothing in it does, which is why a browser reports `focusable` on the root web area and why a
        // client that prunes uninteresting nodes keeps the root because of it.
        var properties = new List<AxProperty>
        {
            new(AxPropertyName.Focusable, AxValue.Boolean(true)),
        };

        if (!string.IsNullOrEmpty(DomDocumentState.Of(document).Url))
        {
            properties.Add(new AxProperty(AxPropertyName.Url, AxValue.String(DomDocumentState.Of(document).Url)));
        }

        var root = new AxNode(IdOf(document, document), AriaRoles.RootWebArea)
        {
            Node = document,
            Name = NullIfEmpty(AccessibleName.Flatten(ContentDom.DocumentTitle(document))),
            Properties = properties,
            Children = children,
        };

        root.AdoptChildren();
        return root;
    }

    /// <summary>
    /// Builds the accessibility tree rooted at <paramref name="element"/>, or <see langword="null"/> when the
    /// element produces no node at all.
    /// </summary>
    /// <remarks>
    /// This is what a partial tree request answers with. An element whose own node is pruned — a
    /// <c>&lt;div&gt;</c> with nothing on it — yields its first surviving descendant rather than nothing,
    /// so a caller always gets the subtree it asked about.
    /// </remarks>
    internal static AxNode? Build(Element element, AccessibilityOptions? options = null,
        NativeCssQueryDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(element);

        options ??= AccessibilityOptions.Default;
        var document = element.OwnerDocument ?? throw new ArgumentException("The element does not belong to a document.", nameof(element));
        var builder = new Builder(document, options, diagnostics);

        var nodes = new List<AxNode>();
        builder.Visit(element, InheritedReasonFor(element, builder), nodes, suppressText: false);

        return nodes.Count switch
        {
            0 => null,
            1 => nodes[0],
            _ => Wrap(document, element, nodes),
        };
    }

    /// <summary>Converts one node to the protocol shape, with its children named by identifier.</summary>
    internal static AxProtocolNode ToProtocol(AxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new AxProtocolNode
        {
            NodeId = Identifier(node.Id),
            Ignored = node.Ignored,
            IgnoredReasons = node.Ignored ? [IgnoredReasonProperty(node.IgnoredReason)] : null,
            Role = new AxProtocolValue(node.Ignored ? "internalRole" : "role", AxValue.Role(node.Role)),
            Name = node.Name is null ? null : new AxProtocolValue("computedString", AxValue.ComputedString(node.Name)),
            Description = node.Description is null ? null : new AxProtocolValue("computedString", AxValue.ComputedString(node.Description)),
            Value = node.Value is null ? null : new AxProtocolValue("computedString", AxValue.ComputedString(node.Value)),
            Properties = node.Properties.Count == 0 ? null : node.Properties.Select(ToProtocol).ToArray(),
            ParentId = node.Parent is null ? null : Identifier(node.Parent.Id),
            ChildIds = node.Children.Count == 0 ? null : node.Children.Select(child => Identifier(child.Id)).ToArray(),
        };
    }

    /// <summary>Flattens the tree into the node list <c>Accessibility.getFullAXTree</c> answers with.</summary>
    internal static IReadOnlyList<AxProtocolNode> Flatten(AxNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var nodes = new List<AxProtocolNode>();
        Collect(root, nodes);
        return nodes;

        static void Collect(AxNode node, List<AxProtocolNode> into)
        {
            into.Add(ToProtocol(node));
            foreach (var child in node.Children)
            {
                Collect(child, into);
            }
        }
    }

    /// <summary>Writes the flattened tree as the JSON body of a <c>getFullAXTree</c> reply's <c>nodes</c>.</summary>
    internal static string ToJson(AxNode root, bool indented = false) =>
        JsonSerializer.Serialize(Flatten(root), indented ? s_indented.IReadOnlyListAxProtocolNode : AxProtocolJsonContext.Default.IReadOnlyListAxProtocolNode);

    /// <summary>The identifier this document gave the node, assigning one when it has none yet.</summary>
    internal static int IdOf(Document document, Node node) => s_identifiers.GetValue(document, static _ => new NodeIdentifiers()).For(node);

    /// <summary>The element this document already gave <paramref name="id"/> to, or <see langword="null"/>.</summary>
    /// <remarks>
    /// <para>
    /// The reverse of <see cref="IdOf"/>, and it is what makes a reference printed in a snapshot something a
    /// caller can act on — <c>Page.ClickAsync("ref=12")</c> resolves through here.
    /// </para>
    /// <para>
    /// <b>It assigns nothing.</b> A walk that asked <see cref="IdOf"/> for every node would hand identifiers
    /// to the whole document and move every one a later snapshot prints, so this reads only the table and
    /// answers <see langword="null"/> for an identifier no snapshot has published. It is linear in the size
    /// of the document, which a click can afford and a render loop could not.
    /// </para>
    /// </remarks>
    internal static Element? ElementFor(Document document, int id)
        => s_identifiers.TryGetValue(document, out var identifiers) ? identifiers.Find(document, id) : null;

    /// <summary>
    /// Appends to <paramref name="output"/>, in document order, every element under one of
    /// <paramref name="scopes"/> whose accessibility node <paramref name="query"/> matches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the snapshot's own walk.</b> One <see cref="Builder"/> serves every scope, so the role, the
    /// accessible name and the hidden verdict are the ones <see cref="Build(Document, AccessibilityOptions?, NativeCssQueryDiagnostics?)"/>
    /// computes, from the same visibility cascade, and nothing runs the page's script. Unlike a build it
    /// assigns no identifier and computes a name only for an element whose role and states already matched.
    /// </para>
    /// <para>
    /// A document scope includes its document element; an element scope contributes its descendants and not
    /// itself, which is what a CSS scope means in a chained target. Each scope's matches are appended in
    /// document order; a caller passing several scopes, nested or not, restores order and removes duplicates.
    /// </para>
    /// <para>
    /// <paramref name="checkpoint"/> runs once per element visited, which is how a page's turn budget and
    /// cancellation reach a walk that runs no script; <paramref name="first"/> stops at the first match.
    /// </para>
    /// </remarks>
    internal static void Query(Document document, IReadOnlyList<Node> scopes, AxRoleQuery query, List<Element> output,
        bool first = false, Action? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(query);

        var builder = new Builder(document, AccessibilityOptions.Snapshot, diagnostics: null);
        var search = new RoleSearch(query, output, first, checkpoint);
        foreach (var scope in scopes)
        {
            switch (scope)
            {
                case Document { DocumentElement: { } root }:
                    builder.Query(root, AxIgnoredReason.None, search);
                    break;

                case Element element when builder.TryEnter(element, InheritedReasonFor(element, builder),
                    query.IncludeHidden, out var reason, out _, out _):
                    foreach (var child in element.ChildNodes)
                    {
                        if (search.Done)
                        {
                            return;
                        }

                        builder.Query(child, reason, search);
                    }

                    break;
            }

            if (search.Done)
            {
                return;
            }
        }
    }

    private static string Identifier(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static AxProtocolProperty ToProtocol(AxProperty property) =>
        new(property.ProtocolName, new AxProtocolValue(TypeName(property.Value.Type), property.Value));

    private static AxProtocolProperty IgnoredReasonProperty(AxIgnoredReason reason)
    {
        var name = reason switch
        {
            AxIgnoredReason.Hidden => AxPropertyName.Hidden,
            AxIgnoredReason.NotRendered => AxPropertyName.NotRendered,
            AxIgnoredReason.NotVisible => AxPropertyName.NotVisible,
            AxIgnoredReason.AriaHiddenElement => AxPropertyName.AriaHiddenElement,
            AxIgnoredReason.AriaHiddenSubtree => AxPropertyName.AriaHiddenSubtree,
            AxIgnoredReason.PresentationalRole => AxPropertyName.PresentationalRole,
            AxIgnoredReason.EmptyAlt => AxPropertyName.EmptyAlt,
            AxIgnoredReason.EmptyText => AxPropertyName.EmptyText,
            _ => AxPropertyName.Uninteresting,
        };

        var property = new AxProperty(name, AxValue.Boolean(true));
        return new AxProtocolProperty(property.ProtocolName, new AxProtocolValue("boolean", property.Value));
    }

    private static string TypeName(AxValueType type) => type switch
    {
        AxValueType.Boolean => "boolean",
        AxValueType.Tristate => "tristate",
        AxValueType.Integer => "integer",
        AxValueType.Number => "number",
        AxValueType.String => "string",
        AxValueType.ComputedString => "computedString",
        AxValueType.Token => "token",
        AxValueType.Role => "role",
        _ => "string",
    };

    private static AxIgnoredReason InheritedReasonFor(Element element, Builder builder)
    {
        for (var ancestor = (element.ParentNode as Element); ancestor is not null; ancestor = (ancestor.ParentNode as Element))
        {
            var reason = builder.ReasonFor(ancestor);
            if (reason != AxIgnoredReason.None)
            {
                return Inherit(reason);
            }
        }

        return AxIgnoredReason.None;
    }

    private static AxNode Wrap(Document document, Element element, List<AxNode> nodes)
    {
        // The element's own node was pruned but several of its descendants survived, so they need a holder.
        // It takes the element's identifier rather than a fresh one: it stands for that element.
        var wrapper = new AxNode(IdOf(document, element), AriaRoles.Generic)
        {
            Element = element,
            Node = element,
            Children = nodes,
        };

        wrapper.AdoptChildren();
        return wrapper;
    }

    private static AxIgnoredReason Inherit(AxIgnoredReason reason) => reason switch
    {
        AxIgnoredReason.AriaHiddenElement => AxIgnoredReason.AriaHiddenSubtree,
        _ => reason,
    };

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    private sealed class Builder
    {
        private readonly Document _document;
        private readonly AccessibilityOptions _options;
        private readonly AccessibleName _names;
        private readonly CssCascade.Traversal? _cascade;

        internal Builder(Document document, AccessibilityOptions options, NativeCssQueryDiagnostics? diagnostics)
        {
            _document = document;
            _options = options;
            Visibility = new ElementVisibility(options.UseComputedStyle, diagnostics: diagnostics);
            _cascade = Visibility.CreateTraversal(document);
            _names = new AccessibleName(Visibility, _cascade);
        }

        internal ElementVisibility Visibility { get; }

        internal AxIgnoredReason ReasonFor(Element element) => Visibility.ReasonFor(element, _cascade);

        internal void Visit(Node node, AxIgnoredReason inherited, List<AxNode> output, bool suppressText)
        {
            switch (node)
            {
                case Text text:
                    VisitText(text, text.Data, inherited, output, suppressText);
                    return;

                case CDataSection cdata:
                    VisitText(cdata, cdata.Data, inherited, output, suppressText);
                    return;

                case Element element:
                    VisitElement(element, inherited, output, suppressText);
                    return;

                default:
                    return;
            }
        }

        /// <summary>
        /// The element half of <see cref="Visit"/>'s decisions — which elements the walk reaches, with which
        /// role and hidden verdict — collecting the elements <paramref name="search"/>'s query matches.
        /// </summary>
        internal void Query(Node node, AxIgnoredReason inherited, RoleSearch search)
        {
            if (node is not Element element)
            {
                return;
            }

            search.Checkpoint?.Invoke();
            var query = search.Query;
            if (!TryEnter(element, inherited, query.IncludeHidden, out var reason, out var role, out _))
            {
                return;
            }

            if ((query.IncludeHidden || !IsHidden(reason))
                && Matches(element, role, query)
                && (query.Name is null || MatchesName(_names.Compute(element, role, hiddenRoot: query.IncludeHidden), query)))
            {
                search.Output.Add(element);
                if (search.First)
                {
                    search.Done = true;
                    return;
                }
            }

            foreach (var child in element.ChildNodes)
            {
                Query(child, reason, search);
                if (search.Done)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// The per-element decision both walks share: whether the walk enters the element at all, with which
        /// hidden verdict and which role.
        /// </summary>
        /// <param name="element">The element the walk reached.</param>
        /// <param name="inherited">The verdict its parent passed down.</param>
        /// <param name="keepHidden">Whether a subtree-hiding verdict is still entered.</param>
        /// <param name="reason">The element's own verdict, which its children inherit.</param>
        /// <param name="role">The computed role: the author's, else HTML-AAM's.</param>
        /// <param name="explicitRole">Whether the role came from a <c>role</c> attribute.</param>
        /// <returns>
        /// <see langword="false"/> for metadata content, a hidden subtree that is not kept, and an element
        /// HTML-AAM maps to no role at all (<c>&lt;br&gt;</c>, <c>&lt;wbr&gt;</c>, <c>&lt;input type=hidden&gt;</c>).
        /// </returns>
        internal bool TryEnter(Element element, AxIgnoredReason inherited, bool keepHidden,
            out AxIgnoredReason reason, out string role, out bool explicitRole)
        {
            reason = AxIgnoredReason.None;
            role = string.Empty;
            explicitRole = false;
            if (ImplicitRole.IsMetadataContent(element))
            {
                return false;
            }

            reason = Reason(ReasonFor(element), inherited);

            // Four of the reasons take the subtree with them: nothing inside a `display: none`, a `hidden`
            // attribute or an `aria-hidden` can come back. `visibility` is the one that can, because CSS
            // inherits it and a descendant may set it back to `visible`.
            if (IsSubtreeHidden(reason) && !keepHidden)
            {
                return false;
            }

            var author = AriaRoles.Explicit(element.GetAttribute("role"));
            explicitRole = author is not null;
            if ((author ?? ImplicitRole.For(element)) is not { } computed)
            {
                return false;
            }

            role = computed;
            return true;
        }

        private void VisitText(Node text, string data, AxIgnoredReason inherited, List<AxNode> output, bool suppressText)
        {
            if (!_options.IncludeText || (suppressText && !_options.IncludeIgnored))
            {
                return;
            }

            var content = AccessibleName.Flatten(data);
            var reason = inherited != AxIgnoredReason.None ? inherited
                : content.Length == 0 ? AxIgnoredReason.EmptyText
                : AxIgnoredReason.None;

            if (reason != AxIgnoredReason.None && !_options.IncludeIgnored)
            {
                return;
            }

            output.Add(new AxNode(IdOf(_document, text), AriaRoles.StaticText)
            {
                Node = text,
                Name = NullIfEmpty(content),
                IgnoredReason = reason,
                Properties = reason == AxIgnoredReason.None ? [] : [new AxProperty(AxPropertyName.Hidden, AxValue.Boolean(true))],
            });
        }

        private void VisitElement(Element element, AxIgnoredReason inherited, List<AxNode> output, bool suppressText)
        {
            if (!TryEnter(element, inherited, _options.IncludeIgnored, out var reason, out var role, out var explicitRole))
            {
                return;
            }

            // Text this element's own name already carries is not repeated as a node of its own: a
            // button's label, a <label>'s text, a <legend>, a <caption>, a <figcaption>.
            var suppressBelow = suppressText || AriaRoles.NameFromContent.Contains(role) || NamesAnAncestor(element);

            var children = new List<AxNode>();
            foreach (var child in element.ChildNodes)
            {
                Visit(child, reason, children, suppressBelow);
            }

            if (reason == AxIgnoredReason.None && !explicitRole && IsEmptyAlt(element))
            {
                reason = AxIgnoredReason.EmptyAlt;
            }

            if (reason == AxIgnoredReason.None && AriaRoles.IsPresentational(role))
            {
                reason = AxIgnoredReason.PresentationalRole;
            }

            var name = _names.Compute(element, role);
            var disabled = IsDisabled(element);
            var focusable = reason == AxIgnoredReason.None && IsFocusable(element, disabled);

            if (reason != AxIgnoredReason.None)
            {
                if (!_options.IncludeIgnored)
                {
                    // An ignored node that did not take its subtree with it is replaced by its children.
                    output.AddRange(children);
                    return;
                }
            }
            else if (!_options.IncludeGeneric && children.Count == 0 && name.Length == 0 && NamesAnAncestor(element))
            {
                // Everything this element had was its ancestor's accessible name, and it has nothing of its
                // own: an empty <caption> under a named <figure> states nothing twice over.
                return;
            }
            else if (string.Equals(role, AriaRoles.Generic, StringComparison.Ordinal)
                     && !_options.IncludeGeneric
                     && name.Length == 0
                     && !focusable)
            {
                // A generic wrapper with nothing on it is replaced by its children rather than dropped with
                // them, so the tree stays complete while losing the levels an agent cannot act on.
                output.AddRange(children);
                return;
            }

            var value = ControlValue.For(element, role);
            var node = new AxNode(IdOf(_document, element), role)
            {
                Element = element,
                Node = element,
                Name = NullIfEmpty(name),
                Description = NullIfEmpty(_names.ComputeDescription(element, name)),
                Value = NullIfEmpty(value),
                IgnoredReason = reason,
                Properties = BuildProperties(element, role, reason, disabled, focusable),
                Children = children,
            };

            node.AdoptChildren();
            output.Add(node);
        }

        private AxIgnoredReason Reason(AxIgnoredReason own, AxIgnoredReason inherited)
        {
            if (own != AxIgnoredReason.None)
            {
                return own;
            }

            if (inherited == AxIgnoredReason.None)
            {
                return AxIgnoredReason.None;
            }

            // `visibility` is the one CSS inherits, so with the cascade available the element's own verdict
            // is the whole answer and a `visibility: visible` child of a hidden parent is visible again.
            // Without the cascade there is nothing to resolve it against, so the ancestor's verdict stands.
            if (inherited == AxIgnoredReason.NotVisible && Visibility.CascadeAvailable)
            {
                return AxIgnoredReason.None;
            }

            return Inherit(inherited);
        }

        private List<AxProperty> BuildProperties(Element element, string role, AxIgnoredReason reason, bool disabled, bool focusable)
        {
            var properties = new List<AxProperty>();

            if (reason != AxIgnoredReason.None)
            {
                properties.Add(new AxProperty(AxPropertyName.Hidden, AxValue.Boolean(true)));
            }

            AddTristate(properties, AxPropertyName.Checked, CheckedState(element, role));
            AddTristate(properties, AxPropertyName.Pressed, element.GetAttribute("aria-pressed"));
            AddBoolean(properties, AxPropertyName.Expanded, ExpandedState(element));
            AddBoolean(properties, AxPropertyName.Selected, SelectedState(element, role));

            if (disabled)
            {
                properties.Add(new AxProperty(AxPropertyName.Disabled, AxValue.Boolean(true)));
            }

            if (IsRequired(element))
            {
                properties.Add(new AxProperty(AxPropertyName.Required, AxValue.Boolean(true)));
            }

            if (IsReadOnly(element))
            {
                properties.Add(new AxProperty(AxPropertyName.Readonly, AxValue.Boolean(true)));
            }

            if (focusable)
            {
                properties.Add(new AxProperty(AxPropertyName.Focusable, AxValue.Boolean(true)));
            }

            if (ReferenceEquals(Events.BrowserEventRealm.FocusedElementOf(_document), element)
                && ReferenceEquals(DomNodeMembers.Root(element), _document))
            {
                properties.Add(new AxProperty(AxPropertyName.Focused, AxValue.Boolean(true)));
            }

            if (Level(element, role) is { } level)
            {
                properties.Add(new AxProperty(AxPropertyName.Level, AxValue.Integer(level)));
            }

            if (IsMultiline(element, role))
            {
                properties.Add(new AxProperty(AxPropertyName.Multiline, AxValue.Boolean(true)));
            }

            if (IsMultiselectable(element, role))
            {
                properties.Add(new AxProperty(AxPropertyName.Multiselectable, AxValue.Boolean(true)));
            }

            AddToken(properties, AxPropertyName.Orientation, element.GetAttribute("aria-orientation"));
            AddToken(properties, AxPropertyName.Invalid, Invalid(element));
            AddToken(properties, AxPropertyName.Autocomplete, Autocomplete(element, role));
            AddToken(properties, AxPropertyName.HasPopup, element.GetAttribute("aria-haspopup"));
            AddToken(properties, AxPropertyName.Live, Live(element, role));
            AddBoolean(properties, AxPropertyName.Atomic, Flag(element.GetAttribute("aria-atomic")));
            AddBoolean(properties, AxPropertyName.Busy, Flag(element.GetAttribute("aria-busy")));
            AddBoolean(properties, AxPropertyName.Modal, Flag(element.GetAttribute("aria-modal")));

            var (minimum, maximum) = ControlValue.Range(element, role);
            if (minimum is { } low)
            {
                properties.Add(new AxProperty(AxPropertyName.Valuemin, AxValue.Number(low)));
            }

            if (maximum is { } high)
            {
                properties.Add(new AxProperty(AxPropertyName.Valuemax, AxValue.Number(high)));
            }

            AddToken(properties, AxPropertyName.Valuetext, element.GetAttribute("aria-valuetext"));

            if (Url(element) is { } url)
            {
                properties.Add(new AxProperty(AxPropertyName.Url, AxValue.String(url)));
            }

            return properties;
        }

        private static void AddBoolean(List<AxProperty> properties, AxPropertyName name, bool? value)
        {
            if (value is { } flag)
            {
                properties.Add(new AxProperty(name, AxValue.Boolean(flag)));
            }
        }

        private static void AddToken(List<AxProperty> properties, AxPropertyName name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                properties.Add(new AxProperty(name, AxValue.Token(value)));
            }
        }

        private static void AddTristate(List<AxProperty> properties, AxPropertyName name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                properties.Add(new AxProperty(name, AxValue.Tristate(value)));
            }
        }

        /// <summary>Whether the reason hides the element's whole subtree, which only <c>visibility</c> does not.</summary>
        private static bool IsSubtreeHidden(AxIgnoredReason reason) => reason is AxIgnoredReason.Hidden
            or AxIgnoredReason.NotRendered or AxIgnoredReason.AriaHiddenElement or AxIgnoredReason.AriaHiddenSubtree;

        /// <summary>Whether the reason is one of the visibility verdicts, rather than a pruning one.</summary>
        private static bool IsHidden(AxIgnoredReason reason) => reason == AxIgnoredReason.NotVisible || IsSubtreeHidden(reason);

        private static bool MatchesState(string? state, string? wanted, bool stateful)
        {
            if (wanted is null)
            {
                return true;
            }

            // A role that has the state but whose element states nothing is in its default, unchecked state.
            return string.Equals(state ?? (stateful ? "false" : null), wanted, StringComparison.Ordinal);
        }

        private static bool MatchesFlag(bool? state, bool? wanted) => wanted is null || state == wanted;

        private static bool MatchesName(string name, AxRoleQuery query)
        {
            if (query.Name is not { } wanted)
            {
                return true;
            }

            return query.NameExact
                ? string.Equals(name, wanted, StringComparison.Ordinal)
                : name.Contains(wanted, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Matches(Element element, string role, AxRoleQuery query)
        {
            if (query.Role is { } wanted && !string.Equals(AriaRoles.Canonical(role), AriaRoles.Canonical(wanted), StringComparison.Ordinal))
            {
                return false;
            }

            if (!MatchesState(CheckedState(element, role), query.Checked, IsCheckable(role)))
            {
                return false;
            }

            if (query.Pressed is not null)
            {
                var pressed = element.GetAttribute("aria-pressed");
                if (!string.Equals(role, "button", StringComparison.Ordinal)
                    || !MatchesState(string.IsNullOrWhiteSpace(pressed) ? null : pressed.ToLowerInvariant(), query.Pressed, stateful: true))
                {
                    return false;
                }
            }

            // WAI-ARIA 1.2 supports aria-selected, aria-expanded and aria-level on these roles only, which is
            // also where Playwright's role selector answers them; elsewhere the state is not the role's to have.
            if (query.Selected is not null
                && (!s_selectedRoles.Contains(role) || !MatchesFlag(SelectedState(element, role) ?? false, query.Selected)))
            {
                return false;
            }

            if (query.Expanded is not null
                && ((!s_expandedRoles.Contains(role) && element is not { NamespaceUri: Namespaces.Html, LocalName: "details" })
                    || !MatchesFlag(ExpandedState(element), query.Expanded)))
            {
                return false;
            }

            if (query.Level is { } level && (!s_levelRoles.Contains(role) || Level(element, role) != level))
            {
                return false;
            }

            return query.Disabled is not { } disabled || IsDisabled(element) == disabled;
        }

        private static readonly FrozenSet<string> s_selectedRoles = new[]
        {
            "columnheader", "gridcell", "option", "row", "rowheader", "tab", "treeitem",
        }.ToFrozenSet(StringComparer.Ordinal);

        private static readonly FrozenSet<string> s_expandedRoles = new[]
        {
            "application", "button", "checkbox", "columnheader", "combobox", "gridcell", "link", "listbox", "menuitem",
            "menuitemcheckbox", "menuitemradio", "row", "rowheader", "switch", "tab", "treeitem",
        }.ToFrozenSet(StringComparer.Ordinal);

        private static readonly FrozenSet<string> s_levelRoles = new[]
        {
            "heading", "listitem", "row", "treeitem",
        }.ToFrozenSet(StringComparer.Ordinal);

        /// <summary>The roles whose node publishes a <c>checked</c> state.</summary>
        private static bool IsCheckable(string role) =>
            role is "checkbox" or "radio" or "switch" or "menuitemcheckbox" or "menuitemradio";

        private static string? CheckedState(Element element, string role)
        {
            if (!IsCheckable(role))
            {
                return null;
            }

            var aria = element.GetAttribute("aria-checked");
            if (!string.IsNullOrWhiteSpace(aria))
            {
                return aria.ToLowerInvariant();
            }

            if (HtmlCheckableState.Get(element) is { Type: HtmlInputType.Checkbox or HtmlInputType.Radio } input)
            {
                return input.Type == HtmlInputType.Checkbox && input.Indeterminate ? "mixed" : input.Checked ? "true" : "false";
            }

            return null;
        }

        private static bool? ExpandedState(Element element)
        {
            var aria = element.GetAttribute("aria-expanded");
            if (!string.IsNullOrWhiteSpace(aria))
            {
                return Flag(aria);
            }

            if (element is { NamespaceUri: Namespaces.Html, LocalName: "details" })
            {
                return element.HasAttribute("open");
            }

            if (string.Equals(element.LocalName, "summary", StringComparison.Ordinal)
                && element.ParentNode is Element { NamespaceUri: Namespaces.Html, LocalName: "details" } parent)
            {
                return parent.HasAttribute("open");
            }

            return null;
        }

        private static bool? SelectedState(Element element, string role)
        {
            var aria = element.GetAttribute("aria-selected");
            if (!string.IsNullOrWhiteSpace(aria))
            {
                return Flag(aria);
            }

            if (role is "option" && element is { NamespaceUri: Namespaces.Html, LocalName: "option" })
            {
                return element.GetHtmlState()!.GetOptionState(CancellationToken.None)!.Selected;
            }

            return null;
        }

        private static bool IsDisabled(Element element)
        {
            if (string.Equals(element.GetAttribute("aria-disabled"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return HtmlDisabledness.GetState(element, CancellationToken.None) == HtmlDisabledState.Disabled;
        }

        private static bool IsRequired(Element element) =>
            string.Equals(element.GetAttribute("aria-required"), "true", StringComparison.OrdinalIgnoreCase)
            || element.HasAttribute("required");

        private static bool IsReadOnly(Element element) =>
            string.Equals(element.GetAttribute("aria-readonly"), "true", StringComparison.OrdinalIgnoreCase)
            || element.HasAttribute("readonly");

        private static bool IsFocusable(Element element, bool disabled)
        {
            if (disabled)
            {
                return false;
            }

            var tabIndex = element.GetAttribute("tabindex");
            if (tabIndex is not null && int.TryParse(tabIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                return index > int.MinValue;
            }

            switch (ContentDom.HtmlName(element))
            {
                case "a":
                case "area":
                    return element.HasAttribute("href");
                case "button":
                case "select":
                case "textarea":
                case "iframe":
                case "object":
                case "embed":
                    return true;
                case "input":
                    return !string.Equals(ContentDom.InputType(element), "hidden", StringComparison.OrdinalIgnoreCase);
                case "summary":
                    return string.Equals((element.ParentNode as Element)?.LocalName, "details", StringComparison.Ordinal);
                case "audio":
                case "video":
                    return element.HasAttribute("controls");
                default:
                    return element.NamespaceUri == Namespaces.Html && Events.ContentEditing.HostOf(element) is not null;
            }
        }

        private static int? Level(Element element, string role)
        {
            var aria = element.GetAttribute("aria-level");
            if (int.TryParse(aria, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
            {
                return level;
            }

            if (!string.Equals(role, "heading", StringComparison.Ordinal))
            {
                return null;
            }

            return element.LocalName switch
            {
                "h1" => 1,
                "h2" => 2,
                "h3" => 3,
                "h4" => 4,
                "h5" => 5,
                "h6" => 6,
                _ => 2,
            };
        }

        private static bool IsMultiline(Element element, string role)
        {
            if (Flag(element.GetAttribute("aria-multiline")) is { } aria)
            {
                return aria;
            }

            return role is "textbox" && string.Equals(element.LocalName, "textarea", StringComparison.Ordinal);
        }

        private static bool IsMultiselectable(Element element, string role)
        {
            if (Flag(element.GetAttribute("aria-multiselectable")) is { } aria)
            {
                return aria;
            }

            return role is "listbox" && element is { NamespaceUri: Namespaces.Html, LocalName: "select" } && element.HasAttribute("multiple");
        }

        private static string? Invalid(Element element)
        {
            var aria = element.GetAttribute("aria-invalid");
            if (string.IsNullOrWhiteSpace(aria))
            {
                return null;
            }

            return string.Equals(aria, "false", StringComparison.OrdinalIgnoreCase) ? null : aria.ToLowerInvariant();
        }

        private static string? Autocomplete(Element element, string role)
        {
            var aria = element.GetAttribute("aria-autocomplete");
            if (!string.IsNullOrWhiteSpace(aria))
            {
                return aria.ToLowerInvariant();
            }

            return role is "combobox" && element.HasAttribute("list") ? "list" : null;
        }

        private static string? Live(Element element, string role)
        {
            var aria = element.GetAttribute("aria-live");
            if (!string.IsNullOrWhiteSpace(aria))
            {
                return aria.ToLowerInvariant();
            }

            return role switch
            {
                "alert" => "assertive",
                "log" or "status" => "polite",
                _ => null,
            };
        }

        private static string? Url(Element element) => element.LocalName switch
        {
            "a" or "area" => ContentDom.Url(element, "href"),
            "img" => ContentDom.Url(element, "src"),
            _ => null,
        };

        /// <summary>
        /// Whether the element's text is already an ancestor's accessible name, so publishing it again would
        /// state the same string twice.
        /// </summary>
        /// <remarks>These are exactly HTML-AAM's native-label sources that take their value from content.</remarks>
        private static bool NamesAnAncestor(Element element)
        {
            var parent = (element.ParentNode as Element);
            if (parent is null)
            {
                return false;
            }

            return element.LocalName switch
            {
                "legend" => string.Equals(parent.LocalName, "fieldset", StringComparison.Ordinal),
                "caption" => parent.LocalName is "table" or "figure",
                "figcaption" => string.Equals(parent.LocalName, "figure", StringComparison.Ordinal),
                "label" => LabelsAControl(element),
                _ => false,
            };
        }

        private static bool LabelsAControl(Element label)
        {
            return HtmlLabelAssociation.ControlFor(label) is not null;
        }

        private static bool IsEmptyAlt(Element element) =>
            string.Equals(element.LocalName, "img", StringComparison.Ordinal)
            && element.HasAttribute("alt")
            && element.GetAttribute("alt")!.Length == 0;

        private static bool? Flag(string? value) => value switch
        {
            null => null,
            _ when string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) => true,
            _ when string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) => false,
            _ => null,
        };
    }

    /// <summary>One role query's state across the scopes it searches.</summary>
    private sealed class RoleSearch(AxRoleQuery query, List<Element> output, bool first, Action? checkpoint)
    {
        internal AxRoleQuery Query { get; } = query;

        internal List<Element> Output { get; } = output;

        internal bool First { get; } = first;

        internal Action? Checkpoint { get; } = checkpoint;

        /// <summary>Whether a first-only search has found its match.</summary>
        internal bool Done { get; set; }
    }

    private sealed class NodeIdentifiers
    {
        private readonly ConditionalWeakTable<Node, Box> _ids = new();
        private int _next;

        internal int For(Node node) => _ids.GetValue(node, _ => new Box(Interlocked.Increment(ref _next))).Value;

        /// <summary>The element carrying <paramref name="id"/>, without assigning one to anything.</summary>
        internal Element? Find(Document document, int id)
        {
            foreach (var element in ContentDom.Descendants(document))
            {
                if (_ids.TryGetValue(element, out var box) && box.Value == id)
                {
                    return element;
                }
            }

            return null;
        }

        private sealed class Box(int value)
        {
            internal int Value { get; } = value;
        }
    }
}
