using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Animations;

/// <summary>
/// https://drafts.csswg.org/web-animations-1/#animation-frame-loop.
/// Only advancing/pending animations participate in frames. Relevant effects are indexed separately, so
/// paused and filling animations remain discoverable without retaining a timer or scanning on every frame.
/// </summary>
internal sealed class AnimationRegistry
{
    private readonly PageRuntime _runtime;
    private readonly HashSet<JsAnimation> _ticking = [];
    private readonly Dictionary<Element, HashSet<JsAnimation>> _targets = [];
    private readonly Dictionary<JsAnimation, Element> _registered = [];
    private readonly HashSet<Element> _replacementTargets = [];
    private readonly List<PendingEvent> _events = [];
    private long _nextOrder;
    private long _eventOrder;

    internal AnimationRegistry(PageRuntime runtime) => _runtime = runtime;
    internal bool NeedsFrames => _ticking.Count != 0 || _events.Count != 0 || _replacementTargets.Count != 0;
    internal long NextOrder() => ++_nextOrder;

    internal void Changed(JsAnimation animation)
    {
        if (animation.NeedsFrames)
        {
            _ticking.Add(animation);
        }
        else
        {
            _ticking.Remove(animation);
        }

        var target = animation.ReplaceState != "removed" && animation.Effect is JsKeyframeEffect { Relevant: true } effect
            ? effect.Target : null;
        if (_registered.TryGetValue(animation, out var previous) && !ReferenceEquals(previous, target))
        {
            var set = _targets[previous];
            set.Remove(animation);
            if (set.Count == 0)
            {
                _targets.Remove(previous);
            }

            _registered.Remove(animation);
        }

        if (target is not null)
        {
            if (!_targets.TryGetValue(target, out var set))
            {
                _targets.Add(target, set = []);
            }

            set.Add(animation);
            _registered[animation] = target;
            if (Replaceable(animation))
            {
                _replacementTargets.Add(target);
            }
        }

        _runtime.AnimationFrames.Refresh();
    }

    internal void Queue(JsAnimation target, JsAnimationPlaybackEvent ev, double? scheduled)
    {
        _events.Add(new(target, ev, scheduled, _eventOrder++));
        _runtime.AnimationFrames.Refresh();
    }

    /// <summary>Update timelines and pending tasks, remove replaced animations, then checkpoint and send events.</summary>
    internal void Update()
    {
        foreach (var animation in _ticking.ToArray())
        {
            _runtime.Engine.Constraints.Check();
            animation.Tick();
        }

        RemoveReplaced();
        _runtime.Engine.CleanUpAfterRunningScript();
        var events = _events.ToArray();
        _events.Clear();
        Array.Sort(events, static (a, b) =>
        {
            var byTime = Nullable.Compare(a.Scheduled, b.Scheduled);
            if (byTime != 0)
            {
                return byTime;
            }

            var byAnimation = a.Target.Order.CompareTo(b.Target.Order);
            return byAnimation != 0 ? byAnimation : a.Order.CompareTo(b.Order);
        });
        foreach (var pending in events)
        {
            _runtime.Engine.Constraints.Check();
            try
            {
                try
                {
                    pending.Target.DispatchEvent(pending.Event);
                }
                finally
                {
                    _runtime.Engine.CleanUpAfterRunningScript();
                }
            }
            catch (JavaScriptException exception)
            {
                _runtime.Recorder.Add(PageErrorKind.UncaughtCallbackError,
                    PageRecorder.Diagnostics.Describe(exception.Error, exception), "Animation");
            }
        }
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#removing-replaced-animations</summary>
    private void RemoveReplaced()
    {
        if (_replacementTargets.Count == 0)
        {
            return;
        }

        var targets = _replacementTargets.ToArray();
        _replacementTargets.Clear();
        foreach (var target in targets)
        {
            _runtime.Engine.Constraints.Check();
            if (!AnimationValues.IsConnected(target) || !_targets.TryGetValue(target, out var set))
            {
                continue;
            }

            var ordered = set.Where(Replaceable).OrderByDescending(static a => a.Order).ToArray();
            var covered = new HashSet<(string? Pseudo, string Property)>();
            foreach (var animation in ordered)
            {
                _runtime.Engine.Constraints.Check();
                var effect = (JsKeyframeEffect) animation.Effect!;
                var properties = new HashSet<(string? Pseudo, string Property)>();
                foreach (var frame in effect.Frames)
                {
                    _runtime.Engine.Constraints.Check();
                    foreach (var name in frame.Properties.Keys)
                    {
                        AnimationValues.Check(animation.Owner, properties.Count);
                        properties.Add((effect.PseudoElement, name));
                    }
                }

                if (animation.ReplaceState == "active" && properties.IsSubsetOf(covered))
                {
                    animation.Remove();
                }

                covered.UnionWith(properties);
            }
        }
    }

    private static bool Replaceable(JsAnimation animation) => animation.PlayState == "finished"
        && animation.ReplaceState != "removed" && animation.Timeline is JsDocumentTimeline
        && animation.Effect is JsKeyframeEffect { Target: not null } effect && effect.Sample.Progress is not null;

    internal JsValue[] GetAnimations(Node node, bool subtree)
    {
        var result = new List<JsAnimation>();
        if (node is Element element && !subtree)
        {
            if (_targets.TryGetValue(element, out var animations))
            {
                result.AddRange(animations.Where(static a => a.Effect?.Relevant == true && a.ReplaceState != "removed"));
            }
        }
        else
        {
            foreach (var (target, animations) in _targets)
            {
                _runtime.Engine.Constraints.Check();
                var depth = 0;
                for (Node? ancestor = target; ancestor is not null; ancestor = ancestor.ParentNode ?? (ancestor as ShadowRoot)?.Host)
                {
                    if (depth++ % Engine.ConstraintCheckInterval == 0)
                    {
                        _runtime.Engine.Constraints.Check();
                    }

                    if (ReferenceEquals(ancestor, node))
                    {
                        result.AddRange(animations.Where(static a => a.Effect?.Relevant == true && a.ReplaceState != "removed"));
                        break;
                    }
                }
            }
        }

        result.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        return [.. result];
    }

    private readonly record struct PendingEvent(JsAnimation Target, JsAnimationPlaybackEvent Event, double? Scheduled, long Order);
}
