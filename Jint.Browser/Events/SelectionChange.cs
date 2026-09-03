using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using Jint.Browser.Dom;

namespace Jint.Browser.Events;

/// <summary>
/// The <c>selectionchange</c> event: one task per target per turn, at the document or at the text control
/// whose selection moved.
/// <para>
/// https://w3c.github.io/selection-api/#selectionchange-event
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>It is queued and coalesced, and both halves are the specification's.</b> "Scheduling a selectionchange
/// event" sets a per-target <i>has scheduled selectionchange event</i> flag and does nothing if it was
/// already set, then queues a task; firing it clears the flag. So a script that moves the caret ten times in
/// one turn is heard once, after its own code returns — which is what makes a listener that reads the
/// selection back see where it ended up rather than each step on the way. The queue is the engine's own, the
/// way the <c>toggle</c> event's is, so a page loop's <c>ProcessTasks</c> delivers it and
/// <c>Page.WaitForIdleAsync</c> waits for it.
/// </para>
/// <para>
/// <b>The target decides whether it bubbles.</b> A document's selection change is fired at the document and
/// does not bubble; a text control's is fired at the <i>element</i> and does, which is what lets one
/// <c>document.addEventListener("selectionchange", …)</c> — the way every editor library writes it, React's
/// <c>onSelect</c> included — hear a caret moving inside an <c>&lt;input&gt;</c>.
/// </para>
/// <para>
/// <b>What is deliberately not covered.</b> A script that takes the range out of <c>getSelection()</c> and
/// mutates <i>that</i> object moves the selection's boundary points without going through any member of
/// <c>Selection</c>, and the specification says a change made that way schedules the event too; there is no
/// hook on AngleSharp's <c>IRange</c> to make it do so, and putting one there would be re-implementing a
/// DOM this package does not own. Every path a page normally takes — the <c>Selection</c> members, the
/// editor's own caret moves, and <c>contenteditable</c> — goes through this file.
/// </para>
/// </remarks>
internal static class SelectionChange
{
    /// <summary>
    /// https://w3c.github.io/selection-api/#has-scheduled-selectionchange-event — one flag per document and
    /// per text control, keyed on the AngleSharp node exactly as the wrapper cache is.
    /// </summary>
    private static readonly ConditionalWeakTable<INode, Pending> _pending = new();

    /// <summary>
    /// https://w3c.github.io/selection-api/#scheduling-a-selectionchange-event, for a target whose selection
    /// has just moved.
    /// </summary>
    /// <param name="dom">The realm the event is created in.</param>
    /// <param name="target">The document, or the text control whose own selection moved.</param>
    internal static void Schedule(DomRealm dom, INode target)
    {
        var pending = _pending.GetOrCreateValue(target);

        if (pending.Scheduled)
        {
            return;
        }

        pending.Scheduled = true;

        // Wrapped now rather than in the task, so the event is fired at the wrapper a page has already added
        // its listener to and not at a second one minted after the fact.
        var wrapper = dom.WrapNode(target);
        var bubbles = target is IElement;

        dom.Engine.Tasks.Post(() =>
        {
            pending.Scheduled = false;
            ActivationBehaviors.Fire(wrapper, "selectionchange", bubbles, composed: false);
        });
    }

    /// <summary>Whether a target already has an unfired <c>selectionchange</c> task.</summary>
    private sealed class Pending
    {
        internal bool Scheduled { get; set; }
    }
}
