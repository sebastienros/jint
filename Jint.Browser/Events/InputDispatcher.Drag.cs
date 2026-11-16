using Jint.Browser.Dom;
using Jint.Browser.Dom.Files;
using Jint.HtmlParser;
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// One drag-and-drop operation the mouse started, from the press on a draggable element to the release.
/// </summary>
/// <param name="Source">The source node: the draggable element the press was on.</param>
/// <param name="StartX">Where the press was, which the pointer has to move away from to start the drag.</param>
/// <param name="StartY">Where the press was.</param>
internal sealed record DragOperation(Element Source, double StartX, double StartY)
{
    /// <summary>Whether <c>dragstart</c> fired and was not cancelled: the operation is under way.</summary>
    internal bool Started { get; set; }

    /// <summary>The drag data store every event of the operation carries.</summary>
    internal JsDataTransfer? DataTransfer { get; set; }

    /// <summary>The current target element: the one the last <c>dragenter</c> and <c>dragover</c> went to.</summary>
    internal Element? CurrentTarget { get; set; }

    /// <summary>Whether the last <c>dragover</c> was cancelled, which is what makes the current target a drop target.</summary>
    internal bool Accepted { get; set; }
}

/// <summary>
/// The drag-and-drop processing model a mouse drives,
/// https://html.spec.whatwg.org/multipage/dnd.html#drag-and-drop-processing-model.
/// </summary>
/// <remarks>
/// <para>
/// A press on a draggable element — <c>draggable="true"</c>, or a link or an image that has not opted out —
/// arms an operation; moving the pointer far enough with the button held starts it with <c>dragstart</c>,
/// and from then on the mouse produces drag events instead of mouse events: every move is a <c>drag</c> at
/// the source, a <c>dragenter</c> and a <c>dragleave</c> when the element under the pointer changes, and a
/// <c>dragover</c> at it; the release is a <c>drop</c> where the last <c>dragover</c> was cancelled, then a
/// <c>dragend</c> at the source. No <c>mouseup</c> and no <c>click</c> follow a drag, as in every browser.
/// </para>
/// <para>
/// <b>The target is the element under the pointer</b>, whether or not its <c>dragenter</c> was cancelled:
/// what Blink and Gecko do, and what a list sorting its items on <c>dragover</c> — SortableJS — needs. The
/// standard's text would send a <c>dragover</c> nobody cancelled to the body. The drag data store is not
/// switched between its read/write, protected and read-only modes: every event can read and write it.
/// </para>
/// </remarks>
internal static partial class InputDispatcher
{
    /// <summary>How far the pointer moves from the press before the drag starts, in CSS pixels; Blink's threshold.</summary>
    private const double DragThreshold = 4;

