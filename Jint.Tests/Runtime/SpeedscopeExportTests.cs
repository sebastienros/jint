#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Jint.Native;
using Jint.Native.Json;
using Jint.Native.Object;
using Jint.Profiling;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the exported document against the
/// <see href="https://www.speedscope.app/file-format-schema.json">speedscope file format schema</see>:
/// <c>FileFormat.File</c> requires <c>$schema</c>, <c>shared.frames</c> and <c>profiles</c>;
/// <c>FileFormat.EventedProfile</c> requires <c>type</c>, <c>name</c>, <c>unit</c>, <c>startValue</c>,
/// <c>endValue</c> and <c>events</c>; each event requires <c>type</c> (<c>"O"</c> or <c>"C"</c>),
/// <c>frame</c> and <c>at</c>; and <c>FileFormat.Frame</c> requires <c>name</c>, with <c>file</c>,
/// <c>line</c> and <c>col</c> optional.
/// <para>
/// The document is parsed back with Jint's own <see cref="JsonParser"/> rather than compared as text, so
/// what is asserted is the structure a consumer sees and not a byte layout nobody promised.
/// </para>
/// </summary>
public class SpeedscopeExportTests
{
    private const string SchemaUrl = "https://www.speedscope.app/file-format-schema.json";

    private static ScriptProfile Profile(string code, string source = "profiled.js", Action<Engine>? setup = null)
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);
        setup?.Invoke(engine);
        engine.Diagnostics.StartProfiling();
        engine.Execute(code, source);
        return engine.Diagnostics.StopProfiling();
    }

    private static string ToJson(ScriptProfile profile)
    {
        var writer = new StringWriter();
        profile.WriteSpeedscopeJson(writer);
        return writer.ToString();
    }

    private static ObjectInstance ParseBack(string json)
    {
        // A parser engine of its own, so nothing about the profiled engine can flatter the result.
        var parsed = new JsonParser(new Engine()).Parse(json);
        return parsed.Should().BeAssignableTo<ObjectInstance>().Subject;
    }

    private static ObjectInstance At(JsValue array, int index) =>
        array.Get(index.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Should().BeAssignableTo<ObjectInstance>().Subject;

    private static int Length(JsValue array) => (int) array.Get("length").AsNumber();

    private const string SampleScript = """
        function leaf() { return 1; }
        function middle() { return leaf(); }
        function root() { return middle() + leaf(); }
        root();
        """;

    [Fact]
    public void EmitsTheRequiredTopLevelMembers()
    {
        var document = ParseBack(ToJson(Profile(SampleScript)));

        document.Get("$schema").AsString().Should().Be(SchemaUrl);
        document.Get("exporter").AsString().Should().Be("Jint");
        document.Get("name").IsString().Should().BeTrue();
        document.Get("activeProfileIndex").AsNumber().Should().Be(0);
        document.Get("shared").IsObject().Should().BeTrue();
        document.Get("profiles").IsArray().Should().BeTrue();
    }

    [Fact]
    public void EmitsExactlyOneEventedProfileInNanoseconds()
    {
        var profile = Profile(SampleScript);
        var document = ParseBack(ToJson(profile));

        var profiles = document.Get("profiles");
        Length(profiles).Should().Be(1);

        var evented = At(profiles, 0);
        evented.Get("type").AsString().Should().Be("evented");
        evented.Get("unit").AsString().Should().Be("nanoseconds");
        evented.Get("name").IsString().Should().BeTrue();
        evented.Get("startValue").AsNumber().Should().Be(0);
        evented.Get("endValue").AsNumber().Should().Be(profile.DurationNanoseconds);
        evented.Get("events").IsArray().Should().BeTrue();
    }

    [Fact]
    public void SharedFramesMirrorTheProfileFrames()
    {
        var profile = Profile(SampleScript);
        var frames = ParseBack(ToJson(profile)).Get("shared").Get("frames");

        Length(frames).Should().Be(profile.Frames.Count);
        profile.Frames.Should().NotBeEmpty();

        for (var i = 0; i < profile.Frames.Count; i++)
        {
            var expected = profile.Frames[i];
            var actual = At(frames, i);

            actual.Get("name").AsString().Should().Be(expected.Name);

            if (expected.File is null)
            {
                actual.HasProperty("file").Should().BeFalse("an absent optional member is omitted, not null");
                actual.HasProperty("line").Should().BeFalse();
                actual.HasProperty("col").Should().BeFalse();
            }
            else
            {
                actual.Get("file").AsString().Should().Be(expected.File);
                actual.Get("line").AsNumber().Should().Be(expected.Line!.Value);
                actual.Get("col").AsNumber().Should().Be(expected.Column!.Value);
            }
        }
    }

    [Fact]
    public void EventsAreBalancedAndNonDecreasing()
    {
        var profile = Profile(SampleScript);
        var document = ParseBack(ToJson(profile));

        var events = At(document.Get("profiles"), 0).Get("events");
        var count = Length(events);
        count.Should().Be(profile.Events.Count).And.BePositive();

        var frameCount = Length(document.Get("shared").Get("frames"));
        var open = new Stack<int>();
        var previous = 0d;

        for (var i = 0; i < count; i++)
        {
            var e = At(events, i);
            var type = e.Get("type").AsString();
            var frame = (int) e.Get("frame").AsNumber();
            var at = e.Get("at").AsNumber();

            type.Should().BeOneOf("O", "C");
            frame.Should().BeInRange(0, frameCount - 1);
            at.Should().BeGreaterThanOrEqualTo(previous);
            previous = at;

            if (type == "O")
            {
                open.Push(frame);
            }
            else
            {
                open.Should().NotBeEmpty();
                open.Pop().Should().Be(frame);
            }
        }

        open.Should().BeEmpty();
        previous.Should().BeLessThanOrEqualTo(At(document.Get("profiles"), 0).Get("endValue").AsNumber());
    }

    [Fact]
    public void AnEmptyProfileIsStillAValidDocument()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);
        engine.Diagnostics.StartProfiling();
        var profile = engine.Diagnostics.StopProfiling();

        var document = ParseBack(ToJson(profile));

        Length(document.Get("shared").Get("frames")).Should().Be(0);
        Length(At(document.Get("profiles"), 0).Get("events")).Should().Be(0);
        At(document.Get("profiles"), 0).Get("endValue").AsNumber().Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// A function's <c>name</c> is a JS string, so it can hold anything JSON has to escape — including an
    /// unpaired surrogate, which has no UTF-8 encoding at all and would otherwise make the exported bytes
    /// undecodable.
    /// </summary>
    [Fact]
    public void HostileFunctionNamesSurviveTheRoundTrip()
    {
        const string Hostile = "a\"b\\c\nde\ud800f";

        var profile = Profile("""
            var f = function () { return 1; };
            Object.defineProperty(f, 'name', { value: 'a"b\\c\nde\ud800f' });
            f();
            """);

        var index = IndexOfFrameNamed(profile, Hostile);
        index.Should().BeGreaterThanOrEqualTo(0);

        var json = ToJson(profile);
        json.Should().NotContain("\ud800", "an unpaired surrogate must be escaped rather than emitted raw");

        var frames = ParseBack(json).Get("shared").Get("frames");
        At(frames, index).Get("name").AsString().Should().Be(Hostile);
    }

    [Fact]
    public void APairedSurrogateIsEmittedAsIs()
    {
        // U+1F600, a legitimate astral character, must not be mangled by the unpaired-surrogate handling.
        // Built from code units on both sides so the assertion does not depend on this file's encoding.
        const string Emoji = "\uD83D\uDE00";

        var profile = Profile("""
            var f = function () { return 1; };
            Object.defineProperty(f, 'name', { value: String.fromCharCode(0xD83D, 0xDE00) });
            f();
            """);

        var index = IndexOfFrameNamed(profile, Emoji);
        index.Should().BeGreaterThanOrEqualTo(0);

        var json = ToJson(profile);
        json.Should().Contain(Emoji);
        At(ParseBack(json).Get("shared").Get("frames"), index).Get("name").AsString().Should().Be(Emoji);
    }

    private static int IndexOfFrameNamed(ScriptProfile profile, string name)
    {
        for (var i = 0; i < profile.Frames.Count; i++)
        {
            if (profile.Frames[i].Name == name)
            {
                return i;
            }
        }

        return -1;
    }

    [Fact]
    public void TheStreamOverloadWritesUtf8WithoutABomAndLeavesTheStreamOpen()
    {
        var profile = Profile(SampleScript);

        using var stream = new MemoryStream();
        profile.WriteSpeedscopeJson(stream);

        var bytes = stream.ToArray();
        bytes[0].Should().Be((byte) '{', "a byte-order mark is not part of the document");

        // Still writable, so the overload did not dispose what it was handed.
        stream.WriteByte((byte) ' ');

        new UTF8Encoding(false).GetString(bytes).Should().Be(ToJson(profile));
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        var profile = Profile(SampleScript);

        Assert.Throws<ArgumentNullException>(() => profile.WriteSpeedscopeJson((TextWriter) null!));
        Assert.Throws<ArgumentNullException>(() => profile.WriteSpeedscopeJson((Stream) null!));
    }

    [Fact]
    public void ATruncatedProfileStillExportsABalancedDocument()
    {
        var engine = new Engine(options =>
        {
            options.Profiling.Enabled = true;
            options.Profiling.MaxEvents = 10;
        });

        engine.Diagnostics.StartProfiling();
        engine.Execute("function down(n) { return n === 0 ? 0 : down(n - 1); } down(50);");
        var profile = engine.Diagnostics.StopProfiling();
        profile.Truncated.Should().BeTrue();

        var events = At(ParseBack(ToJson(profile)).Get("profiles"), 0).Get("events");
        var depth = 0;
        for (var i = 0; i < Length(events); i++)
        {
            depth += At(events, i).Get("type").AsString() == "O" ? 1 : -1;
            depth.Should().BeGreaterThanOrEqualTo(0);
        }

        depth.Should().Be(0, "truncation closes what it leaves open");
    }
}
