using System.Runtime.CompilerServices;
using Jint.Browser.Events;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.Browser.Dom;

#pragma warning disable CA1822 // Concrete media receiver models keep their initial facts on the same binding API.

/// <summary>Script-written media state, independent of a decoder and of any engine.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/media.html#media-elements</remarks>
internal sealed partial class BrowserMediaState
{
    private static readonly ConditionalWeakTable<Element, BrowserMediaState> States = new();
    private readonly Element _element;
    private bool? _muted;
    private double _volume = 1;
    private double _defaultPlaybackRate = 1;
    private double _playbackRate = 1;
    private BrowserAudioTrackList? _audioTracks;
    private BrowserVideoTrackList? _videoTracks;
    private BrowserTextTrackList? _textTracks;

    private BrowserMediaState(Element element) => _element = element;

    internal static BrowserMediaState Of(DomRealm realm, Element element)
    {
        if (element.NamespaceUri != Namespaces.Html || element.LocalName is not ("audio" or "video"))
        {
            Throw.TypeError(realm.OwningRealm, "Illegal invocation of HTMLMediaElement member");
        }
        return States.GetValue(element, static target => new BrowserMediaState(target));
    }

    internal double CurrentTime { get; private set; }
    internal double Volume => _volume;
    internal bool Muted => _muted ?? _element.HasContentAttribute("muted");
    internal double DefaultPlaybackRate => _defaultPlaybackRate;
    internal double PlaybackRate => _playbackRate;
    internal double Duration => double.NaN;
    internal bool Paused => true;
    internal bool Ended => false;
    internal bool Seeking => false;
    internal int ReadyState => 0;
    internal int NetworkState => _loadOperation?.IsCanceled == true ? 0 : _networkState;
    internal string CurrentSrc => _loadOperation?.IsCanceled == true ? "" : _currentSrc;
    internal string PlaybackState => "waiting";
    internal BrowserMediaError? Error => _loadOperation?.IsCanceled == true ? null : _error;
    internal int VideoWidth => 0;
    internal int VideoHeight => 0;
    internal BrowserAudioTrackList AudioTracks => _audioTracks ??= new BrowserAudioTrackList();
    internal BrowserVideoTrackList VideoTracks => _videoTracks ??= new BrowserVideoTrackList();
    internal BrowserTextTrackList TextTracks => _textTracks ??= new BrowserTextTrackList();
    internal BrowserTimeRanges Buffered => BrowserTimeRanges.Empty;
    internal BrowserTimeRanges Played => BrowserTimeRanges.Empty;
    internal BrowserTimeRanges Seekable => BrowserTimeRanges.Empty;

    internal void SetCurrentTime(DomRealm realm, double value)
    {
        RequireFinite(realm, value, "currentTime");
        // HAVE_NOTHING: this is a requested start position, not a decoded seek.
        CurrentTime = value;
    }

    internal void SetVolume(DomRealm realm, double value)
    {
        RequireFinite(realm, value, "volume");
        if (value is < 0 or > 1)
        {
            DomFailures.Refuse(realm, "HTMLMediaElement.volume", "IndexSizeError", "Volume must be between zero and one.");
        }
        if (_volume == value) return;
        _volume = value;
        QueueEvent(realm, "volumechange");
    }

    internal void SetMuted(DomRealm realm, bool value)
    {
        if (_muted == value) return;
        _muted = value;
        QueueEvent(realm, "volumechange");
    }

    internal void SetDefaultPlaybackRate(DomRealm realm, double value)
    {
        RequireFinite(realm, value, "defaultPlaybackRate");
        if (_defaultPlaybackRate == value) return;
        _defaultPlaybackRate = value;
        QueueEvent(realm, "ratechange");
    }

    internal void SetPlaybackRate(DomRealm realm, double value)
    {
        RequireFinite(realm, value, "playbackRate");
        if (value < 0)
        {
            DomFailures.Refuse(realm, "HTMLMediaElement.playbackRate", "NotSupportedError", "Reverse playback is not available.");
        }
        if (_playbackRate == value) return;
        _playbackRate = value;
        QueueEvent(realm, "ratechange");
    }