    /// <summary>
    /// The draggable element a press on <paramref name="hit"/> would drag, or <see langword="null"/>:
    /// https://html.spec.whatwg.org/multipage/dnd.html#the-draggable-attribute.
    /// </summary>
    private static Element? DraggableFrom(Element hit)
    {
        for (var element = hit; element is not null; element = element.ParentNode as Element)
        {
            if (element.NamespaceUri != Namespaces.Html)
            {
                continue;
            }

            var draggable = element.GetAttribute("draggable");
            if (string.Equals(draggable, "true", StringComparison.OrdinalIgnoreCase))
            {
                return element;
            }

            if (string.Equals(draggable, "false", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // The auto state: an img, and an a with an href, are draggable by default.
            if (element.LocalName == "img" || (element.LocalName == "a" && element.GetAttribute("href") is not null))
            {
                return element;
            }
        }

        return null;
    }

    /// <summary>A press arms a drag on the draggable element under it; nothing is dispatched yet.</summary>
    private static void ArmDrag(BrowserEventRealm events, Element hit, in MouseInput input)
    {
        events.Drag = input.Button == PrimaryButton && DraggableFrom(hit) is { } source
            ? new DragOperation(source, input.X, input.Y)
            : null;
    }

    /// <summary>
    /// A move while a drag is armed or under way: starts it once the pointer has moved far enough, then runs
    /// one iteration of the processing model. Answers whether the move belonged to the drag, in which case no
    /// mouse event is dispatched for it.
    /// </summary>
    private static bool MoveDrag(DomRealm dom, BrowserEventRealm events, Element hit, in MouseInput input, in ClickOptions options)
    {
        if (events.Drag is not { } drag)
        {
            return false;
        }

        if (((int) input.Buttons & 1) == 0 || !Connected(drag.Source))
        {
            events.Drag = null;
            return false;
        }

        if (!drag.Started)
        {
            var dx = input.X - drag.StartX;
            var dy = input.Y - drag.StartY;
            if ((dx * dx) + (dy * dy) < DragThreshold * DragThreshold)
            {
                return false;
            }

            // https://html.spec.whatwg.org/multipage/dnd.html#initiate-the-drag-and-drop-operation
            drag.DataTransfer = FileTransferRealm.Of(dom.Engine).CreateDataTransfer();
            drag.DataTransfer.InitializeForDrag();
            if (!FireDrag(dom, drag, drag.Source, "dragstart", options, cancelable: true))
            {
                events.Drag = null;
                return false;
            }

            drag.Started = true;

            // The press that started the drag gets no click, whatever the release hits.
            events.MousePressTarget = null;
        }

        // https://html.spec.whatwg.org/multipage/dnd.html#drag-and-drop-processing-model, one iteration.
        if (!FireDrag(dom, drag, drag.Source, "drag", options, cancelable: true))
        {
            EndDrag(dom, events, drag, options, dropped: false);
            return true;
        }

        if (!ReferenceEquals(hit, drag.CurrentTarget))
        {
            var previous = drag.CurrentTarget;
            drag.DataTransfer!.PrepareDropEffect();
            FireDrag(dom, drag, hit, "dragenter", options, cancelable: true);
            drag.CurrentTarget = hit;
            if (previous is not null && Connected(previous))
            {
                FireDrag(dom, drag, previous, "dragleave", options, cancelable: false);
            }
        }

        drag.DataTransfer!.PrepareDropEffect();
        drag.Accepted = !FireDrag(dom, drag, hit, "dragover", options, cancelable: true);
        if (!drag.Accepted)
        {
            drag.DataTransfer.ResetDropEffect();
        }

        return true;
    }

    /// <summary>
    /// The release while a drag is under way: a <c>drop</c> where the last <c>dragover</c> accepted one, a
    /// <c>dragleave</c> where it did not, then <c>dragend</c>. Answers whether the release ended a drag.
    /// </summary>
    private static bool ReleaseDrag(DomRealm dom, BrowserEventRealm events, in ClickOptions options)
    {
        if (events.Drag is not { Started: true } drag)
        {
            events.Drag = null;
            return false;
        }

        var dropped = false;
        if (drag.CurrentTarget is { } target && Connected(target))
        {
            if (drag.Accepted && drag.DataTransfer!.DropEffect != "none")
            {
                FireDrag(dom, drag, target, "drop", options, cancelable: true);
                dropped = true;
            }
            else
            {
                FireDrag(dom, drag, target, "dragleave", options, cancelable: false);
            }
        }

        EndDrag(dom, events, drag, options, dropped);
        return true;
    }

    private static void EndDrag(DomRealm dom, BrowserEventRealm events, DragOperation drag, in ClickOptions options, bool dropped)
    {
        events.Drag = null;
        events.MousePressTarget = null;
        if (!dropped)
        {
            drag.DataTransfer?.ResetDropEffect();
        }

        if (Connected(drag.Source))
        {
            FireDrag(dom, drag, drag.Source, "dragend", options with { Buttons = 0 }, cancelable: false);
        }
    }

    /// <summary>Whether <paramref name="node"/> is still in its document, which a listener may have changed.</summary>
    private static bool Connected(Node node)
    {
        for (Node? current = node; current is not null; current = current.ParentNode ?? (current as ShadowRoot)?.Host)
        {
            if (current is Document)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Fires one trusted <c>DragEvent</c>, answering whether nothing cancelled it.</summary>
    private static bool FireDrag(DomRealm dom, DragOperation drag, Element target, string type, in ClickOptions options, bool cancelable)
    {
        var engine = dom.Engine;
        var realm = BrowserEventRealm.Of(engine);
        var wrapper = dom.WrapNode(target);
        var ev = new JsDragEvent(
            engine,
            JsString.Create(type),
            new EventInit(Bubbles: true, cancelable, Composed: true),
            realm.TimeStamp,
            engine._mainRealm.GlobalObject,
            detail: 0,
            which: null,
            MouseState(options),
            (JsValue?) drag.DataTransfer ?? JsValue.Null);

        ev._prototype = realm.PrototypeOf(BrowserEventInterfaces.DragEvent);
        ev.IsTrusted = true;
        return wrapper.DispatchEvent(ev);
    }
}
