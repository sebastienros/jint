#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.WebApi;
using Jint.WebApi.Events;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>EventTarget</c> dispatch over a tree — https://dom.spec.whatwg.org/#concept-event-dispatch — driven
/// through the virtual seams a DOM overrides.
/// </summary>
/// <remarks>
/// <para>
/// The engine ships no tree, so the fixture below is one: a target that answers <c>IsNode</c>, a parent
/// pointer, shadow roots with a mode, slot assignment and activation behaviour with a recorder. It is
/// deliberately the <i>minimum</i> a DOM has to supply, because that is half of what these tests assert —
/// that the seams are enough, and that a host writes no dispatch code of its own.
/// </para>
/// <para>
/// Every tree-less target keeps the reduction it always had, which
/// <see cref="ATreeLessDispatchBuildsNoPath"/> pins structurally rather than by timing: a flat dispatch must
/// leave the event's path list unallocated.
/// </para>
/// </remarks>
public class TreeDispatchTests
{
    private sealed class RecordingSink : DiagnosticsSink
    {
        internal List<DiagnosticEvent> Reports { get; } = new();

        public override void Report(DiagnosticEvent report) => Reports.Add(report);
    }

    private enum TreeKind
    {
        Node,
        Slot,
        OpenShadowRoot,
        ClosedShadowRoot,
    }

    /// <summary>
    /// A node in the fixture tree: the smallest thing that can stand in for a DOM wrapper.
    /// </summary>
    private sealed class TreeEventTarget : JsEventTarget
    {
        private readonly List<string> _log;

        internal TreeEventTarget(Engine engine, Realm realm, string name, TreeKind kind, List<string> log)
            : base(engine, realm)
        {
            _prototype = realm.Intrinsics.EventTarget.PrototypeObject;
            Name = name;
            Kind = kind;
            _log = log;
            DefineOwnDataPropertyUnchecked("name", JsString.Create(name));
        }

        internal string Name { get; }

        private TreeKind Kind { get; }

        /// <summary>The node-tree parent, which is what roots and retargeting are computed from.</summary>
        internal TreeEventTarget? NodeParent { get; set; }

        /// <summary>The shadow host, set on a shadow root.</summary>
        internal TreeEventTarget? Host { get; init; }

        /// <summary>The slot this slottable is assigned to, if any.</summary>
        internal TreeEventTarget? Slot { get; set; }

        /// <summary>
        /// A parent that is not a node — the fixture's stand-in for a document answering the global scope.
        /// </summary>
        internal JsEventTarget? NonNodeParent { get; set; }

        /// <summary>Whether this target has activation behaviour.</summary>
        internal bool Activates { get; set; }

        /// <summary>Whether it also has the legacy pre- and canceled-activation pair.</summary>
        internal bool HasLegacyActivation { get; set; }

        internal override bool IsNode => true;

        internal override JsEventTarget? TreeParent => NodeParent;

        internal override bool IsShadowRoot => Kind is TreeKind.OpenShadowRoot or TreeKind.ClosedShadowRoot;

        internal override bool IsClosedShadowRoot => Kind == TreeKind.ClosedShadowRoot;

        internal override JsEventTarget? ShadowHost => Host;

        internal override bool IsSlot => Kind == TreeKind.Slot;

        internal override JsEventTarget? AssignedSlot => Slot;

        internal override bool HasActivationBehavior => Activates;

        internal override void ActivationBehavior(JsEvent ev) => _log.Add("activate:" + Name);

        internal override void LegacyPreActivationBehavior()
        {
            if (HasLegacyActivation)
            {
                _log.Add("pre:" + Name);
            }
        }

        internal override void LegacyCanceledActivationBehavior()
        {
            if (HasLegacyActivation)
            {
                _log.Add("canceled:" + Name);
            }
        }

        /// <summary>
        /// The three <i>get the parent</i> overrides DOM defines, in one class: a shadow root answers its
        /// host unless a non-composed event started inside it, a slottable answers its assigned slot, and
        /// anything else answers its parent — or, for the fixture's document, a target that is not a node.
        /// </summary>
        internal override JsEventTarget? GetParent(JsEvent ev)
        {
            if (IsShadowRoot)
            {
                if (!ev.Composed
                    && ev.Path is { Count: > 0 } path
                    && ReferenceEquals(path[0].InvocationTarget.GetRoot(), this))
                {
                    return null;
                }

                return Host;
            }

            return Slot ?? (JsEventTarget?) NodeParent ?? NonNodeParent;
        }
    }

