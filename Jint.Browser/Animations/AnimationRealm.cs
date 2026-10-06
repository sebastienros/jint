using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Animations;

/// <summary>Lazy, per-realm Web Animations interface objects and per-document default timelines.</summary>
internal sealed class AnimationRealm
{
    internal static readonly string[] InterfaceNames =
        ["AnimationTimeline", "DocumentTimeline", "AnimationEffect", "KeyframeEffect", "Animation"];

    private readonly ConditionalWeakTable<Document, JsDocumentTimeline> _timelines = new();
    private readonly Dictionary<string, (ObjectInstance Prototype, HostInterfaceObject Interface)> _interfaces = new(StringComparer.Ordinal);
    private long _nextOrder;

    internal AnimationRealm(DomRealm dom) => Dom = dom;
    internal DomRealm Dom { get; }
    internal Engine Engine => Dom.Engine;
    internal Realm Realm => Dom.OwningRealm;
    internal AnimationRegistry? Registry => PageRuntime.Find(Engine)?.Animations;
    internal JsDocumentTimeline? DefaultTimeline => Dom.Document is { } document ? TimelineFor(document) : null;
    internal ObjectInstance KeyframeEffectPrototype => Interface("KeyframeEffect").Prototype;
    internal ObjectInstance AnimationPrototype => Interface("Animation").Prototype;
    internal ObjectInstance DocumentTimelinePrototype => Interface("DocumentTimeline").Prototype;

    internal long NextOrder() => Registry?.NextOrder() ?? ++_nextOrder;
    internal JsValue InterfaceObject(string name) => Interface(name).Interface;

    /// <summary>https://drafts.csswg.org/web-animations-1/#dom-document-timeline</summary>
    internal JsDocumentTimeline TimelineFor(Document document)
        => _timelines.GetValue(document, d => new JsDocumentTimeline(this, d, 0));

    internal static JsValue DocumentTimeline(DomRealm dom, Document document)
        => dom.RealmOfDocument(document).Animations.TimelineFor(document);

    private (ObjectInstance Prototype, HostInterfaceObject Interface) Interface(string name)
    {
        if (_interfaces.TryGetValue(name, out var existing))
        {
            return existing;
        }

        ObjectInstance parentPrototype = Realm.Intrinsics.Object.PrototypeObject;
        ObjectInstance? parentInterface = null;
        Func<JsValue[], ObjectInstance>? construct = null;
        var length = 0;
        JsObjectShape shape;
        switch (name)
        {
            case "AnimationTimeline":
                shape = AnimationShapes.Timeline;
                break;
            case "DocumentTimeline":
                var timeline = Interface("AnimationTimeline");
                parentPrototype = timeline.Prototype;
                parentInterface = timeline.Interface;
                shape = AnimationShapes.DocumentTimeline;
                construct = args =>
                {
                    var options = AnimationValues.Dictionary(this, args.At(0));
                    var origin = options?.Get("originTime") ?? JsValue.Undefined;
                    return new JsDocumentTimeline(this, Dom.Document, origin.IsUndefined() ? 0 : AnimationValues.Number(this, origin));
                };
                break;
            case "AnimationEffect":
                shape = AnimationShapes.Effect;
                break;
            case "KeyframeEffect":
                var effect = Interface("AnimationEffect");
                parentPrototype = effect.Prototype;
                parentInterface = effect.Interface;
                shape = AnimationShapes.KeyframeEffect;
                length = 1;
                construct = args => JsKeyframeEffect.Construct(this, args);
                break;
            default:
                parentPrototype = Realm.Intrinsics.EventTarget.PrototypeObject;
                parentInterface = Realm.Intrinsics.EventTarget;
                shape = AnimationShapes.Animation;
                construct = args => new JsAnimation(this, EffectArgument(this, args.At(0)),
                    args.At(1).IsUndefined() ? DefaultTimeline : TimelineArgument(this, args[1]));
                break;
        }

        using var scope = new RealmScope(Engine, Realm);
        var prototype = shape.Instantiate(Engine, parentPrototype);
        JsObjectShape.SetHostState(prototype, Dom);
        var iface = new HostInterfaceObject(Engine, Realm, name, prototype, length, construct, parentInterface);
        prototype.DefineOwnPropertyUnchecked("constructor", new PropertyDescriptor(iface, PropertyFlag.NonEnumerable));
        _interfaces.Add(name, (prototype, iface));
        return (prototype, iface);
    }

    internal static JsAnimationEffect? EffectArgument(AnimationRealm owner, JsValue value)
    {
        if (value.IsNullOrUndefined())
        {
            return null;
        }

        if (value is JsAnimationEffect effect)
        {
            return effect;
        }

        Throw.TypeError(owner.Realm, "Expected an AnimationEffect or null.");
        return null;
    }

    internal static JsAnimationTimeline? TimelineArgument(AnimationRealm owner, JsValue value)
    {
        if (value.IsNullOrUndefined())
        {
            return null;
        }

        if (value is JsAnimationTimeline timeline)
        {
            return timeline;
        }

        Throw.TypeError(owner.Realm, "Expected an AnimationTimeline or null.");
        return null;
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#dom-animatable-animate</summary>
    internal static JsValue Animate(DomRealm dom, Element target, JsValue[] args)
    {
        var owner = dom.Animations;
        AnimationValues.Required(owner, args, 1);
        var effect = JsKeyframeEffect.Create(owner, target, args[0], args.At(1), out var id, out var timeline, animate: true);
        var animation = new JsAnimation(owner, effect, timeline) { Id = id };
        animation.Play();
        return animation;
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#dom-animatable-getanimations</summary>
    internal static JsValue GetAnimations(DomRealm dom, Node node, JsValue[] args)
    {
        var owner = dom.Animations;
        var subtree = node is Document;
        if (node is Element)
        {
            var options = AnimationValues.Dictionary(owner, args.At(0));
            subtree = options is not null && TypeConverter.ToBoolean(options.Get("subtree"));
        }

        return owner.Realm.Intrinsics.Array.ConstructFast(owner.Registry?.GetAnimations(node, subtree) ?? []);
    }
}

/// <summary>https://drafts.csswg.org/web-animations-1/#the-animationtimeline-interface</summary>
internal abstract class JsAnimationTimeline : ObjectInstance
{
    protected JsAnimationTimeline(AnimationRealm owner) : base(owner.Engine) => Owner = owner;
    internal AnimationRealm Owner { get; }
    internal abstract double? CurrentTime { get; }
}

/// <summary>https://drafts.csswg.org/web-animations-1/#document-timelines</summary>
internal sealed class JsDocumentTimeline : JsAnimationTimeline
{
    internal JsDocumentTimeline(AnimationRealm owner, Document? document, double originTime) : base(owner)
    {
        Document = document;
        OriginTime = originTime;
        _prototype = owner.DocumentTimelinePrototype;
    }

    internal Document? Document { get; }
    internal double OriginTime { get; }
    internal override double? CurrentTime => PageRuntime.FindBrowsingContext(Owner.Engine, Document) is { } runtime
        ? (runtime.AnimationFrames.FrameTime ?? runtime.Now) - OriginTime : null;
}
