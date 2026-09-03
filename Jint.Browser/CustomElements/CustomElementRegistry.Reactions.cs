using AngleSharp.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.CustomElements;

/// <summary>
/// The reaction lane: the element queue, the upgrade algorithm and the callback invocations.
/// </summary>
/// <remarks>
/// <para>
/// <b>The queue is HTML's and the drain points are this package's approximation of <c>[CEReactions]</c>.</b>
/// HTML processes the element queue when the outermost <c>[CEReactions]</c> operation returns to script;
/// nothing here can see a generated member return, so the queue is drained at the moment a reaction
/// <i>arrives</i> instead — which, for everything a script does, is inside the DOM call that caused it and
/// therefore before that call returns. What is deliberately not drained there is a reaction that arrived on
/// the parser thread or while the queue was already draining; those wait for the enclosing drain or for the
/// checkpoint. <c>Jint.Browser/AGENTS.md</c> states the approximation and what it costs.
/// </para>
/// <para>
/// The two-level shape — an element queue of elements, each with its own reaction queue — is the
/// specification's rather than a flat list, and the difference is observable: an element that gains a second
/// reaction while its first is running runs both before the next element's, which a flat queue would
/// interleave.
/// </para>
/// </remarks>
internal sealed partial class CustomElementRegistry
{
    private readonly List<IElement> _elementQueue = [];
    private readonly Action _checkpoint;
    private bool _draining;
    private bool _scheduled;

    /// <summary>The record <paramref name="element"/> already has, or <see langword="null"/>.</summary>
    internal CustomElementRecord? TryGetRecord(IElement element)
        => _records.TryGetValue(element, out var record) ? record : null;

    /// <summary>The record <paramref name="element"/> has, created on first use.</summary>
    internal CustomElementRecord RecordFor(IElement element)
        => _records.GetValue(element, static _ => new CustomElementRecord());

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#concept-try-upgrade — look up a definition
    /// for <paramref name="element"/> and, if there is one, enqueue an upgrade reaction.
    /// </summary>
    internal void TryUpgrade(IElement element)
    {
        if (_byName.Count == 0 || StateOf(element) != CustomElementState.Undefined)
        {
            return;
        }

        if (Lookup(element.NamespaceUri, element.LocalName, IsValueOf(element)) is { } definition)
        {
            EnqueueUpgrade(element, definition);
        }
    }

    /// <summary>Tries to upgrade every element of <paramref name="root"/>'s subtree, in tree order.</summary>
    internal void UpgradeSubtree(INode root)
    {
        if (_byName.Count == 0)
        {
            return;
        }

        Walk(root, TryUpgrade);
    }

    /// <summary>
    /// The parse boundary: everything the tokenizer added since the last one becomes custom here.
    /// </summary>
    /// <remarks>
    /// AngleSharp creates a parser element with no notification this package can hook, so an element written
    /// in the markup is <i>undefined</i> until the next moment the driver owns — before each script it runs,
    /// and once when the parse ends. It costs one tree walk, and only for a document that has defined
    /// something.
    /// </remarks>
    internal void UpgradeParsedElements()
    {
        if (_byName.Count == 0 || _runtime.Document is not { } document)
        {
            return;
        }

        EnsureWatching(document);
        Walk(document, TryUpgrade);
        Drain();
    }

