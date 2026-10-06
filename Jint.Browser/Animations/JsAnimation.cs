using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.Streams;

namespace Jint.Browser.Animations;

/// <summary>
/// https://drafts.csswg.org/web-animations-1/#animations.
/// All state and promise settlement belongs to the page loop; the frame lane supplies ready times.
/// </summary>
internal sealed class JsAnimation : JsEventTarget
{
    private JsAnimationEffect? _effect;
    private JsAnimationTimeline? _timeline;
    private double? _startTime;
    private double? _holdTime;
    private double? _previousCurrentTime;
    private double _playbackRate = 1;
    private double? _pendingPlaybackRate;
    private PendingTask _pending;
    private PromiseCapability _ready;
    private PromiseCapability _finished;
    private bool _finishedResolved;
    private bool _finishNotificationQueued;
    private int _notificationGeneration;

    internal JsAnimation(AnimationRealm owner, JsAnimationEffect? effect, JsAnimationTimeline? timeline)
        : base(owner.Engine, owner.Realm)
    {
        Owner = owner;
        _prototype = owner.AnimationPrototype;
        Order = owner.NextOrder();
        _ready = NewPromise();
        _ready.Resolve(this);
        _finished = NewPromise();
        _timeline = timeline;
        Effect = effect;
    }

    internal AnimationRealm Owner { get; }
    internal long Order { get; }
    internal string Id { get; set; } = "";
    internal string ReplaceState { get; private set; } = "active";
    internal bool Pending => _pending != PendingTask.None;
    internal JsPromise Ready => StreamPromises.PromiseOf(_ready);
    internal JsPromise Finished => StreamPromises.PromiseOf(_finished);
    internal double EffectEnd => Effect?.Timing.EndTime ?? 0;
    private double EffectivePlaybackRate => _pendingPlaybackRate ?? _playbackRate;
    private double? TimelineTime => Timeline?.CurrentTime;
    internal double? CurrentTime => _holdTime ?? UnconstrainedTime;
    private double? UnconstrainedTime => TimelineTime is { } time && _startTime is { } start
        ? (time - start) * _playbackRate : null;

    internal bool NeedsFrames => TimelineTime is not null
        && (Pending || PlayState == "running" && _playbackRate != 0);

