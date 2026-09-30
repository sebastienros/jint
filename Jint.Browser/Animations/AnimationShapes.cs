using Jint.Browser.Dom;
using Jint.Browser.SystemState;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;

namespace Jint.Browser.Animations;

/// <summary>https://drafts.csswg.org/web-animations-1/#programming-interface</summary>
internal static class AnimationShapes
{
    internal static readonly JsObjectShape Timeline = Base("AnimationTimeline")
        .Accessor("currentTime", static (t, _) => AnimationValues.Value(SystemBrand.Of<JsAnimationTimeline>(t, "AnimationTimeline", "currentTime").CurrentTime))
        .Build();

    internal static readonly JsObjectShape DocumentTimeline = Base("DocumentTimeline").Build();

    internal static readonly JsObjectShape Effect = Base("AnimationEffect")
        .Method("getTiming", static (t, _) => EffectOf(t, "getTiming").GetTiming(computed: false))
        .Method("getComputedTiming", static (t, _) => EffectOf(t, "getComputedTiming").GetTiming(computed: true))
        .Method("updateTiming", static (t, args) =>
        {
            EffectOf(t, "updateTiming").UpdateTiming(args.At(0));
            return JsValue.Undefined;
        })
        .Build();

    internal static readonly JsObjectShape KeyframeEffect = Base("KeyframeEffect")
        .Accessor("target", static (t, _) =>
        {
            var effect = Keyframes(t, "target");
            return effect.Target is { } target ? effect.Owner.Dom.WrapNode(target) : JsValue.Null;
        }, static (t, args) =>
        {
            var effect = Keyframes(t, "target");
            effect.Target = AnimationValues.Target(effect.Owner, args.At(0));
            return JsValue.Undefined;
        })
        .Accessor("pseudoElement", static (t, _) => DomConvert.NullableText(Keyframes(t, "pseudoElement").PseudoElement),
            static (t, args) =>
            {
                var effect = Keyframes(t, "pseudoElement");
                effect.PseudoElement = args.At(0).IsNullOrUndefined() ? null : TypeConverter.ToString(args[0]);
                return JsValue.Undefined;
            })
        .Accessor("composite", static (t, _) => JsString.Create(Keyframes(t, "composite").Composite),
            static (t, args) =>
            {
                var effect = Keyframes(t, "composite");
                effect.Composite = AnimationValues.Enum(effect.Owner, args.At(0), "replace", "add", "accumulate");
                return JsValue.Undefined;
            })
        .Method("getKeyframes", static (t, _) => Keyframes(t, "getKeyframes").GetKeyframes())
        .Method("setKeyframes", static (t, args) =>
        {
            var effect = Keyframes(t, "setKeyframes");
            AnimationValues.Required(effect.Owner, args, 1);
            effect.SetKeyframes(args[0]);
            return JsValue.Undefined;
        }, length: 1)
        .Build();