    internal string CanPlayType(string _) => "";
    internal void Pause() { }

    internal JsValue Play(DomRealm realm)
    {
        var capability = PromiseConstructor.NewPromiseCapability(realm.Engine, realm.OwningRealm.Intrinsics.Promise);
        capability.Reject(realm.OwningRealm.Intrinsics.DomException.CreateException("NotSupportedError", "No media decoder is available."));
        return capability.PromiseInstance;
    }

    internal JsValue AddTextTrack(DomRealm realm, string kind, string _, string __)
    {
        if (kind is not ("subtitles" or "captions" or "descriptions" or "chapters" or "metadata"))
        {
            Throw.TypeError(realm.OwningRealm, "The text track kind is not a valid TextTrackKind.");
        }
        return DomFailures.Refuse(realm, "HTMLMediaElement.addTextTrack", "NotSupportedError", "Text track creation is not available.");
    }

    internal static int GetVideoWidth(DomRealm realm, Element element)
    {
        RequireElement(realm, element, "video", "HTMLVideoElement.videoWidth");
        return 0;
    }

    internal static int GetVideoHeight(DomRealm realm, Element element)
    {
        RequireElement(realm, element, "video", "HTMLVideoElement.videoHeight");
        return 0;
    }

    internal static int TrackReadyState(DomRealm realm, Element element)
    {
        RequireElement(realm, element, "track", "HTMLTrackElement.readyState");
        return 0;
    }

    internal static JsValue Track(DomRealm realm, Element element)
    {
        RequireElement(realm, element, "track", "HTMLTrackElement.track");
        return DomFailures.Refuse(realm, "HTMLTrackElement.track", "NotSupportedError", "Text track creation is not available.");
    }

    private static void RequireElement(DomRealm realm, Element element, string localName, string member)
    {
        if (!EventDom.IsHtml(element, localName)) Throw.TypeError(realm.OwningRealm, "Illegal invocation of " + member);
    }

    internal JsValue Controller(DomRealm realm)
        => DomFailures.Refuse(realm, "HTMLMediaElement.controller", "NotSupportedError", "Media controllers are not available.");

    internal JsValue StartDate(DomRealm realm)
        => DomFailures.Refuse(realm, "HTMLMediaElement.startDate", "NotSupportedError", "No decoded media timeline is available.");

    private static void RequireFinite(DomRealm realm, double value, string member)
    {
        if (!double.IsFinite(value)) Throw.TypeError(realm.OwningRealm, "HTMLMediaElement." + member + " requires a finite number.");
        realm.Engine.Constraints.Check();
    }

    private void QueueEvent(DomRealm realm, string name)
    {
        var target = realm.WrapNode(_element);
        realm.Engine.Tasks.Post(() => ActivationBehaviors.Fire(target, name, bubbles: false, composed: false));
    }
}

/// <summary>An immutable empty range snapshot: no buffering, seeking or playback has occurred.</summary>
internal sealed class BrowserTimeRanges
{
    internal static readonly BrowserTimeRanges Empty = new();
    private BrowserTimeRanges() { }
    internal int Length => 0;
    internal double Start(DomRealm realm, uint _)
        => Fail(realm, "TimeRanges.start");
    internal double End(DomRealm realm, uint _)
        => Fail(realm, "TimeRanges.end");
    private static double Fail(DomRealm realm, string member)
    {
        DomFailures.Refuse(realm, member, "IndexSizeError", "The range index is outside the empty range list.");
        return 0;
    }
}

// Separate concrete brands for the contract, with no invented decoder output.
internal sealed class BrowserAudioTrackList
{
    internal int Length => 0;
    internal object? GetItem(uint _) => null;
    internal object? GetTrackById(string _) => null;
}

internal sealed class BrowserVideoTrackList
{
    internal int Length => 0;
    internal int SelectedIndex => -1;
    internal object? GetItem(uint _) => null;
    internal object? GetTrackById(string _) => null;
}

internal sealed class BrowserTextTrackList
{
    internal int Length => 0;
    internal object? GetItem(uint _) => null;
    internal object? GetTrackById(string _) => null;
}

#pragma warning restore CA1822