    /// <summary>An event whose type makes it an activation event, standing in for a <c>click</c>.</summary>
    private sealed class ActivationEvent : JsEvent
    {
        internal ActivationEvent(Engine engine, Realm realm, bool bubbles, bool cancelable)
            : base(engine, new JsString("click"), new EventInit(bubbles, cancelable, Composed: false), timeStamp: 0)
        {
            _prototype = realm.Intrinsics.Event.PrototypeObject;
        }

        internal override bool IsActivationEvent => true;
    }

    /// <summary>
    /// An engine with the events feature, a <c>log</c> array and a <c>note</c> helper, plus the factory
    /// methods that build the fixture tree and publish each node to script under its own name.
    /// </summary>
    private sealed class Fixture
    {
        internal Fixture(WebApiFeatures features = WebApiFeatures.Events, DiagnosticsSink? sink = null)
        {
            Engine = new Engine(options =>
            {
                options.UseWebApis(features);
                if (sink is not null)
                {
                    options.WebApi.Diagnostics.Sink = sink;
                }
            });

            Engine.Execute("var log = []; function note(s) { log.push(s); }");
            Engine.SetValue("relatedName", new Func<JsValue, string>(NameOfRelatedTarget));
        }

        internal Engine Engine { get; }

        /// <summary>What the activation hooks and the CLR-side probes record, in order.</summary>
        internal List<string> HostLog { get; } = new();

        private Realm Realm => Engine.Realm;

        internal TreeEventTarget Node(string name, TreeEventTarget? parent = null, TreeKind kind = TreeKind.Node)
        {
            var node = new TreeEventTarget(Engine, Realm, name, kind, HostLog) { NodeParent = parent };
            Engine.SetValue(name, node);
            return node;
        }

        internal TreeEventTarget ShadowRoot(string name, TreeEventTarget host, bool closed)
        {
            var kind = closed ? TreeKind.ClosedShadowRoot : TreeKind.OpenShadowRoot;
            var root = new TreeEventTarget(Engine, Realm, name, kind, HostLog) { Host = host };
            Engine.SetValue(name, root);
            return root;
        }

        /// <summary>The <c>root &gt; mid &gt; leaf</c> chain every ordering test uses.</summary>
        internal (TreeEventTarget Root, TreeEventTarget Mid, TreeEventTarget Leaf) Chain()
        {
            var root = Node("root");
            var mid = Node("mid", root);
            var leaf = Node("leaf", mid);
            return (root, mid, leaf);
        }

        internal string Log => Engine.Evaluate("log.join(',')").AsString();

        internal void Execute(string source) => Engine.Execute(source);

        internal JsValue Evaluate(string source) => Engine.Evaluate(source);

        /// <summary>
        /// The event's related target by name. It is read through a host function because no interface this
        /// engine ships declares the IDL attribute — a <c>MouseEvent</c> is what would.
        /// </summary>
        private static string NameOfRelatedTarget(JsValue value) =>
            ((JsEvent) value).RelatedTarget is TreeEventTarget node ? node.Name : "null";
    }

    /// <summary>
    /// The listener registration every ordering test uses: one capturing and one bubbling listener on each of
    /// the three targets, each noting its target's name, the phase it saw and whether it was handed itself.
    /// </summary>
    private const string ListenOnChain = """
        ['root', 'mid', 'leaf'].forEach(function (name) {
            var t = globalThis[name];
            t.addEventListener('ping', function (e) { note(name + ':capture:' + e.eventPhase + ':' + (e.currentTarget === t)); }, true);
            t.addEventListener('ping', function (e) { note(name + ':bubble:' + e.eventPhase + ':' + (e.currentTarget === t)); }, false);
        });
        """;

    [Test]
    public void WalksTheTreeDownInTheCapturePhaseAndBackUpInTheBubblePhase()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute(ListenOnChain);
        fixture.Execute("leaf.dispatchEvent(new Event('ping', { bubbles: true }));");

