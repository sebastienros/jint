using System.IO;

namespace Jint.Profiling;

/// <summary>
/// Renders a <see cref="ScriptProfile"/> as a speedscope <c>evented</c> profile, per
/// <see href="https://www.speedscope.app/file-format-schema.json">the published schema</see>: a
/// <c>FileFormat.File</c> whose required members are <c>$schema</c>, <c>shared.frames</c> and
/// <c>profiles</c>, carrying a single <c>FileFormat.EventedProfile</c> whose required members are
/// <c>type</c>, <c>name</c>, <c>unit</c>, <c>startValue</c>, <c>endValue</c> and <c>events</c>, each event
/// being an <c>{ type: "O" | "C", frame, at }</c>.
/// </summary>
internal static class SpeedscopeWriter
{
    private const string SchemaUrl = "https://www.speedscope.app/file-format-schema.json";
    private const string ProfileName = "Jint";

    internal static void Write(
        TextWriter writer,
        ScriptProfileFrame[] frames,
        ScriptProfileEvent[] events,
        long durationNanoseconds)
    {
        writer.Write("{\"$schema\":\"");
        writer.Write(SchemaUrl);
        writer.Write("\",\"exporter\":\"Jint\",\"name\":\"");
        writer.Write(ProfileName);
        writer.Write("\",\"activeProfileIndex\":0,\"shared\":{\"frames\":[");

        for (var i = 0; i < frames.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            WriteFrame(writer, in frames[i]);
        }

        writer.Write("]},\"profiles\":[{\"type\":\"evented\",\"name\":\"");
        writer.Write(ProfileName);
        writer.Write("\",\"unit\":\"nanoseconds\",\"startValue\":0,\"endValue\":");
        ProfileJson.WriteNumber(writer, durationNanoseconds);
        writer.Write(",\"events\":[");

        for (var i = 0; i < events.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            var e = events[i];
            writer.Write(e.Kind == ScriptProfileEventKind.Open ? "{\"type\":\"O\",\"frame\":" : "{\"type\":\"C\",\"frame\":");
            ProfileJson.WriteNumber(writer, e.FrameIndex);
            writer.Write(",\"at\":");
            ProfileJson.WriteNumber(writer, e.TimestampNanoseconds);
            writer.Write('}');
        }

        writer.Write("]}]}");
    }

    private static void WriteFrame(TextWriter writer, in ScriptProfileFrame frame)
    {
        writer.Write("{\"name\":");
        ProfileJson.WriteString(writer, frame.Name);

        if (frame.File is not null)
        {
            writer.Write(",\"file\":");
            ProfileJson.WriteString(writer, frame.File);
        }

        if (frame.Line is { } line)
        {
            writer.Write(",\"line\":");
            ProfileJson.WriteNumber(writer, line);
        }

        if (frame.Column is { } column)
        {
            writer.Write(",\"col\":");
            ProfileJson.WriteNumber(writer, column);
        }

        writer.Write('}');
    }
}