    /// <summary>Adds a reaction to <paramref name="element"/>'s queue and puts it on the element queue.</summary>
    private void Enqueue(IElement element, CustomElementRecord record, in CustomElementReaction reaction)
    {
        record.Reactions.Enqueue(reaction);

        if (record.Queued)
        {
            return;
        }

        record.Queued = true;
        _elementQueue.Add(element);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#enqueue-a-custom-element-upgrade-reaction.
    /// </summary>
    private void EnqueueUpgrade(IElement element, CustomElementDefinition definition)
        => Enqueue(element, RecordFor(element), new CustomElementReaction(CustomElementReactionKind.Upgrade, definition, null, null, null));

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#enqueue-a-custom-element-callback-reaction,
    /// which is a no-op unless the element is custom and the definition has the callback.
    /// </summary>
    private void EnqueueCallback(IElement element, CustomElementRecord record, CustomElementReactionKind kind, string? name = null, string? oldValue = null, string? newValue = null)
    {
        if (record.State != CustomElementState.Custom || record.Definition is not { } definition)
        {
            return;
        }

        var callback = kind switch
        {
            CustomElementReactionKind.Connected => definition.ConnectedCallback,
            CustomElementReactionKind.Disconnected => definition.DisconnectedCallback,
            CustomElementReactionKind.Adopted => definition.AdoptedCallback,
            _ => definition.AttributeChangedCallback,
        };

        if (callback is null)
        {
            return;
        }

        if (kind == CustomElementReactionKind.AttributeChanged && !definition.Observes(name!))
        {
            return;
        }

        Enqueue(element, record, new CustomElementReaction(kind, definition, name, oldValue, newValue));
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#invoke-custom-element-reactions, for the
    /// whole element queue.
    /// </summary>
    /// <remarks>
    /// A reaction that arrives from another thread — the parser's — or while this is already running is left
    /// on the queue; the enclosing drain picks it up, and if there is none, the checkpoint job does. The
    /// queue itself needs no lock for that: the parser baton parks one holder while the other works, so the
    /// parser thread and the loop are never both inside this class, which is the same property
    /// <c>Observers/MutationObserverLane</c> rests on.
    /// </remarks>
    internal void Drain()
    {
        if (_elementQueue.Count == 0)
        {
            return;
        }

        if (_draining || Environment.CurrentManagedThreadId != _runtime.LoopThreadId)
        {
            Schedule();
            return;
        }

        _draining = true;

        try
        {
            while (_elementQueue.Count > 0)
            {
                var element = _elementQueue[0];
                _elementQueue.RemoveAt(0);

                if (!_records.TryGetValue(element, out var record))
                {
                    continue;
                }

                // Queued stays set for the whole of this element's own queue, so a reaction the callbacks add
                // for the same element joins the loop below rather than putting it on the queue a second time.
                while (record.Reactions.Count > 0)
                {
                    Invoke(element, record.Reactions.Dequeue());
                }

                record.Queued = false;
            }
        }
        finally
        {
            _draining = false;
        }
    }

    /// <summary>Puts one drain on the engine's job queue, at most one at a time.</summary>
    private void Schedule()
    {
        if (_scheduled)
        {
            return;
        }

        _scheduled = true;
        _runtime.Engine.AddToEventLoop(_checkpoint);
    }

    private void RunCheckpoint()
    {
        _scheduled = false;
        Drain();
    }

    private void Invoke(IElement element, in CustomElementReaction reaction)
    {
        if (reaction.Kind == CustomElementReactionKind.Upgrade)
        {
            Upgrade(element, reaction.Definition);
            return;
        }

        var callback = reaction.Kind switch
        {
            CustomElementReactionKind.Connected => reaction.Definition.ConnectedCallback,
            CustomElementReactionKind.Disconnected => reaction.Definition.DisconnectedCallback,
            CustomElementReactionKind.Adopted => reaction.Definition.AdoptedCallback,
            _ => reaction.Definition.AttributeChangedCallback,
        };

        if (callback is null)
        {
            return;
        }

        var wrapper = _runtime.Dom.WrapNode(element);

        JsValue[] arguments = reaction.Kind == CustomElementReactionKind.AttributeChanged
            ?
            [
                JsString.Create(reaction.Name!),
                reaction.OldValue is null ? JsValue.Null : JsString.Create(reaction.OldValue),
                reaction.NewValue is null ? JsValue.Null : JsString.Create(reaction.NewValue),
                JsValue.Null,
            ]
            : [];

        try
        {
            callback.Call(wrapper, arguments);
        }
        catch (JavaScriptException exception)
        {
            Report(exception, reaction.Definition.Name);
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#concept-upgrade-an-element.
    /// </summary>
    /// <remarks>
    /// The two enqueues before the construction are the specification's order and they are observable: the
    /// constructor runs first, then one <c>attributeChangedCallback</c> per observed attribute already on the
    /// element, then <c>connectedCallback</c> — because those reactions go on the element's own queue, which
    /// the drain that is running this upgrade continues into.
    /// </remarks>
    internal void Upgrade(IElement element, CustomElementDefinition definition)
    {
        var record = RecordFor(element);

        if (record.State is not (CustomElementState.Undefined or CustomElementState.Uncustomized))
        {
            return;
        }

        record.Definition = definition;
        record.State = CustomElementState.Failed;
        record.FormAssociated = definition.FormAssociated;
        Snapshot(element, record, definition);

        if (definition.AttributeChangedCallback is not null)
        {
            foreach (var attribute in element.Attributes)
            {
                if (definition.Observes(attribute.LocalName))
                {
                    Enqueue(element, record, new CustomElementReaction(
                        CustomElementReactionKind.AttributeChanged,
                        definition,
                        attribute.LocalName,
                        null,
                        attribute.Value));
                }
            }
        }

        if (definition.ConnectedCallback is not null && IsConnected(element))
        {
            Enqueue(element, record, new CustomElementReaction(CustomElementReactionKind.Connected, definition, null, null, null));
        }

        // The wrapper is built now rather than by the constructor, because it is what `super()` hands back:
        // the construction stack holds the element being upgraded, and the base constructor returns it.
        var wrapper = _runtime.Dom.WrapNode(element);
        definition.ConstructionStack.Add(wrapper);

        try
        {
            if (definition.DisableShadow && element.ShadowRoot is not null)
            {
                var engine = _runtime.Engine;
                var error = engine._mainRealm.Intrinsics.DomException.CreateException(
                    DomExceptionNames.NotSupported,
                    "The custom element '" + definition.Name + "' disabled shadow, so an element that already has a shadow root cannot be upgraded.");
                var location = engine._lastSyntaxElement?.Location ?? default;
                Throw.JavaScriptException(engine, error, in location);
            }

            record.State = CustomElementState.Precustomized;

            var constructed = _runtime.Engine.Construct(definition.Constructor, [], definition.Constructor, null);

            if (!ReferenceEquals(constructed, wrapper))
            {
                Throw.TypeError(
                    _runtime.Engine._mainRealm,
                    "The custom element constructor for '" + definition.Name + "' did not produce the element being upgraded.");
            }
        }
        catch (JavaScriptException exception)
        {
            // "If any of these steps threw exception: set element's custom element definition to null, empty
            // element's custom element reaction queue, and report the exception." The element stays failed,
            // which is what stops a second attempt from running the constructor again.
            record.Definition = null;
            record.Reactions.Clear();
            record.State = CustomElementState.Failed;
            Report(exception, definition.Name);
            return;
        }
        finally
        {
            definition.ConstructionStack.RemoveAt(definition.ConstructionStack.Count - 1);
        }

        record.State = CustomElementState.Custom;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#report-the-exception — the <c>error</c> event
    /// at the global scope first, then the page's own recorder, which is the order the parser driver uses for
    /// a script that threw.
    /// </summary>
    private void Report(JavaScriptException exception, string name)
    {
        try
        {
            _runtime.Engine._webApi?.FireGlobalErrorEvent(exception);
        }
        catch (JavaScriptException)
        {
            // A page whose own error handler throws is not a reason to lose the original report.
        }

        _runtime.Recorder.Add(new PageError(
            PageErrorKind.UncaughtCallbackError,
            PageRecorder.Diagnostics.Describe(exception.Error, exception),
            "custom element '" + name + "'"));
    }

    /// <summary>Remembers the current value of every observed attribute, so the next change has an old one.</summary>
    private static void Snapshot(IElement element, CustomElementRecord record, CustomElementDefinition definition)
    {
        if (definition.ObservedAttributes.Length == 0)
        {
            return;
        }

        var values = record.Attributes ??= new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var observed in definition.ObservedAttributes)
        {
            values[observed] = element.GetAttribute(observed);
        }
    }

    /// <summary>DOM's custom element state for an element that has no record of its own yet.</summary>
    internal CustomElementState StateOf(IElement element)
    {
        if (_records.TryGetValue(element, out var record))
        {
            return record.State;
        }

        return IsPotentiallyCustom(element) ? CustomElementState.Undefined : CustomElementState.Uncustomized;
    }

    /// <summary>
    /// Whether an element could ever become custom: an HTML element whose local name is a valid custom
    /// element name, or one carrying an <c>is</c>.
    /// </summary>
    private static bool IsPotentiallyCustom(IElement element)
        => string.Equals(element.NamespaceUri, HtmlNamespace, StringComparison.Ordinal)
        && (CustomElementNames.IsValid(element.LocalName) || element.HasAttribute("is"));

    /// <summary>
    /// The element's <c>is</c> value: the one creation recorded, and otherwise the content attribute — which
    /// is what a parser-created element has, there being no hook on the parser's element creation.
    /// </summary>
    private string? IsValueOf(IElement element)
    {
        if (_records.TryGetValue(element, out var record) && record.IsValue is { } recorded)
        {
            return recorded;
        }

        var attribute = element.GetAttribute("is");
        return string.IsNullOrEmpty(attribute) ? null : attribute;
    }

    /// <summary>Whether <paramref name="element"/> would be one of <paramref name="definition"/>'s candidates.</summary>
    private bool Matches(IElement element, CustomElementDefinition definition)
        => string.Equals(element.NamespaceUri, HtmlNamespace, StringComparison.Ordinal)
        && string.Equals(element.LocalName, definition.LocalName, StringComparison.Ordinal)
        && StateOf(element) == CustomElementState.Undefined
        && (definition.IsAutonomous || string.Equals(IsValueOf(element), definition.Name, StringComparison.Ordinal));

    /// <summary>https://dom.spec.whatwg.org/#connected, shadow-including.</summary>
    private static bool IsConnected(INode node)
    {
        for (INode? current = node; current is not null;)
        {
            if (current is IDocument)
            {
                return true;
            }

            current = current is IShadowRoot shadow ? shadow.Host : current.Parent;
        }

        return false;
    }

    /// <summary>Every element of <paramref name="root"/>'s inclusive subtree, in tree order.</summary>
    /// <remarks>
    /// An explicit stack rather than recursion, because the depth is a stranger's document's; the children are
    /// pushed in reverse so that popping gives tree order. Nothing it calls runs script — a walk only ever
    /// enqueues — so the tree cannot change under it.
    /// </remarks>
    private static void Walk(INode root, Action<IElement> visit)
    {
        var pending = new Stack<INode>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var node = pending.Pop();

            if (node is IElement element)
            {
                visit(element);
            }

            var children = node.ChildNodes;
            for (var i = children.Length - 1; i >= 0; i--)
            {
                pending.Push(children[i]);
            }
        }
    }
}
