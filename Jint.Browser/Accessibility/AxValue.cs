using System.Globalization;
using System.Runtime.InteropServices;

namespace Jint.Browser.Accessibility;

/// <summary>
/// The type tag a computed accessibility value carries, mirroring the Chrome DevTools Protocol's
/// <c>Accessibility.AXValueType</c>.
/// </summary>
/// <remarks>
/// Only the members this package produces are listed. The protocol's enumeration is larger — it also carries
/// the source-tracking types <c>idref</c>, <c>nodeList</c> and <c>domRelation</c> that a real browser fills in
/// from its layout tree — and a member never produced here would be a name nothing writes.
/// </remarks>
internal enum AxValueType
{
    /// <summary>A <see langword="true"/>/<see langword="false"/> value.</summary>
    Boolean,

    /// <summary>A <c>"true"</c>/<c>"false"</c>/<c>"mixed"</c> value, as ARIA's tristate attributes carry.</summary>
    Tristate,

    /// <summary>An integral value, such as a heading level.</summary>
    Integer,

    /// <summary>A numeric value, such as <c>aria-valuemin</c>.</summary>
    Number,

    /// <summary>A literal string, such as a URL.</summary>
    String,

    /// <summary>A string the algorithm computed rather than read, such as an accessible name.</summary>
    ComputedString,

    /// <summary>One value out of a closed set, such as <c>aria-orientation</c>'s.</summary>
    Token,

    /// <summary>A role name.</summary>
    Role,
}

/// <summary>
/// One computed accessibility value: a type tag and the value itself.
/// </summary>
/// <remarks>
/// The value is kept as its CLR primitive rather than as a string so that the protocol writer can emit a JSON
/// boolean, number or string without re-parsing what the builder already knew.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct AxValue
{
    private AxValue(AxValueType type, string? text, bool? flag, double? number)
    {
        Type = type;
        Text = text;
        Flag = flag;
        Numeric = number;
    }

    /// <summary>What kind of value this is.</summary>
    internal AxValueType Type { get; }

    /// <summary>The value, when it is a string, a token, a tristate or a role; otherwise <see langword="null"/>.</summary>
    internal string? Text { get; }

    /// <summary>The value, when it is a boolean; otherwise <see langword="null"/>.</summary>
    internal bool? Flag { get; }

    /// <summary>The value, when it is a number or an integer; otherwise <see langword="null"/>.</summary>
    internal double? Numeric { get; }

    internal static AxValue Boolean(bool value) => new(AxValueType.Boolean, text: null, value, number: null);

    internal static AxValue Tristate(string value) => new(AxValueType.Tristate, value, flag: null, number: null);

    internal static AxValue Integer(int value) => new(AxValueType.Integer, text: null, flag: null, value);

    internal static AxValue Number(double value) => new(AxValueType.Number, text: null, flag: null, value);

    internal static AxValue String(string value) => new(AxValueType.String, value, flag: null, number: null);

    internal static AxValue ComputedString(string value) => new(AxValueType.ComputedString, value, flag: null, number: null);

    internal static AxValue Token(string value) => new(AxValueType.Token, value, flag: null, number: null);

    internal static AxValue Role(string value) => new(AxValueType.Role, value, flag: null, number: null);

    /// <summary>Renders the value the way the compact snapshot prints it.</summary>
    internal string ToDisplayString() => Type switch
    {
        AxValueType.Boolean => Flag == true ? "true" : "false",
        AxValueType.Integer => ((int) (Numeric ?? 0)).ToString(CultureInfo.InvariantCulture),
        AxValueType.Number => (Numeric ?? 0).ToString("0.############", CultureInfo.InvariantCulture),
        _ => Text ?? string.Empty,
    };
}

/// <summary>
/// The name of a computed accessibility property, spelled as the Chrome DevTools Protocol's
/// <c>Accessibility.AXPropertyName</c> spells it.
/// </summary>
/// <remarks>
/// The protocol's enumeration is closed, so every member here has to be one of its names — which is why
/// <c>placeholder</c> is absent. HTML-AAM routes a placeholder into the accessible name or, when the element
/// already has one, into the accessible description, and that is where this builder puts it.
/// </remarks>
internal enum AxPropertyName
{
    /// <summary>Whether a checkbox, radio or switch is checked, as a tristate.</summary>
    Checked,

    /// <summary>Whether a toggle button is pressed, as a tristate.</summary>
    Pressed,

    /// <summary>Whether a disclosure is expanded.</summary>
    Expanded,

    /// <summary>Whether an option, tab or row is selected.</summary>
    Selected,

    /// <summary>Whether the element is disabled.</summary>
    Disabled,

    /// <summary>Whether a form control must be filled in.</summary>
    Required,

