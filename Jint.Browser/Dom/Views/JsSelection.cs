using Jint.HtmlParser;
using Jint.Browser.Events;
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
/// Nothing renders, so nothing is highlighted: the selection is a place to put a range and read it back —
/// which is what <c>window.getSelection().toString()</c>, the one call a text-extracting agent makes, needs.
/// Moving it fires queued, coalesced <c>selectionchange</c> at the document. Native range
/// subscriptions also observe direct boundary edits and repairs performed by tree mutations.
/// </para>
/// </remarks>
internal sealed class JsSelection : ObjectInstance
{
    private readonly PageRuntime _runtime;
    private DomRange? _range;
    private RangeChangeSubscription? _subscription;
    private Document? _subscriptionDocument;
    private readonly Action _rangeChanged;

    internal JsSelection(PageRuntime runtime, ObjectInstance prototype) : base(runtime.Engine)
    {
        _runtime = runtime;
        _rangeChanged = NativeRangeChanged;
        Prototype = prototype;
    }

    /// <inheritdoc />
    public override string ToString() => _range?.GetText(_runtime.Dom.NativeReadCheckpoint, _runtime.Dom.CancellationToken) ?? "";

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
    internal DomRange? Range
    {
        get => _range;
        set
        {
            SetRange(value);
        }
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-rangecount.</summary>
    internal int RangeCount => _range is null ? 0 : 1;

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-anchornode.</summary>
    internal JsValue AnchorNode => _range is null ? JsValue.Null : _runtime.Dom.WrapIdentity(_range.Start.Container);

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-anchoroffset.</summary>
    internal uint AnchorOffset => _range?.Start.Offset ?? 0;

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-focusnode.</summary>
    internal JsValue FocusNode => _range is null ? JsValue.Null : _runtime.Dom.WrapIdentity(_range.End.Container);

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-focusoffset.</summary>
    internal uint FocusOffset => _range?.End.Offset ?? 0;

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-iscollapsed.</summary>
    internal bool IsCollapsed => _range is null || _range.Collapsed;

    /// <summary>
    /// https://w3c.github.io/selection-api/#dom-selection-type — <c>None</c>, <c>Caret</c> or <c>Range</c>.
    /// </summary>
    internal string SelectionType => _range is null ? "None" : _range.Collapsed ? "Caret" : "Range";

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
        var range = DomBindings.Argument<DomRange>(arguments, 0, "Selection.addRange");

        // "If the selection's range list is not empty, abort these steps": a browser keeps the first range
        // and drops the second rather than replacing it, and a page that meant to replace calls
        // removeAllRanges() first. Nothing changed in that case, so nothing is scheduled either.
        if (_range is null)
        {
            SetRange(range);
        }

        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-removerange.</summary>
    internal JsValue RemoveRange(JsValue[] arguments)
    {
        var range = DomBindings.Argument<DomRange>(arguments, 0, "Selection.removeRange");

        if (ReferenceEquals(_range, range))
        {
            SetRange(null);
        }

        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-removeallranges.</summary>
    internal JsValue RemoveAllRanges()
    {
        if (_range is not null)
        {
            SetRange(null);
        }

        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-collapse, which is also <c>setPosition</c>.</summary>
    internal JsValue Collapse(JsValue[] arguments)
    {
        if (arguments.At(0).IsNull())
        {
            return RemoveAllRanges();
        }

        var node = DomBindings.IdentityArgument(arguments, 0, "Selection.collapse");
        var offset = DomConvert.OptionalUInt32(arguments, 1, 0);

        var range = NewRange("collapse");
        range.SetStart(node, offset);
        range.Collapse(true);
        SetRange(range);
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
        Moved();
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-selectallchildren.</summary>
    internal JsValue SelectAllChildren(JsValue[] arguments)
    {
        var node = DomBindings.IdentityArgument(arguments, 0, "Selection.selectAllChildren");
        var range = NewRange("selectAllChildren");
        range.SelectNodeContents(node);
        SetRange(range);
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-containsnode.</summary>
    internal JsValue ContainsNode(JsValue[] arguments)
    {
        var node = DomBindings.IdentityArgument(arguments, 0, "Selection.containsNode");
        return _range is not null && _range.IntersectsNode(node, _runtime.Dom.NativeReadCheckpoint, _runtime.Dom.CancellationToken) ? JsBoolean.True : JsBoolean.False;
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-selection-deletefromdocument.</summary>
    internal JsValue DeleteFromDocument()
    {
        using var mutation = _runtime.Layout.BeginMutation();
        if (_range is null)
        {
            return JsValue.Undefined;
        }

        // Removing the content collapses the range, which is a boundary point of the selection moving.
        var replacement = new DomProcessingInstructionAttributes.RangeDataReplacement(_range);
        _range.DeleteContents();
        replacement.Complete();
        Moved();
        return JsValue.Undefined;
    }

    private void SetRange(DomRange? range)
    {
        Disconnect();
        _range = range;
        if (range is not null && _runtime.Document is { } document)
        {
            _subscription = range.ObserveChanges(document);
            _subscriptionDocument = document;
            document.PendingRangeChanges = _rangeChanged;
        }
        Moved();
    }

    // The native notification is bookkeeping after all mutation phases. It queues a task;
    // it never runs a listener from within a range or tree mutation.
    private void NativeRangeChanged()
    {
        if (_subscription?.TakePendingChange() == true) Moved();
    }

    internal void Disconnect()
    {
        _subscription?.Dispose();
        _subscription = null;
        if (_subscriptionDocument is { } document && ReferenceEquals(document.PendingRangeChanges, _rangeChanged))
            document.PendingRangeChanges = null;
        _subscriptionDocument = null;
        _range = null;
    }

    /// <summary>
    /// https://w3c.github.io/selection-api/#selectionchange-event — the selection was dissociated from its
    /// range, associated with a new one, or had a boundary point moved, so the document hears about it once
    /// this turn.
    /// </summary>
    private void Moved()
    {
        if (_runtime.Document is { } document)
        {
            SelectionChange.Schedule(_runtime.Dom, document);
        }
    }

    private DomRange NewRange(string member)
    {
        var document = _runtime.Document;

        if (document is null)
        {
            Throw.TypeError(_runtime.Engine._mainRealm, "Failed to execute '" + member + "' on 'Selection': the page has no document.");
        }

        return document!.CreateRange();
    }
}