    /// <summary>https://drafts.csswg.org/web-animations-1/#setting-the-associated-effect</summary>
    internal JsAnimationEffect? Effect
    {
        get => _effect;
        set
        {
            if (ReferenceEquals(value, _effect))
            {
                return;
            }

            if (value?.Animation is { } previous)
            {
                previous.Effect = null;
            }

            if (_effect is not null)
            {
                _effect.Animation = null;
            }

            _effect = value;
            if (value is not null)
            {
                value.Animation = this;
            }

            EffectChanged();
        }
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#setting-the-timeline</summary>
    internal JsAnimationTimeline? Timeline
    {
        get => _timeline;
        set
        {
            if (ReferenceEquals(value, _timeline))
            {
                return;
            }

            _timeline = value;
            if (_startTime is not null)
            {
                _holdTime = null;
            }

            UpdateFinished();
            Changed();
        }
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#setting-the-start-time-of-an-animation</summary>
    internal double? StartTime
    {
        get => _startTime;
        set
        {
            if (TimelineTime is null && value is not null)
            {
                _holdTime = null;
            }

            var previous = CurrentTime;
            ApplyPlaybackRate();
            _startTime = value;
            if (value is null)
            {
                _holdTime = previous;
            }
            else if (_playbackRate != 0)
            {
                _holdTime = null;
            }

            CompletePending();
            UpdateFinished(didSeek: true);
            Changed();
        }
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#setting-the-playback-rate-of-an-animation</summary>
    internal double PlaybackRate
    {
        get => _playbackRate;
        set
        {
            _pendingPlaybackRate = null;
            var previous = CurrentTime;
            _playbackRate = value;
            if (Timeline is not null && previous is not null)
            {
                SetCurrentTime(previous);
            }

            UpdateFinished(didSeek: true);
            Changed();
        }
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#play-states</summary>
    internal string PlayState
    {
        get
        {
            var time = CurrentTime;
            if (time is null && _startTime is null && !Pending)
            {
                return "idle";
            }

            if (_pending == PendingTask.Pause || _startTime is null && _pending != PendingTask.Play)
            {
                return "paused";
            }

            if (time is { } current && (EffectivePlaybackRate > 0 && current >= EffectEnd || EffectivePlaybackRate < 0 && current <= 0))
            {
                return "finished";
            }

            return "running";
        }
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#setting-the-current-time-of-an-animation</summary>
    internal void SetCurrentTime(double? time)
    {
        SilentlySeek(time);
        if (_pending == PendingTask.Pause)
        {
            _holdTime = time;
            ApplyPlaybackRate();
            _startTime = null;
            CompletePending();
        }

        UpdateFinished(didSeek: true);
        Changed();
    }

    private void SilentlySeek(double? time)
    {
        if (time is null)
        {
            if (CurrentTime is not null)
            {
                Throw.TypeError(Owner.Realm, "A resolved currentTime cannot be made unresolved.");
            }

            return;
        }

        var timelineTime = TimelineTime;
        if (_holdTime is not null || _startTime is null || timelineTime is null || _playbackRate == 0)
        {
            _holdTime = time;
        }
        else
        {
            _startTime = timelineTime - time / _playbackRate;
        }

        if (timelineTime is null)
        {
            _startTime = null;
        }

        _previousCurrentTime = null;
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#playing-an-animation-section</summary>
    internal void Play(bool autoRewind = true)
    {
        var abortedPause = _pending == PendingTask.Pause;
        var time = CurrentTime;
        double? seek = null;
        if (autoRewind)
        {
            if (EffectivePlaybackRate >= 0 && (time is null || time < 0 || time >= EffectEnd))
            {
                seek = 0;
            }
            else if (EffectivePlaybackRate < 0 && (time is null || time <= 0 || time > EffectEnd))
            {
                if (double.IsPositiveInfinity(EffectEnd))
                {
                    Refuse("play", "Cannot play backwards from an infinite end.");
                }

                seek = EffectEnd;
            }
        }

        if (seek is null && _startTime is null && time is null)
        {
            seek = 0;
        }

        if (seek is not null)
        {
            _holdTime = seek;
        }

        if (_holdTime is not null)
        {
            _startTime = null;
        }

        var hadPending = Pending;
        _pending = PendingTask.None;
        if (_holdTime is null && seek is null && !abortedPause && _pendingPlaybackRate is null)
        {
            return;
        }

        if (!hadPending)
        {
            _ready = NewPromise();
        }

        _pending = PendingTask.Play;
        UpdateFinished();
        Changed();
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#pausing-an-animation-section</summary>
    internal void Pause()
    {
        if (_pending == PendingTask.Pause || PlayState == "paused")
        {
            return;
        }

        if (CurrentTime is null)
        {
            if (_playbackRate < 0 && double.IsPositiveInfinity(EffectEnd))
            {
                Refuse("pause", "Cannot pause at an infinite end.");
            }

            _holdTime = _playbackRate >= 0 ? 0 : EffectEnd;
        }

        if (!Pending)
        {
            _ready = NewPromise();
        }

        _pending = PendingTask.Pause;
        UpdateFinished();
        Changed();
    }

    /// <summary>The pending play/pause tasks in sections 4.5.8 and 4.5.9, at this frame's ready time.</summary>
    internal void Tick()
    {
        if (TimelineTime is { } readyTime)
        {
            var task = _pending;
            _pending = PendingTask.None;
            if (task == PendingTask.Play)
            {
                if (_holdTime is { } hold)
                {
                    ApplyPlaybackRate();
                    _startTime = _playbackRate == 0 ? readyTime : readyTime - hold / _playbackRate;
                    if (_playbackRate != 0)
                    {
                        _holdTime = null;
                    }
                }
                else if (_startTime is { } start && _pendingPlaybackRate is not null)
                {
                    var match = (readyTime - start) * _playbackRate;
                    ApplyPlaybackRate();
                    if (_playbackRate == 0)
                    {
                        _holdTime = match;
                    }

                    _startTime = _playbackRate == 0 ? readyTime : readyTime - match / _playbackRate;
                }

                _ready.Resolve(this);
            }
            else if (task == PendingTask.Pause)
            {
                if (_startTime is { } start && _holdTime is null)
                {
                    _holdTime = (readyTime - start) * _playbackRate;
                }

                ApplyPlaybackRate();
                _startTime = null;
                _ready.Resolve(this);
            }
        }

        UpdateFinished();
        Changed();
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#finishing-an-animation-section</summary>
    internal void Finish()
    {
        if (EffectivePlaybackRate == 0 || EffectivePlaybackRate > 0 && double.IsPositiveInfinity(EffectEnd))
        {
            Refuse("finish", "Cannot finish a zero-rate animation or an animation with an infinite end.");
        }

        ApplyPlaybackRate();
        var limit = _playbackRate > 0 ? EffectEnd : 0;
        SilentlySeek(limit);
        if (_startTime is null && TimelineTime is { } time)
        {
            _startTime = time - limit / _playbackRate;
        }

        if (_startTime is not null)
        {
            if (_pending == PendingTask.Pause)
            {
                _holdTime = null;
            }

            CompletePending();
        }

        UpdateFinished(didSeek: true, synchronouslyNotify: true);
        Changed();
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#canceling-an-animation-section</summary>
    internal void Cancel()
    {
        if (PlayState != "idle")
        {
            var error = Owner.Realm.Intrinsics.DomException.CreateException(DomExceptionNames.Abort, "The animation was canceled.");
            if (Pending)
            {
                _pending = PendingTask.None;
                ApplyPlaybackRate();
                StreamPromises.RejectHandled(_ready, error);
                _ready = NewPromise();
                _ready.Resolve(this);
            }

            StreamPromises.RejectHandled(_finished, error);
            _finished = NewPromise();
            _finishedResolved = false;
            QueueEvent("cancel", null, OriginTime(TimelineTime));
        }

        CancelFinishNotification();
        _holdTime = null;
        _startTime = null;
        _previousCurrentTime = null;
        Changed();
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#seamlessly-updating-the-playback-rate-of-an-animation</summary>
    internal void UpdatePlaybackRate(double rate)
    {
        var previousState = PlayState;
        _pendingPlaybackRate = rate;
        if (Pending)
        {
            return;
        }

        if (previousState is "idle" or "paused" || CurrentTime is null)
        {
            ApplyPlaybackRate();
        }
        else if (previousState == "finished")
        {
            var unconstrained = UnconstrainedTime;
            _startTime = rate == 0 ? TimelineTime : TimelineTime - unconstrained / rate;
            ApplyPlaybackRate();
            UpdateFinished();
        }
        else
        {
            Play(autoRewind: false);
        }

        Changed();
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#reversing-an-animation-section</summary>
    internal void Reverse()
    {
        if (TimelineTime is null)
        {
            Refuse("reverse", "The animation has no active timeline.");
        }

        var original = _pendingPlaybackRate;
        _pendingPlaybackRate = -EffectivePlaybackRate;
        try
        {
            Play();
        }
        catch (JavaScriptException)
        {
            _pendingPlaybackRate = original;
            throw;
        }
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#dom-animation-persist</summary>
    internal void Persist()
    {
        ReplaceState = "persisted";
        Changed();
    }

    internal void Remove()
    {
        ReplaceState = "removed";
        QueueEvent("remove", CurrentTime, OriginTime(TimelineTime));
        Changed();
    }

    internal void EffectChanged()
    {
        UpdateFinished();
        Changed();
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#updating-the-finished-state</summary>
    internal void UpdateFinished(bool didSeek = false, bool synchronouslyNotify = false)
    {
        var unconstrained = didSeek ? CurrentTime : UnconstrainedTime;
        if (unconstrained is { } time && _startTime is not null && !Pending)
        {
            if (_playbackRate > 0 && time >= EffectEnd)
            {
                _holdTime = didSeek ? time : Math.Max(_previousCurrentTime ?? EffectEnd, EffectEnd);
            }
            else if (_playbackRate < 0 && time <= 0)
            {
                _holdTime = didSeek ? time : Math.Min(_previousCurrentTime ?? 0, 0);
            }
            else if (_playbackRate != 0 && TimelineTime is { } timelineTime)
            {
                if (didSeek && _holdTime is { } hold)
                {
                    _startTime = timelineTime - hold / _playbackRate;
                }

                _holdTime = null;
            }
        }

        _previousCurrentTime = CurrentTime;
        if (PlayState == "finished" && !_finishedResolved)
        {
            if (synchronouslyNotify)
            {
                CancelFinishNotification();
                NotifyFinished();
            }
            else if (!_finishNotificationQueued)
            {
                _finishNotificationQueued = true;
                var generation = _notificationGeneration;
                Engine.AddToEventLoop(() =>
                {
                    if (generation == _notificationGeneration)
                    {
                        _finishNotificationQueued = false;
                        NotifyFinished();
                    }
                }, EventLoopJobKind.Microtask);
            }
        }
        else if (PlayState != "finished" && _finishedResolved)
        {
            _finished = NewPromise();
            _finishedResolved = false;
        }
    }

    private void NotifyFinished()
    {
        if (PlayState != "finished" || _finishedResolved)
        {
            return;
        }

        _finishedResolved = true;
        _finished.Resolve(this);
        var scheduled = _playbackRate == 0 || double.IsInfinity(EffectEnd) || _startTime is null
            ? null : OriginTime(EffectEnd / _playbackRate + _startTime);
        QueueEvent("finish", CurrentTime, scheduled);
    }

    private void CancelFinishNotification()
    {
        _notificationGeneration++;
        _finishNotificationQueued = false;
    }

    private double? OriginTime(double? timelineTime)
        => Timeline is JsDocumentTimeline document && timelineTime is { } time ? time + document.OriginTime : null;

    private void QueueEvent(string type, double? currentTime, double? scheduled)
    {
        var ev = JsAnimationPlaybackEvent.CreateTrusted(Owner.Dom, type, currentTime, TimelineTime);
        if (Owner.Registry is { } registry)
        {
            registry.Queue(this, ev, scheduled);
        }
        else
        {
            Engine.Tasks.Post(() =>
            {
                try
                {
                    DispatchEvent(ev);
                }
                finally
                {
                    Engine.CleanUpAfterRunningScript();
                }
            });
        }
    }

    private void ApplyPlaybackRate()
    {
        if (_pendingPlaybackRate is { } rate)
        {
            _playbackRate = rate;
            _pendingPlaybackRate = null;
        }
    }

    private void CompletePending()
    {
        if (Pending)
        {
            _pending = PendingTask.None;
            _ready.Resolve(this);
        }
    }

    private PromiseCapability NewPromise() => StreamPromises.NewPromise(Engine, Owner.Realm);
    private void Changed() => Owner.Registry?.Changed(this);

    private void Refuse(string member, string detail)
        => DomFailures.Refuse(Owner.Dom, "Animation." + member, DomExceptionNames.InvalidState, detail);

    private enum PendingTask : byte
    {
        None,
        Play,
        Pause,
    }
}