    /// <summary>Whether a form control rejects edits.</summary>
    Readonly,

    /// <summary>Whether the element can take focus.</summary>
    Focusable,

    /// <summary>Whether the element currently has focus.</summary>
    Focused,

    /// <summary>Whether the element is hidden from the accessibility tree.</summary>
    Hidden,

    /// <summary>Which ancestor made a hidden element hidden.</summary>
    HiddenRoot,

    /// <summary>A heading's or tree item's level.</summary>
    Level,

    /// <summary>Whether a textbox accepts more than one line.</summary>
    Multiline,

    /// <summary>Whether more than one descendant may be selected at a time.</summary>
    Multiselectable,

    /// <summary>Whether the element is laid out horizontally or vertically.</summary>
    Orientation,

    /// <summary>Whether the element's value fails validation.</summary>
    Invalid,

    /// <summary>What kind of completion a text input offers.</summary>
    Autocomplete,

    /// <summary>What kind of pop-up the element triggers.</summary>
    HasPopup,

    /// <summary>A range widget's minimum.</summary>
    Valuemin,

    /// <summary>A range widget's maximum.</summary>
    Valuemax,

    /// <summary>A range widget's human-readable value.</summary>
    Valuetext,

    /// <summary>How assertively a live region announces updates.</summary>
    Live,

    /// <summary>Whether a live region announces itself in full on every update.</summary>
    Atomic,

    /// <summary>Whether the element is a modal dialog.</summary>
    Modal,

    /// <summary>The resource a link or image points at.</summary>
    Url,

    /// <summary>Whether the element's content is being updated.</summary>
    Busy,

    /// <summary>Whether the element's text can be edited.</summary>
    Editable,

    /// <summary>An element whose <c>aria-hidden</c> removed the node.</summary>
    AriaHiddenElement,

    /// <summary>An ancestor whose <c>aria-hidden</c> removed the node.</summary>
    AriaHiddenSubtree,

    /// <summary>An image whose <c>alt</c> is present and empty.</summary>
    EmptyAlt,

    /// <summary>A text node that collapses to nothing.</summary>
    EmptyText,

    /// <summary>An element the flat rendering model would not render.</summary>
    NotRendered,

    /// <summary>An element rendered but not visible.</summary>
    NotVisible,

    /// <summary>An element whose role is <c>none</c> or <c>presentation</c>.</summary>
    PresentationalRole,

    /// <summary>An element carrying nothing an assistive technology would report.</summary>
    Uninteresting,
}

/// <summary>
/// One name-and-value pair on an accessibility node.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct AxProperty(AxPropertyName Name, AxValue Value)
{
    /// <summary>The protocol spelling of <see cref="Name"/>.</summary>
    internal string ProtocolName => Name switch
    {
        AxPropertyName.Checked => "checked",
        AxPropertyName.Pressed => "pressed",
        AxPropertyName.Expanded => "expanded",
        AxPropertyName.Selected => "selected",
        AxPropertyName.Disabled => "disabled",
        AxPropertyName.Required => "required",
        AxPropertyName.Readonly => "readonly",
        AxPropertyName.Focusable => "focusable",
        AxPropertyName.Focused => "focused",
        AxPropertyName.Hidden => "hidden",
        AxPropertyName.HiddenRoot => "hiddenRoot",
        AxPropertyName.Level => "level",
        AxPropertyName.Multiline => "multiline",
        AxPropertyName.Multiselectable => "multiselectable",
        AxPropertyName.Orientation => "orientation",
        AxPropertyName.Invalid => "invalid",
        AxPropertyName.Autocomplete => "autocomplete",
        AxPropertyName.HasPopup => "hasPopup",
        AxPropertyName.Valuemin => "valuemin",
        AxPropertyName.Valuemax => "valuemax",
        AxPropertyName.Valuetext => "valuetext",
        AxPropertyName.Live => "live",
        AxPropertyName.Atomic => "atomic",
        AxPropertyName.Modal => "modal",
        AxPropertyName.Url => "url",
        AxPropertyName.Busy => "busy",
        AxPropertyName.Editable => "editable",
        AxPropertyName.AriaHiddenElement => "ariaHiddenElement",
        AxPropertyName.AriaHiddenSubtree => "ariaHiddenSubtree",
        AxPropertyName.EmptyAlt => "emptyAlt",
        AxPropertyName.EmptyText => "emptyText",
        AxPropertyName.NotRendered => "notRendered",
        AxPropertyName.NotVisible => "notVisible",
        AxPropertyName.PresentationalRole => "presentationalRole",
        AxPropertyName.Uninteresting => "uninteresting",
        _ => Name.ToString().ToLowerInvariant(),
    };
}
