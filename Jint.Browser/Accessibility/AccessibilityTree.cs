using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Accessibility;

/// <summary>
/// Computes an accessibility tree over an AngleSharp document, with no layout and no script engine.
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
    private static readonly ConditionalWeakTable<IDocument, NodeIdentifiers> s_identifiers = new();

    private static readonly AxProtocolJsonContext s_indented = new(new JsonSerializerOptions(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    });

    /// <summary>Builds the accessibility tree of <paramref name="document"/>.</summary>
    internal static AxNode Build(IDocument document, AccessibilityOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        options ??= AccessibilityOptions.Default;
        var builder = new Builder(document, options);

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

        if (!string.IsNullOrEmpty(document.Url))
        {
            properties.Add(new AxProperty(AxPropertyName.Url, AxValue.String(document.Url)));
        }

        var root = new AxNode(IdOf(document, document), AriaRoles.RootWebArea)
        {
            Node = document,
            Name = NullIfEmpty(AccessibleName.Flatten(document.Title ?? string.Empty)),
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
    internal static AxNode? Build(IElement element, AccessibilityOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(element);

        options ??= AccessibilityOptions.Default;
        var document = element.Owner ?? throw new ArgumentException("The element does not belong to a document.", nameof(element));
        var builder = new Builder(document, options);

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
    internal static int IdOf(IDocument document, INode node) => s_identifiers.GetValue(document, static _ => new NodeIdentifiers()).For(node);

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
    internal static IElement? ElementFor(IDocument document, int id)
        => s_identifiers.TryGetValue(document, out var identifiers) ? identifiers.Find(document, id) : null;

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

    private static AxIgnoredReason InheritedReasonFor(IElement element, Builder builder)
    {
        for (var ancestor = element.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
        {
            var reason = builder.Visibility.ReasonFor(ancestor);
            if (reason != AxIgnoredReason.None)
            {
                return Inherit(reason);
            }
        }

        return AxIgnoredReason.None;
    }

    private static AxNode Wrap(IDocument document, IElement element, List<AxNode> nodes)
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
        private readonly IDocument _document;
        private readonly AccessibilityOptions _options;
        private readonly AccessibleName _names;

        internal Builder(IDocument document, AccessibilityOptions options)
        {
            _document = document;
            _options = options;
            Visibility = new ElementVisibility(options.UseComputedStyle);
            _names = new AccessibleName(Visibility);
        }

        internal ElementVisibility Visibility { get; }

        internal void Visit(INode node, AxIgnoredReason inherited, List<AxNode> output, bool suppressText)
        {
            switch (node)
            {
                case IText text:
                    VisitText(text, inherited, output, suppressText);
                    return;

                case IElement element:
                    VisitElement(element, inherited, output, suppressText);
                    return;

                default:
                    return;
            }
        }

        private void VisitText(IText text, AxIgnoredReason inherited, List<AxNode> output, bool suppressText)
        {
            if (!_options.IncludeText || (suppressText && !_options.IncludeIgnored))
            {
                return;
            }

            var content = AccessibleName.Flatten(text.Data);
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

        private void VisitElement(IElement element, AxIgnoredReason inherited, List<AxNode> output, bool suppressText)
        {
            if (ImplicitRole.IsMetadataContent(element))
            {
                return;
            }

            var reason = Reason(Visibility.ReasonFor(element), inherited);

            // Four of the reasons take the subtree with them: nothing inside a `display: none`, a `hidden`
            // attribute or an `aria-hidden` can come back. `visibility` is the one that can, because CSS
            // inherits it and a descendant may set it back to `visible`.
            var subtreeHidden = reason is AxIgnoredReason.Hidden or AxIgnoredReason.NotRendered
                or AxIgnoredReason.AriaHiddenElement or AxIgnoredReason.AriaHiddenSubtree;
            if (subtreeHidden && !_options.IncludeIgnored)
            {
                return;
            }

            var explicitRole = AriaRoles.Explicit(element.GetAttribute("role"));
            var role = explicitRole ?? ImplicitRole.For(element);
            if (role is null)
            {
                // HTML-AAM maps the element to no role at all: <br>, <wbr>, <input type=hidden>.
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

            if (reason == AxIgnoredReason.None && explicitRole is null && IsEmptyAlt(element))
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

        private List<AxProperty> BuildProperties(IElement element, string role, AxIgnoredReason reason, bool disabled, bool focusable)
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

            if (ReferenceEquals(_document.ActiveElement, element))
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

        private static string? CheckedState(IElement element, string role)
        {
            if (role is not ("checkbox" or "radio" or "switch" or "menuitemcheckbox" or "menuitemradio"))
            {
                return null;
            }

            var aria = element.GetAttribute("aria-checked");
            if (!string.IsNullOrWhiteSpace(aria))
            {
                return aria.ToLowerInvariant();
            }

            if (element is IHtmlInputElement input)
            {
                return input.IsIndeterminate ? "mixed" : input.IsChecked ? "true" : "false";
            }

            return null;
        }

        private static bool? ExpandedState(IElement element)
        {
            var aria = element.GetAttribute("aria-expanded");
            if (!string.IsNullOrWhiteSpace(aria))
            {
                return Flag(aria);
            }

            if (element is IHtmlDetailsElement details)
            {
                return details.IsOpen;
            }

            if (string.Equals(element.LocalName, "summary", StringComparison.Ordinal)
                && element.ParentElement is IHtmlDetailsElement parent)
            {
                return parent.IsOpen;
            }

            return null;
        }

        private static bool? SelectedState(IElement element, string role)
        {
            var aria = element.GetAttribute("aria-selected");
            if (!string.IsNullOrWhiteSpace(aria))
            {
                return Flag(aria);
            }

            if (role is "option" && element is IHtmlOptionElement option)
            {
                return option.IsSelected;
            }

            return null;
        }

        private static bool IsDisabled(IElement element)
        {
            if (string.Equals(element.GetAttribute("aria-disabled"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (element.HasAttribute("disabled"))
            {
                return true;
            }

            // A disabled fieldset disables its descendants, except those inside its first legend.
            for (var ancestor = element.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
            {
                if (ancestor is IHtmlFieldSetElement { IsDisabled: true })
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRequired(IElement element) =>
            string.Equals(element.GetAttribute("aria-required"), "true", StringComparison.OrdinalIgnoreCase)
            || element.HasAttribute("required");

        private static bool IsReadOnly(IElement element) =>
            string.Equals(element.GetAttribute("aria-readonly"), "true", StringComparison.OrdinalIgnoreCase)
            || element.HasAttribute("readonly");

        private static bool IsFocusable(IElement element, bool disabled)
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

            switch (element.LocalName)
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
                    return !string.Equals((element as IHtmlInputElement)?.Type, "hidden", StringComparison.OrdinalIgnoreCase);
                case "summary":
                    return string.Equals(element.ParentElement?.LocalName, "details", StringComparison.Ordinal);
                case "audio":
                case "video":
                    return element.HasAttribute("controls");
                default:
                    return element is IHtmlElement { IsContentEditable: true };
            }
        }

        private static int? Level(IElement element, string role)
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

        private static bool IsMultiline(IElement element, string role)
        {
            if (Flag(element.GetAttribute("aria-multiline")) is { } aria)
            {
                return aria;
            }

            return role is "textbox" && string.Equals(element.LocalName, "textarea", StringComparison.Ordinal);
        }

        private static bool IsMultiselectable(IElement element, string role)
        {
            if (Flag(element.GetAttribute("aria-multiselectable")) is { } aria)
            {
                return aria;
            }

            return role is "listbox" && element is IHtmlSelectElement { IsMultiple: true };
        }

        private static string? Invalid(IElement element)
        {
            var aria = element.GetAttribute("aria-invalid");
            if (string.IsNullOrWhiteSpace(aria))
            {
                return null;
            }

            return string.Equals(aria, "false", StringComparison.OrdinalIgnoreCase) ? null : aria.ToLowerInvariant();
        }

        private static string? Autocomplete(IElement element, string role)
        {
            var aria = element.GetAttribute("aria-autocomplete");
            if (!string.IsNullOrWhiteSpace(aria))
            {
                return aria.ToLowerInvariant();
            }

            return role is "combobox" && element.HasAttribute("list") ? "list" : null;
        }

        private static string? Live(IElement element, string role)
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

        private static string? Url(IElement element) => element switch
        {
            IHtmlAnchorElement anchor when anchor.HasAttribute("href") => anchor.Href,
            IHtmlAreaElement area when area.HasAttribute("href") => area.Href,
            IHtmlImageElement image when image.HasAttribute("src") => image.Source,
            _ => null,
        };

        /// <summary>
        /// Whether the element's text is already an ancestor's accessible name, so publishing it again would
        /// state the same string twice.
        /// </summary>
        /// <remarks>These are exactly HTML-AAM's native-label sources that take their value from content.</remarks>
        private static bool NamesAnAncestor(IElement element)
        {
            var parent = element.ParentElement;
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

        private static bool LabelsAControl(IElement label)
        {
            // A `for` that names nothing labels nothing, so its text is ordinary page text and stays.
            var target = label.GetAttribute("for");
            if (!string.IsNullOrEmpty(target))
            {
                return label.Owner?.GetElementById(target) is not null;
            }

            return label.QuerySelector("input, select, textarea, button, meter, output, progress") is not null;
        }

        private static bool IsEmptyAlt(IElement element) =>
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

    private sealed class NodeIdentifiers
    {
        private readonly ConditionalWeakTable<INode, Box> _ids = new();
        private int _next;

        internal int For(INode node) => _ids.GetValue(node, _ => new Box(Interlocked.Increment(ref _next))).Value;

        /// <summary>The element carrying <paramref name="id"/>, without assigning one to anything.</summary>
        internal IElement? Find(IDocument document, int id)
        {
            foreach (var element in document.Descendants<IElement>())
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
