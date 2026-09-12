using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Dom;

/// <summary>
/// The <c>select</c>-level state HTML defines and AngleSharp's surface does not state: a select's display
/// size, whether it is therefore being rendered as a drop-down box, which of its options are disabled, and
/// the selectedness setting algorithm an option asks it to run.
/// </summary>
/// <remarks>
/// Every one of these is a rule over an element's content attributes rather than over anything AngleSharp
/// computes, which is why they are here and not worked around at each call site: <c>:open</c> and the
/// <c>selected</c> IDL setter ask the same question about the display size, and <c>:disabled</c> and the
/// algorithm's first step ask the same question about an option.
/// </remarks>
internal static class HtmlSelectState
{
    private const string Disabled = "disabled";
    private const string Size = "size";

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/form-elements.html#ask-for-a-reset">Ask for a
    /// reset</a>: "if an <c>option</c> element in the list of options asks for a reset, then run that
    /// <c>select</c> element's selectedness setting algorithm". An option in no select at all asks nobody.
    /// </summary>
    internal static void AskForAReset(IHtmlOptionElement option)
    {
        if (NearestAncestorSelect(option) is { } select)
        {
            SetSelectedness(select, option);
        }
    }

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/form-elements.html#selectedness-setting-algorithm">The
    /// selectedness setting algorithm</a>, whose two steps are gated differently and deliberately so: the
    /// first — select the first option that is not disabled when none is selected — asks for a display size
    /// of 1, while the second — leave exactly one option selected — asks only that the <c>multiple</c>
    /// attribute is absent. So a <c>&lt;select size=4&gt;</c> still holds one selection at a time, and one
    /// with nothing selected stays that way.
    /// </summary>
    /// <param name="select">The select whose options are being reset.</param>
    /// <param name="asking">
    /// The option that asked for the reset, when one did. HTML's step 2 says "all but the <i>last</i> option
    /// element with its selectedness set to true in tree order", and taken literally that would make
    /// <c>options[0].selected = true</c> a no-op while a later option is selected — which no browser does and
    /// which upstream's own
    /// <a href="https://github.com/web-platform-tests/wpt/blob/master/html/semantics/forms/the-select-element/select-ask-for-reset.html"><c>select-ask-for-reset.html</c></a>
    /// asserts against: it selects every option in turn from last to first and expects each write to win. So
    /// the option that asked is the one kept, and tree order decides only when nobody asked — which is the
    /// entry point the "last wins" wording is really about, a form reset restoring markup that carries
    /// <c>selected</c> more than once.
    /// </param>
    private static void SetSelectedness(IHtmlSelectElement select, IHtmlOptionElement? asking)
    {
        if (select.IsMultiple)
        {
            return;
        }

        // The list of options is AngleSharp's, which is what select.selectedIndex, select.value and
        // select.selectedOptions all read: the algorithm has to agree with them about which options it is
        // over, and a second traversal here would be a second answer to that question.
        var options = select.Options;
        var count = options.Length;
        var selected = 0;
        var lastSelected = -1;
        var askingIndex = -1;

        for (var i = 0; i < count; i++)
        {
            if (!options[i].IsSelected)
            {
                continue;
            }

            selected++;
            lastSelected = i;

            if (ReferenceEquals(options[i], asking))
            {
                askingIndex = i;
            }
        }

        if (selected == 0)
        {
            if (!IsADropDownBox(select))
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                if (!IsADisabledOption(options[i]))
                {
                    options[i].IsSelected = true;
                    return;
                }
            }

            return;
        }

        if (selected == 1)
        {
            return;
        }

        var keep = askingIndex >= 0 ? askingIndex : lastSelected;
        for (var i = 0; i < count; i++)
        {
            if (i != keep)
            {
                options[i].IsSelected = false;
            }
        }
    }

    /// <summary>
    /// HTML §4.10.7: a <c>select</c> is
    /// <a href="https://html.spec.whatwg.org/multipage/form-elements.html#concept-select-size">being rendered
    /// as a drop-down box</a> when it has no <c>multiple</c> attribute and its display size is 1.
    /// </summary>
    internal static bool IsADropDownBox(IElement element)
        => element is IHtmlSelectElement select && !select.IsMultiple && DisplaySizeOf(select) == 1;

    /// <summary>
    /// The <a href="https://html.spec.whatwg.org/multipage/form-elements.html#concept-select-size">display
    /// size</a>: the <c>size</c> attribute under HTML's rules for parsing non-negative integers, or, when
    /// there is none or it does not parse, 4 with <c>multiple</c> and 1 without.
    /// </summary>
    /// <remarks>
    /// This is not the <c>size</c> IDL attribute, whose missing value default is 0, and it is not
    /// AngleSharp's <c>Size</c> either, which answers 0 for an absent attribute and reads the value with
    /// .NET's own parse — so the attribute is read here, through the one implementation of HTML's rules the
    /// package has.
    /// </remarks>
    private static int DisplaySizeOf(IHtmlSelectElement select)
    {
        var value = select.GetAttribute(Size);
        if (value is not null && ReflectedAttribute.TryParseNonNegative(value, out var size))
        {
            return (int) Math.Min(size, int.MaxValue);
        }

        return select.IsMultiple ? 4 : 1;
    }

    /// <summary>
    /// HTML §4.10.10: an <c>option</c>
    /// <a href="https://html.spec.whatwg.org/multipage/form-elements.html#concept-option-disabled">is
    /// disabled</a> if its own <c>disabled</c> attribute is present, or if it is a <i>child</i> of an
    /// <c>optgroup</c> whose <c>disabled</c> attribute is present. The select's own disabled state is not
    /// part of it — that is §4.15's <c>:disabled</c>, which is this rule plus a clause.
    /// </summary>
    internal static bool IsADisabledOption(IElement option)
        => option.HasAttribute(Disabled)
            || (option.ParentElement is IHtmlOptionsGroupElement group && group.HasAttribute(Disabled));

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/form-elements.html#get-the-nearest-ancestor-select">Get
    /// the nearest ancestor select</a>: the upward spelling of the list-of-options walk, so the two agree by
    /// construction about which options a select owns. A <c>datalist</c>, an <c>hr</c>, an <c>option</c> or a
    /// second <c>optgroup</c> on the way up ends the search, because the downward walk does not descend past
    /// any of them.
    /// </summary>
    private static IHtmlSelectElement? NearestAncestorSelect(IElement option)
    {
        IElement? optgroup = null;

        for (var ancestor = option.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
        {
            if (ancestor is IHtmlDataListElement or IHtmlHrElement or IHtmlOptionElement)
            {
                return null;
            }

            if (ancestor is IHtmlOptionsGroupElement)
            {
                if (optgroup is not null)
                {
                    return null;
                }

                optgroup = ancestor;
            }

            if (ancestor is IHtmlSelectElement select)
            {
                return select;
            }
        }

        return null;
    }
}
