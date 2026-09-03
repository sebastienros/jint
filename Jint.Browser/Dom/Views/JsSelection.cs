using AngleSharp.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// The document's <c>Selection</c>: at most one range, and no user to move it.
/// </summary>
/// <remarks>
/// <para>
/// <a href="https://w3c.github.io/selection-api/">Selection API</a> models a selection as a list of ranges
/// that every browser caps at one, and this caps it at one too. What it cannot have is a <em>direction</em>:
/// direction comes from which end the user dragged from, and there is no user, so the anchor is always the
/// range's start and the focus always its end. A page that calls <c>extend</c> backwards and then reads
/// <c>anchorNode</c> gets the earlier boundary where a browser gives it the later one.
/// </para>
/// <para>
/// Nothing renders, so nothing is highlighted and no <c>selectionchange</c> event is fired: the selection is
/// a place to put a range and read it back — which is what <c>window.getSelection().toString()</c>, the one
/// call a text-extracting agent makes, needs.
/// </para>
/// </remarks>
internal sealed class JsSelection : ObjectInstance
{
    private readonly PageRuntime _runtime;
    private IRange? _range;

    internal JsSelection(PageRuntime runtime, ObjectInstance prototype) : base(runtime.Engine)
    {
        _runtime = runtime;
        Prototype = prototype;
    }

    /// <inheritdoc />
    public override string ToString() => _range?.ToString() ?? "";

    /// <summary>The receiver check every member of the interface starts with.</summary>
    internal static JsSelection Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsSelection selection)
        {
            return selection;
        }

        var message = "Failed to execute '" + member + "' on 'Selection': Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    /// <summary>
    /// The one range, for the editor rather than for script.
    /// </summary>
    /// <remarks>
    /// <c>contenteditable</c> keeps its caret here (<c>Events/ContentEditing</c>) rather than in a second
    /// selection of its own, so a page that types into an editing host and then reads
    /// <c>getSelection().focusOffset</c> is told where the next character will go.
    /// </remarks>
    internal IRange? Range
    {
        get => _range;
        set => _range = value;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-rangecount.</summary>
    internal int RangeCount => _range is null ? 0 : 1;

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-anchornode.</summary>
    internal JsValue AnchorNode => _range is null ? JsValue.Null : _runtime.Dom.WrapNodeValue(_range.Head);

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-anchoroffset.</summary>
    internal int AnchorOffset => _range?.Start ?? 0;

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-focusnode.</summary>
    internal JsValue FocusNode => _range is null ? JsValue.Null : _runtime.Dom.WrapNodeValue(_range.Tail);

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-focusoffset.</summary>
    internal int FocusOffset => _range?.End ?? 0;

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-iscollapsed.</summary>
    internal bool IsCollapsed => _range is null || _range.IsCollapsed;

    /// <summary>
    /// https://w3c.github.io/selection-api/#dom-selection-type — <c>None</c>, <c>Caret</c> or <c>Range</c>.
    /// </summary>
    internal string SelectionType => _range is null ? "None" : _range.IsCollapsed ? "Caret" : "Range";

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-getrangeat.</summary>
    internal JsValue GetRangeAt(JsValue[] arguments)
    {
        var index = DomConvert.RequiredInt32(arguments, 0, "Selection.getRangeAt");

        if (_range is null || index != 0)
        {
            Throw.RangeError(_runtime.Engine._mainRealm, "Failed to execute 'getRangeAt' on 'Selection': " + index + " is not a valid index.");
        }

        return _runtime.Dom.Wrap(_range!);
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-addrange — the second range is ignored.</summary>
    internal JsValue AddRange(JsValue[] arguments)
    {
        var range = DomBindings.Argument<IRange>(arguments, 0, "Selection.addRange");

        // "If the selection's range list is not empty, abort these steps": a browser keeps the first range
        // and drops the second rather than replacing it, and a page that meant to replace calls
        // removeAllRanges() first.
        _range ??= range;
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-removerange.</summary>
    internal JsValue RemoveRange(JsValue[] arguments)
    {
        var range = DomBindings.Argument<IRange>(arguments, 0, "Selection.removeRange");

        if (ReferenceEquals(_range, range))
        {
            _range = null;
        }

        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-removeallranges.</summary>
    internal JsValue RemoveAllRanges()
    {
        _range = null;
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-collapse, which is also <c>setPosition</c>.</summary>
    internal JsValue Collapse(JsValue[] arguments)
    {
        if (arguments.At(0).IsNull())
        {
            return RemoveAllRanges();
        }

        var node = DomBindings.Argument<INode>(arguments, 0, "Selection.collapse");
        var offset = DomConvert.OptionalInt32(arguments, 1, 0);

        var range = NewRange("collapse");
        range.StartWith(node, offset);
        range.Collapse(true);
        _range = range;
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-collapsetostart.</summary>
    internal JsValue CollapseTo(bool toStart, string member)
    {
        if (_range is null)
        {
            Throw.TypeError(_runtime.Engine._mainRealm, "Failed to execute '" + member + "' on 'Selection': There is no selection.");
        }

        _range!.Collapse(toStart);
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-selectallchildren.</summary>
    internal JsValue SelectAllChildren(JsValue[] arguments)
    {
        var node = DomBindings.Argument<INode>(arguments, 0, "Selection.selectAllChildren");
        var range = NewRange("selectAllChildren");
        range.SelectContent(node);
        _range = range;
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-containsnode.</summary>
    internal JsValue ContainsNode(JsValue[] arguments)
    {
        var node = DomBindings.Argument<INode>(arguments, 0, "Selection.containsNode");
        return _range is not null && _range.Intersects(node) ? JsBoolean.True : JsBoolean.False;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-deletefromdocument.</summary>
    internal JsValue DeleteFromDocument()
    {
        _range?.ClearContent();
        return JsValue.Undefined;
    }

    private IRange NewRange(string member)
    {
        var document = _runtime.Document;

        if (document is null)
        {
            Throw.TypeError(_runtime.Engine._mainRealm, "Failed to execute '" + member + "' on 'Selection': the page has no document.");
        }

        return document!.CreateRange();
    }
}
