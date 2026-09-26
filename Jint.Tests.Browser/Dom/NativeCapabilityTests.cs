#nullable enable

using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeCapabilityTests
{
    [Test]
    public void MediaStateDefaultsAreUnloadedAndMutingReadsOnlyTheContentAttribute()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var element = Document.CreateHtml().CreateElement("video");
        element.SetAttributeNS("urn:test", "muted", "");
        var state = BrowserMediaState.Of(realm, element);
        state.Muted.Should().BeFalse();
        element.SetAttributeNS(null, "muted", "");
        state.Muted.Should().BeTrue();
        element.RemoveAttributeNS(null, "muted");
        state.Muted.Should().BeFalse();
        state.SetMuted(realm, true);
        state.Muted.Should().BeTrue();
        state.Paused.Should().BeTrue();
        state.Seeking.Should().BeFalse();
        state.Ended.Should().BeFalse();
        state.Duration.Should().Be(double.NaN);
        state.ReadyState.Should().Be(0);
        state.NetworkState.Should().Be(0);
        state.CurrentSrc.Should().BeEmpty();
        state.VideoWidth.Should().Be(0);
        state.VideoHeight.Should().Be(0);
        state.CanPlayType("video/mp4").Should().BeEmpty();
    }

    [Test]
    public void ScriptWrittenMediaValuesSurviveAdoptionButDoNotCopyToClones()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var element = Document.CreateHtml().CreateElement("audio");
        var state = BrowserMediaState.Of(realm, element);
        state.SetVolume(realm, .25);
        state.SetCurrentTime(realm, -2);
        state.SetDefaultPlaybackRate(realm, 2);
        state.SetPlaybackRate(realm, .5);
        Document.CreateHtml().AdoptNode(element);
        BrowserMediaState.Of(realm, element).Should().BeSameAs(state);
        state.Volume.Should().Be(.25);
        state.CurrentTime.Should().Be(-2);
        state.DefaultPlaybackRate.Should().Be(2);
        state.PlaybackRate.Should().Be(.5);
        var clone = BrowserMediaState.Of(realm, (Element) element.CloneNode());
        clone.Should().NotBeSameAs(state);
        clone.Volume.Should().Be(1);
        clone.CurrentTime.Should().Be(0);
        clone.AudioTracks.Should().NotBeSameAs(state.AudioTracks);
        state.AudioTracks.Should().BeSameAs(state.AudioTracks);
        state.VideoTracks.SelectedIndex.Should().Be(-1);
        state.TextTracks.Length.Should().Be(0);
        state.Buffered.Length.Should().Be(0);
        state.Played.Length.Should().Be(0);
        state.Seekable.Length.Should().Be(0);
    }

    [Test]
    public void MediaChangesQueueRealEventsAndDoNotClaimPlayback()
    {
        using var engine = new Engine(options => options.UseWebApis());
        DomBindings.Install(engine);
        var realm = DomRealm.Of(engine);
        var element = Document.CreateHtml().CreateElement("audio");
        var state = BrowserMediaState.Of(realm, element);
        engine.SetValue("media", realm.WrapNode(element));
        engine.Execute("var events=[]; media.addEventListener('volumechange', e=>events.push([e.type,e.bubbles,e.isTrusted])); media.addEventListener('ratechange', e=>events.push([e.type,e.bubbles,e.isTrusted]));");
        state.SetVolume(realm, .5);
        state.SetVolume(realm, .5);
        state.SetDefaultPlaybackRate(realm, 2);
        engine.Evaluate("events.length").Should().Be(0);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("JSON.stringify(events)").AsString().Should().Be("[[\"volumechange\",false,true],[\"ratechange\",false,true]]");
        engine.SetValue("playResult", state.Play(realm));
        engine.Execute("var playError=''; playResult.catch(e=>playError=e.name);");
        engine.Tasks.ProcessTasks();
        engine.Evaluate("playError").AsString().Should().Be("NotSupportedError");
        state.Paused.Should().BeTrue();
        state.ReadyState.Should().Be(0);
    }

    [Test]
    public void UnsupportedCanvasAndInvalidMediaCallsRefuseWithoutChangingState()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var canvas = document.CreateElement("canvas");
        BrowserCanvasMembers.GetContext(realm, canvas, [JsString.Create("2d")]).Should().Be(JsValue.Null);
        BrowserCanvasMembers.ProbablySupportsContext(realm, canvas, [JsString.Create("2d")]).Should().Be(JsBoolean.False);
        ErrorName(() => BrowserCanvasMembers.ToDataUrl(realm, canvas, [])).Should().Be("NotSupportedError");
        ErrorName(() => BrowserCanvasMembers.SetContext(realm, canvas, [JsValue.Null])).Should().Be("NotSupportedError");
        var state = BrowserMediaState.Of(realm, document.CreateElement("audio"));
        ErrorName(() => state.SetVolume(realm, 2)).Should().Be("IndexSizeError");
        ErrorName(() => state.SetCurrentTime(realm, double.NaN)).Should().Be("TypeError");
        ErrorName(() => state.SetPlaybackRate(realm, -1)).Should().Be("NotSupportedError");
        state.Volume.Should().Be(1);
        state.CurrentTime.Should().Be(0);
        state.PlaybackRate.Should().Be(1);
        ErrorName(() => BrowserMediaState.Of(realm, document.CreateElementNS("urn:test", "audio"))).Should().Be("TypeError");
        ErrorName(() => BrowserCanvasMembers.GetContext(realm, document.CreateElement("div"), [JsString.Create("2d")])).Should().Be("TypeError");
    }

    private static string ErrorName(Action action)
    {
        var error = Caught.Exception(action).Should().BeOfType<JavaScriptException>().Subject;
        return error.Error.AsObject().Get("name").AsString();
    }
}