    internal static readonly JsObjectShape Animation = Base("Animation")
        .Accessor("id", static (t, _) => JsString.Create(AnimationOf(t, "id").Id), static (t, args) =>
        {
            AnimationOf(t, "id").Id = TypeConverter.ToString(args.At(0));
            return JsValue.Undefined;
        })
        .Accessor("effect", static (t, _) => (JsValue?) AnimationOf(t, "effect").Effect ?? JsValue.Null, static (t, args) =>
        {
            var animation = AnimationOf(t, "effect");
            animation.Effect = AnimationRealm.EffectArgument(animation.Owner, args.At(0));
            return JsValue.Undefined;
        })
        .Accessor("timeline", static (t, _) => (JsValue?) AnimationOf(t, "timeline").Timeline ?? JsValue.Null, static (t, args) =>
        {
            var animation = AnimationOf(t, "timeline");
            animation.Timeline = AnimationRealm.TimelineArgument(animation.Owner, args.At(0));
            return JsValue.Undefined;
        })
        .Accessor("startTime", static (t, _) => AnimationValues.Value(AnimationOf(t, "startTime").StartTime), static (t, args) =>
        {
            var animation = AnimationOf(t, "startTime");
            animation.StartTime = AnimationValues.NullableNumber(animation.Owner, args.At(0));
            return JsValue.Undefined;
        })
        .Accessor("currentTime", static (t, _) => AnimationValues.Value(AnimationOf(t, "currentTime").CurrentTime), static (t, args) =>
        {
            var animation = AnimationOf(t, "currentTime");
            animation.SetCurrentTime(AnimationValues.NullableNumber(animation.Owner, args.At(0)));
            return JsValue.Undefined;
        })
        .Accessor("playbackRate", static (t, _) => JsNumber.Create(AnimationOf(t, "playbackRate").PlaybackRate), static (t, args) =>
        {
            var animation = AnimationOf(t, "playbackRate");
            animation.PlaybackRate = AnimationValues.Number(animation.Owner, args.At(0));
            return JsValue.Undefined;
        })
        .Accessor("playState", static (t, _) => JsString.Create(AnimationOf(t, "playState").PlayState))
        .Accessor("replaceState", static (t, _) => JsString.Create(AnimationOf(t, "replaceState").ReplaceState))
        .Accessor("pending", static (t, _) => JsBoolean.Create(AnimationOf(t, "pending").Pending))
        .Accessor("ready", static (t, _) => AnimationOf(t, "ready").Ready)
        .Accessor("finished", static (t, _) => AnimationOf(t, "finished").Finished)
        .Handler("finish")
        .Handler("cancel")
        .Handler("remove")
        .Operation("cancel", static animation => animation.Cancel())
        .Operation("finish", static animation => animation.Finish())
        .Operation("play", static animation => animation.Play())
        .Operation("pause", static animation => animation.Pause())
        .Operation("reverse", static animation => animation.Reverse())
        .Operation("persist", static animation => animation.Persist())
        .Operation("commitStyles", static animation =>
        {
            if (animation.Effect is JsKeyframeEffect effect)
            {
                effect.CommitStyles();
            }
            else
            {
                DomFailures.Refuse(animation.Owner.Dom, "Animation.commitStyles", DomExceptionNames.NoModificationAllowed, "The animation has no style target.");
            }
        })
        .Method("updatePlaybackRate", static (t, args) =>
        {
            var animation = AnimationOf(t, "updatePlaybackRate");
            AnimationValues.Required(animation.Owner, args, 1);
            animation.UpdatePlaybackRate(AnimationValues.Number(animation.Owner, args[0]));
            return JsValue.Undefined;
        }, length: 1)
        .Build();

    private static JsObjectShape.Builder Base(string name) => new JsObjectShape.Builder().PerRealmSlot("constructor").ToStringTag(name);
    private static JsAnimationEffect EffectOf(JsValue t, string name) => SystemBrand.Of<JsAnimationEffect>(t, "AnimationEffect", name);
    private static JsKeyframeEffect Keyframes(JsValue t, string name) => SystemBrand.Of<JsKeyframeEffect>(t, "KeyframeEffect", name);
    private static JsAnimation AnimationOf(JsValue t, string name) => SystemBrand.Of<JsAnimation>(t, "Animation", name);

    private static JsObjectShape.Builder Handler(this JsObjectShape.Builder builder, string type)
        => builder.Accessor("on" + type,
            (t, _) => EventHandlerAttributes.Get(AnimationOf(t, "on" + type), type),
            (t, args) => EventHandlerAttributes.Set(AnimationOf(t, "on" + type), type, args.At(0)));

    private static JsObjectShape.Builder Operation(this JsObjectShape.Builder builder, string name, Action<JsAnimation> operation)
        => builder.Method(name, (t, _) =>
        {
            operation(AnimationOf(t, name));
            return JsValue.Undefined;
        });
}