        // CAPTURING_PHASE is 1, AT_TARGET 2 and BUBBLING_PHASE 3; currentTarget is the invocation target
        // rather than the dispatch target throughout.
        fixture.Log.Should().Be(
            "root:capture:1:true,mid:capture:1:true,leaf:capture:2:true,leaf:bubble:2:true,mid:bubble:3:true,root:bubble:3:true");
    }

    [Test]
    public void ReportsTheDispatchTargetToEveryListenerOnThePath()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute("""
            ['root', 'mid', 'leaf'].forEach(function (name) {
                globalThis[name].addEventListener('ping', function (e) { note(name + ':' + e.target.name); }, true);
            });
            leaf.dispatchEvent(new Event('ping'));
            """);

        fixture.Log.Should().Be("root:leaf,mid:leaf,leaf:leaf");
    }

    [Test]
    public void ANonBubblingEventStopsAtItsTarget()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute(ListenOnChain);
        fixture.Execute("leaf.dispatchEvent(new Event('ping'));");

        // The capture pass is unaffected — bubbles decides only whether an item that is not a target is
        // invoked on the way back up, which is why both of the leaf's own listeners still run.
        fixture.Log.Should().Be("root:capture:1:true,mid:capture:1:true,leaf:capture:2:true,leaf:bubble:2:true");
    }

    [Test]
    public void AtTheTargetACapturingListenerRunsBeforeANonCapturingOneHoweverTheyWereRegistered()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute("""
            leaf.addEventListener('ping', function () { note('registered-first-non-capturing'); }, false);
            leaf.addEventListener('ping', function () { note('registered-second-capturing'); }, true);
            leaf.dispatchEvent(new Event('ping'));
            """);

        // The target's item is walked by both passes, and the capture flag decides which one runs a listener.
        fixture.Log.Should().Be("registered-second-capturing,registered-first-non-capturing");
    }

    [Test]
    public void StopPropagationEndsTheDispatchAfterTheCurrentTarget()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute(ListenOnChain);
        fixture.Execute("""
            mid.addEventListener('ping', function (e) { note('stop'); e.stopPropagation(); }, true);
            leaf.dispatchEvent(new Event('ping', { bubbles: true }));
            """);

        // Both of mid's capturing listeners run — stopPropagation ends further *items*, not the list it was
        // called in — and nothing below or above mid is reached.
        fixture.Log.Should().Be("root:capture:1:true,mid:capture:1:true,stop");
    }

    [Test]
    public void StopImmediatePropagationAlsoEndsTheListenerListItWasCalledIn()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute(ListenOnChain);
        fixture.Execute("""
            mid.addEventListener('ping', function (e) { note('stop'); e.stopImmediatePropagation(); }, true);
            mid.addEventListener('ping', function () { note('never'); }, true);
            leaf.dispatchEvent(new Event('ping', { bubbles: true }));
            """);

        fixture.Log.Should().Be("root:capture:1:true,mid:capture:1:true,stop");
    }

    [Test]
    public void CancelBubbleReadsTheStopPropagationFlagAndSetsItOneWay()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute("""
            root.addEventListener('ping', function (e) {
                note('before:' + e.cancelBubble);
                e.cancelBubble = true;
                note('after:' + e.cancelBubble);
                e.cancelBubble = false;
                note('unset:' + e.cancelBubble);
            }, true);
            mid.addEventListener('ping', function () { note('never'); }, true);
            var e = new Event('ping', { bubbles: true });
            leaf.dispatchEvent(e);
            note('cleared:' + e.cancelBubble);
            """);

        // Assigning false does nothing, and the flag is unset by the dispatch tail rather than by the setter.
        fixture.Log.Should().Be("before:false,after:true,unset:true,cleared:false");
    }

    [Test]
    public void TheListenerListIsClonedPerItemRatherThanPerDispatch()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute("""
            function late() { note('late'); }
            function doomed() { note('doomed'); }
            root.addEventListener('ping', function () {
                note('root');
                root.addEventListener('ping', late, true);
                mid.addEventListener('ping', late, true);
                root.removeEventListener('ping', doomed, true);
                mid.removeEventListener('ping', doomed, true);
            }, true);
            root.addEventListener('ping', doomed, true);
            mid.addEventListener('ping', doomed, true);
            leaf.dispatchEvent(new Event('ping'));
            """);

        // root's list was already cloned, so neither the listener added to it nor the one removed from it
        // changes what runs there; mid has not been reached yet, so both edits take effect before it is.
        fixture.Log.Should().Be("root,late");
    }

    [Test]
    public void AOnceListenerOnAnAncestorIsRemovedBeforeItIsInvoked()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute("""
            root.addEventListener('ping', function () { note('root'); }, { once: true });
            leaf.dispatchEvent(new Event('ping', { bubbles: true }));
            leaf.dispatchEvent(new Event('ping', { bubbles: true }));
            """);

        fixture.Log.Should().Be("root");
    }

    [Test]
    public void APassiveListenerOnAnAncestorCannotCancelTheEvent()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute("""
            root.addEventListener('ping', function (e) { e.preventDefault(); note('passive:' + e.defaultPrevented); }, { passive: true });
            mid.addEventListener('ping', function (e) { e.preventDefault(); note('active:' + e.defaultPrevented); });
            var result = leaf.dispatchEvent(new Event('ping', { bubbles: true, cancelable: true }));
            note('result:' + result);
            """);

        fixture.Log.Should().Be("active:true,passive:true,result:false");
    }

    [Test]
    public void AnAbortedSignalRemovesTheListenersItArmedPartWayThroughADispatch()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute("""
            var controller = new AbortController();
            root.addEventListener('ping', function () { note('root'); controller.abort(); }, true);
            mid.addEventListener('ping', function () { note('mid'); }, { capture: true, signal: controller.signal });
            leaf.addEventListener('ping', function () { note('leaf'); }, { capture: true, signal: controller.signal });
            leaf.dispatchEvent(new Event('ping'));
            """);

        fixture.Log.Should().Be("root");
    }

    [Test]
    public void DispatchEventAnswersFalseOnlyWhenTheEventWasCanceled()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Evaluate("leaf.dispatchEvent(new Event('ping', { bubbles: true, cancelable: true }))").AsBoolean().Should().BeTrue();

        fixture.Execute("root.addEventListener('ping', function (e) { e.preventDefault(); });");
        fixture.Evaluate("leaf.dispatchEvent(new Event('ping', { bubbles: true, cancelable: true }))").AsBoolean().Should().BeFalse();

        // A non-cancelable event cannot be canceled from anywhere on the path.
        fixture.Evaluate("leaf.dispatchEvent(new Event('ping', { bubbles: true }))").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void RedispatchingAnEventThatIsBeingDispatchedThroughTheTreeThrowsInvalidStateError()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute("""
            var e = new Event('ping', { bubbles: true });
            root.addEventListener('ping', function () {
                try { mid.dispatchEvent(e); } catch (err) { note(err.name); }
            }, true);
            leaf.dispatchEvent(e);
            """);

        fixture.Log.Should().Be("InvalidStateError");

        // And the outer dispatch still unwound, so the event is usable again.
        fixture.Evaluate("e.eventPhase").AsNumber().Should().Be(0);
        fixture.Evaluate("e.currentTarget").IsNull().Should().BeTrue();
        fixture.Evaluate("e.composedPath().length").AsNumber().Should().Be(0);
    }

    [Test]
    public void AListenerThatThrowsIsReportedAndTheRestOfThePathStillRuns()
    {
        var sink = new RecordingSink();
        var fixture = new Fixture(sink: sink);
        fixture.Chain();

        fixture.Execute("""
            root.addEventListener('ping', function () { note('root'); throw new Error('boom'); }, true);
            mid.addEventListener('ping', function () { note('mid'); }, true);
            leaf.addEventListener('ping', function () { note('leaf'); }, true);
            leaf.dispatchEvent(new Event('ping'));
            """);

        fixture.Log.Should().Be("root,mid,leaf");
        sink.Reports.Should().ContainSingle();
        sink.Reports[0].Kind.Should().Be(DiagnosticEventKind.UncaughtCallbackError);
    }

    [Test]
    public void ADocumentsParentPutsTheGlobalScopeOnThePath()
    {
        var fixture = new Fixture(WebApiFeatures.Events | WebApiFeatures.GlobalEvents);
        var document = fixture.Node("document");
        fixture.Node("body", document);
        document.NonNodeParent = fixture.Engine._webApi!.GlobalEventTarget;

        fixture.Execute("""
            addEventListener('ping', function (e) { note('window:' + (e.currentTarget === globalThis) + ':' + e.target.name + ':' + e.eventPhase); }, true);
            document.addEventListener('ping', function (e) { note('document:' + e.eventPhase); }, true);
            body.dispatchEvent(new Event('ping', { bubbles: true }));
            """);

        // The global scope is on the path as an item the event passes through, so it reports the capturing
        // phase and the node that was dispatched at, with the global object as its currentTarget.
        fixture.Log.Should().Be("window:true:body:1,document:1");
    }

    [Test]
    public void TheGlobalObjectPassesEventTargetsBrandCheck()
    {
        var fixture = new Fixture(WebApiFeatures.Events | WebApiFeatures.GlobalEvents);

        fixture.Execute("""
            var add = EventTarget.prototype.addEventListener;
            var remove = EventTarget.prototype.removeEventListener;
            var dispatch = EventTarget.prototype.dispatchEvent;
            function borrowed(e) { note('borrowed:' + (e.target === globalThis) + ':' + (e.currentTarget === globalThis)); }
            add.call(globalThis, 'ping', borrowed);
            addEventListener('ping', function () { note('free'); });
            note('result:' + dispatch.call(globalThis, new Event('ping')));
            remove.call(globalThis, 'ping', borrowed);
            dispatchEvent(new Event('ping'));
            """);

        // The borrowed operations reach the very listener list the free globals write to, in registration
        // order, and removing through the borrowed one takes the listener off that same list.
        fixture.Log.Should().Be("borrowed:true:true,free,result:true,free");
    }

    [Test]
    public void TheGlobalObjectIsRefusedByEventTargetWhenTheGlobalEventsFeatureIsOff()
    {
        var fixture = new Fixture();

        var exception = Assert.Throws<JavaScriptException>(
            () => fixture.Execute("EventTarget.prototype.addEventListener.call(globalThis, 'ping', function () {});"));

        exception!.Message.Should().Contain("not an EventTarget");
    }

    [Test]
    public void ATreeLessDispatchBuildsNoPath()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));

        engine.Execute("""
            var target = new EventTarget();
            var seen = null;
            target.addEventListener('ping', function (e) { seen = e.composedPath(); });
            var e = new Event('ping', { bubbles: true });
            target.dispatchEvent(e);
            """);

        // The whole promise of the tree-less lane: no path list is ever allocated for it.
        var ev = (JsEvent) engine.Evaluate("e");
        ev.Path.Should().BeNull();

        // And composedPath() still answers the one item the algorithm would have built, then nothing.
        engine.Evaluate("seen.length").AsNumber().Should().Be(1);
        engine.Evaluate("seen[0] === target").AsBoolean().Should().BeTrue();
        engine.Evaluate("e.composedPath().length").AsNumber().Should().Be(0);
    }

    [Test]
    public void ComposedPathAnswersTheWholePathFromEveryPhase()
    {
        var fixture = new Fixture();
        fixture.Chain();

        fixture.Execute("""
            function names(e) { return e.composedPath().map(function (t) { return t.name; }).join('>'); }
            ['root', 'mid', 'leaf'].forEach(function (name) {
                globalThis[name].addEventListener('ping', function (e) { note(name + '=' + names(e)); }, true);
            });
            leaf.dispatchEvent(new Event('ping', { bubbles: true }));
            note('after=' + names(new Event('x')));
            """);

        // Target-first order, identical from every item, and empty once the dispatch is over.
        fixture.Log.Should().Be("root=leaf>mid>root,mid=leaf>mid>root,leaf=leaf>mid>root,after=");
    }

    [Test]
    public void AnEventOutOfAnOpenShadowTreeRetargetsItsTargetButHidesNothing()
    {
        var fixture = ShadowFixture(closed: false);

        fixture.Execute("""
            function names(e) { return e.composedPath().map(function (t) { return t.name; }).join('>'); }
            outer.addEventListener('ping', function (e) { note('outer:' + e.target.name + ':' + names(e)); });
            inner.addEventListener('ping', function (e) { note('inner:' + e.target.name + ':' + names(e)); });
            inner.dispatchEvent(new Event('ping', { bubbles: true, composed: true }));
            """);

        // The listener inside sees the real target; the one outside sees the host, because a node in a shadow
        // tree is retargeted against it. An open tree hides nothing from composedPath().
        fixture.Log.Should().Be("inner:inner:inner>shadow>host>outer,outer:host:inner>shadow>host>outer");
    }

    [Test]
    public void AClosedShadowTreeIsHiddenFromComposedPathOutsideIt()
    {
        var fixture = ShadowFixture(closed: true);

        fixture.Execute("""
            function names(e) { return e.composedPath().map(function (t) { return t.name; }).join('>'); }
            outer.addEventListener('ping', function (e) { note('outer:' + e.target.name + ':' + names(e)); });
            inner.addEventListener('ping', function (e) { note('inner:' + e.target.name + ':' + names(e)); });
            inner.dispatchEvent(new Event('ping', { bubbles: true, composed: true }));
            """);

        fixture.Log.Should().Be("inner:inner:inner>shadow>host>outer,outer:host:host>outer");
    }

    [Test]
    public void AnEventWhoseComposedFlagIsUnsetNeverLeavesTheShadowTree()
    {
        var fixture = ShadowFixture(closed: false);

        fixture.Execute("""
            outer.addEventListener('ping', function () { note('outer'); });
            host.addEventListener('ping', function () { note('host'); });
            shadow.addEventListener('ping', function () { note('shadow'); });
            inner.addEventListener('ping', function (e) { note('inner:' + e.target.name); });
            var e = new Event('ping', { bubbles: true });
            inner.dispatchEvent(e);
            note('target-cleared:' + (e.target === null));
            """);

        // The shadow root's get-the-parent ends the path, and the dispatch tail then forgets the target
        // because the last thing that was one is inside a shadow tree.
        fixture.Log.Should().Be("inner:inner,shadow,target-cleared:true");
    }

    [Test]
    public void ARelatedTargetInsideTheSameShadowTreeStopsTheDispatchAtItsRoot()
    {
        var fixture = new Fixture();
        var outer = fixture.Node("outer");
        var host = fixture.Node("host", outer);
        var shadow = fixture.ShadowRoot("shadow", host, closed: false);
        var from = fixture.Node("from", shadow);
        var to = fixture.Node("to", shadow);

        fixture.Execute("""
            outer.addEventListener('ping', function () { note('outer'); });
            host.addEventListener('ping', function () { note('host'); });
            shadow.addEventListener('ping', function () { note('shadow'); });
            to.addEventListener('ping', function () { note('to'); });
            """);

        var ev = (JsEvent) fixture.Evaluate("new Event('ping', { bubbles: true, composed: true })");
        ev.RelatedTarget = from;
        to.DispatchEvent(ev);

        // Both nodes retarget onto the host as soon as the event tries to leave the shadow tree, so the walk
        // stops there rather than telling anything outside about a movement that never left the tree.
        fixture.Log.Should().Be("to,shadow");
    }

    [Test]
    public void ARelatedTargetInsideAShadowTreeIsReportedOutsideAsItsHost()
    {
        var fixture = new Fixture();
        var outer = fixture.Node("outer");
        var host = fixture.Node("host", outer);
        var shadow = fixture.ShadowRoot("shadow", host, closed: false);
        var inner = fixture.Node("inner", shadow);
        var target = fixture.Node("target", outer);

        fixture.Execute("""
            outer.addEventListener('ping', function (e) { note('outer:' + relatedName(e)); }, true);
            target.addEventListener('ping', function (e) { note('target:' + relatedName(e)); });
            """);

        var ev = (JsEvent) fixture.Evaluate("new Event('ping', { bubbles: true })");
        ev.RelatedTarget = inner;
        target.DispatchEvent(ev);

        // Neither item is inside the shadow tree, so neither is told which node inside it the event came
        // from — retargeting answers the host to both.
        fixture.Log.Should().Be("outer:host,target:host");
    }

    [Test]
    public void ActivationBehaviourRunsAfterTheListenersUnlessTheEventWasCanceled()
    {
        var fixture = new Fixture();
        var (root, _, leaf) = fixture.Chain();
        root.Activates = true;

        fixture.Execute("""
            root.addEventListener('click', function () { note('root'); });
            leaf.addEventListener('click', function () { note('leaf'); });
            """);

        leaf.DispatchEvent(new ActivationEvent(fixture.Engine, fixture.Engine.Realm, bubbles: true, cancelable: true));

        fixture.Log.Should().Be("leaf,root");
        fixture.HostLog.Should().Equal("activate:root");
    }

    [Test]
    public void LegacyPreActivationRunsBeforeTheFirstListenerAndTheCanceledPairUndoesIt()
    {
        var fixture = new Fixture();
        var (root, _, leaf) = fixture.Chain();
        root.Activates = true;
        root.HasLegacyActivation = true;

        var order = fixture.HostLog;
        fixture.Engine.SetValue("markListener", new Action(() => order.Add("listener")));
        fixture.Execute("leaf.addEventListener('click', function () { markListener(); });");

        leaf.DispatchEvent(new ActivationEvent(fixture.Engine, fixture.Engine.Realm, bubbles: true, cancelable: true));
        order.Should().Equal("pre:root", "listener", "activate:root");

        // A listener that cancels gets the pre-activation undone instead of the behaviour run.
        order.Clear();
        fixture.Execute("leaf.addEventListener('click', function (e) { e.preventDefault(); });");
        leaf.DispatchEvent(new ActivationEvent(fixture.Engine, fixture.Engine.Realm, bubbles: true, cancelable: true));
        order.Should().Equal("pre:root", "listener", "canceled:root");
    }

    [Test]
    public void AnActivationEventThatDoesNotBubbleNeverAdoptsAnAncestorAsItsActivationTarget()
    {
        var fixture = new Fixture();
        var (root, _, leaf) = fixture.Chain();
        root.Activates = true;

        leaf.DispatchEvent(new ActivationEvent(fixture.Engine, fixture.Engine.Realm, bubbles: false, cancelable: true));

        // Step 6.9.5 only adopts an ancestor for a bubbling event.
        fixture.HostLog.Should().BeEmpty();
    }

    [Test]
    public void AnEventThatIsNotAnActivationEventRunsNoActivationBehaviour()
    {
        var fixture = new Fixture();
        var (root, _, _) = fixture.Chain();
        root.Activates = true;
        root.HasLegacyActivation = true;

        fixture.Execute("leaf.dispatchEvent(new Event('click', { bubbles: true, cancelable: true }));");

        fixture.HostLog.Should().BeEmpty();
    }

    [Test]
    public void ASlottedNodeKeepsItsTargetAndHidesTheClosedTreeItPassesThrough()
    {
        var fixture = new Fixture();
        var outer = fixture.Node("outer");
        var host = fixture.Node("host", outer);
        var shadow = fixture.ShadowRoot("shadow", host, closed: true);
        var slot = fixture.Node("slot", shadow, TreeKind.Slot);
        var light = fixture.Node("light", host);
        light.Slot = slot;

        fixture.Execute("""
            function names(e) { return e.composedPath().map(function (t) { return t.name; }).join('>'); }
            ['outer', 'host', 'shadow', 'slot', 'light'].forEach(function (name) {
                globalThis[name].addEventListener('ping', function (e) { note(name + ':' + e.target.name + ':' + names(e)); });
            });
            light.dispatchEvent(new Event('ping', { bubbles: true, composed: true }));
            """);

        // The slotted node is in the document tree, so nothing is retargeted and every listener is told the
        // real target — but the slot and the closed root it sits in are hidden from composedPath() on both
        // sides, which is the one thing a closed tree buys.
        fixture.Log.Should().Be(
            "light:light:light>host>outer,"
            + "slot:light:light>slot>shadow>host>outer,"
            + "shadow:light:light>slot>shadow>host>outer,"
            + "host:light:light>host>outer,"
            + "outer:light:light>host>outer");
    }

    /// <summary>
    /// <c>outer &gt; host</c>, a shadow root on the host, and <c>inner</c> inside it.
    /// </summary>
    private static Fixture ShadowFixture(bool closed)
    {
        var fixture = new Fixture();
        var outer = fixture.Node("outer");
        var host = fixture.Node("host", outer);
        var shadow = fixture.ShadowRoot("shadow", host, closed);
        fixture.Node("inner", shadow);
        return fixture;
    }
}
#endif
