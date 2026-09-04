using System.IO;

namespace Jint.Profiling;

/// <summary>
/// Renders a <see cref="SampledProfile"/> as a Firefox Profiler <em>processed profile</em>: the columnar
/// document <see href="https://profiler.firefox.com">profiler.firefox.com</see> opens directly, made of a
/// <c>samples</c> table indexing a <c>stackTable</c>, which indexes a <c>frameTable</c>, which indexes a
/// <c>funcTable</c>, with every string in the document interned into one <c>stringArray</c>.
/// </summary>
/// <remarks>
/// <para>
/// The document declares <c>preprocessedProfileVersion</c> 51, which is the version whose shape this
/// writer emits — a stack table keyed by <c>prefix</c>, per-thread string arrays, and a frame table
/// carrying its own category. The viewer upgrades an older document to whatever it currently reads, so
/// naming the version the writer actually produces is what keeps it readable as the format moves on;
/// claiming a newer one would make the upgrader skip the very steps that adapt this shape.
/// </para>
/// <para>
/// Written by hand for the reason <see cref="ProfileJson"/> gives.
/// </para>
/// </remarks>
internal static class FirefoxProfileWriter
{
    /// <summary>
    /// The Gecko (unprocessed) format version, and the processed format version, that pair with the shape
    /// emitted here.
    /// </summary>
    private const int GeckoProfileVersion = 29;

    private const int ProcessedProfileVersion = 51;

    internal static void Write(TextWriter writer, SampledProfile profile)
    {
        var strings = new List<string>();
        var stringIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

        writer.Write("{\"meta\":{\"interval\":");
        ProfileJson.WriteMilliseconds(writer, ResolveInterval(profile));
        writer.Write(",\"startTime\":");
        ProfileJson.WriteNumber(writer, profile._startUnixMilliseconds);
        writer.Write(",\"profilingStartTime\":0,\"profilingEndTime\":");
        ProfileJson.WriteMilliseconds(writer, profile._durationMilliseconds);
        writer.Write(",\"processType\":0,\"product\":\"Jint\",\"importedFrom\":\"Jint\",\"stackwalk\":0,\"symbolicated\":true,\"markerSchema\":[],\"sampleUnits\":{\"time\":\"ms\",\"eventDelay\":\"ms\",\"threadCPUDelta\":\"ns\"},\"version\":");
        ProfileJson.WriteNumber(writer, GeckoProfileVersion);
        writer.Write(",\"preprocessedProfileVersion\":");
        ProfileJson.WriteNumber(writer, ProcessedProfileVersion);

        // Index 0 must be the grey one: the format calls the first grey category the default, and a frame
        // that has no category of its own is shown as that.
        writer.Write(",\"categories\":[");
        writer.Write("{\"name\":\"Other\",\"color\":\"grey\",\"subcategories\":[\"Other\"]},");
        writer.Write("{\"name\":\"Script\",\"color\":\"yellow\",\"subcategories\":[\"Other\"]},");
        writer.Write("{\"name\":\"Built-in\",\"color\":\"blue\",\"subcategories\":[\"Other\"]},");
        writer.Write("{\"name\":\"Host interop\",\"color\":\"orange\",\"subcategories\":[\"Other\"]}]");
        writer.Write("},\"libs\":[],\"threads\":[{");

        writer.Write("\"processType\":\"default\",\"processStartupTime\":0,\"processShutdownTime\":");
        ProfileJson.WriteMilliseconds(writer, profile._durationMilliseconds);
        writer.Write(",\"registerTime\":0,\"unregisterTime\":");
        ProfileJson.WriteMilliseconds(writer, profile._durationMilliseconds);
        writer.Write(",\"pausedRanges\":[],\"showMarkersInTimeline\":false,\"name\":\"Script\",\"isMainThread\":true,\"processName\":\"Jint\",\"pid\":\"1\",\"tid\":\"1\",");

        WriteSamples(writer, profile);
        writer.Write(",\"markers\":{\"data\":[],\"name\":[],\"startTime\":[],\"endTime\":[],\"phase\":[],\"category\":[],\"length\":0}");
        WriteStackTable(writer, profile);
        WriteFrameTable(writer, profile);
        WriteFuncTable(writer, profile, strings, stringIndexes);
        writer.Write(",\"resourceTable\":{\"lib\":[],\"name\":[],\"host\":[],\"type\":[],\"length\":0}");
        writer.Write(",\"nativeSymbols\":{\"libIndex\":[],\"address\":[],\"name\":[],\"functionSize\":[],\"length\":0}");

        // Last, because interning the function names and files is what fills it.
        writer.Write(",\"stringArray\":[");
        for (var i = 0; i < strings.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            ProfileJson.WriteString(writer, strings[i]);
        }

        writer.Write("]}]}");
    }

    /// <summary>
    /// The interval to declare. The configured one, or — when the host asked for a sample at every check
    /// point and there is therefore no configured interval — the rate the thread actually was sampled at,
    /// which is what the field means.
    /// </summary>
    private static double ResolveInterval(SampledProfile profile)
    {
        var configured = profile.Interval.TotalMilliseconds;
        if (configured > 0)
        {
            return configured;
        }

        var samples = profile.SampleCount;
        return samples > 0 ? Math.Max(profile._durationMilliseconds / samples, 0.0001) : 1;
    }

    /// <summary>
    /// The samples table. The sampler fires at the engine's check points rather than on a timer, so the gap
    /// between two samples varies, and a weight of one each would under-report exactly the stacks that were
    /// running while the engine could not be interrupted. Each sample therefore weighs the number of whole
    /// intervals it covers — at least one — so a gap is charged to the stack observed before it, which is
    /// the call site the engine went into.
    /// </summary>
    /// <remarks>
    /// <b>What that costs a test that asserts a share of weight.</b> The sample <i>count</i> is deterministic
    /// — the sampler fires at check points, so it is a function of the work and not of the machine — but the
    /// weights are not: one descheduled moment is billed whole to whichever stack was observed before it, so
    /// on a small profile a single stall can own a large fraction of the total. Measured: a profile of 318
    /// samples has about four units of weight outside its hot loop, and roughly seventy-five landing anywhere
    /// else — a stall of about a millisecond and a half — moves the loop's share below 0.8. The lever is the
    /// denominator, not the floor: <c>Jint.Tests/Runtime/SamplingProfilerTests.DominantHotLoop</c> and
    /// <c>Jint.Tests.DevTools/Session/ProfilerSessionTests.DominantWorkload</c> exist for that reason and
    /// carry the numbers, and any new assertion over a share belongs on one of them rather than on a profile
    /// sized for a different question.
    /// </remarks>
    private static void WriteSamples(TextWriter writer, SampledProfile profile)
    {
        var stacks = profile._sampleStacks;
        var times = profile._sampleTimes;
        var interval = ResolveInterval(profile);

        writer.Write("\"samples\":{\"stack\":[");
        for (var i = 0; i < stacks.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            ProfileJson.WriteNumber(writer, stacks[i]);
        }

        writer.Write("],\"time\":[");
        for (var i = 0; i < times.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            ProfileJson.WriteMilliseconds(writer, times[i]);
        }

        writer.Write("],\"weight\":[");
        for (var i = 0; i < times.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            var until = i + 1 < times.Length ? times[i + 1] : profile._durationMilliseconds;
            var intervals = (long) Math.Round(Math.Max(until - times[i], 0) / interval, MidpointRounding.AwayFromZero);
            ProfileJson.WriteNumber(writer, Math.Max(intervals, 1));
        }

        writer.Write("],\"weightType\":\"samples\",\"length\":");
        ProfileJson.WriteNumber(writer, stacks.Length);
        writer.Write('}');
    }

    private static void WriteStackTable(TextWriter writer, SampledProfile profile)
    {
        var stacks = profile._stacks;
        var frames = profile._frames;
        var categories = profile._funcCategories;

        writer.Write(",\"stackTable\":{\"frame\":[");
        for (var i = 0; i < stacks.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            ProfileJson.WriteNumber(writer, stacks[i].Frame);
        }

        writer.Write("],\"category\":[");
        for (var i = 0; i < stacks.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            ProfileJson.WriteNumber(writer, (int) categories[frames[stacks[i].Frame].Func]);
        }

        writer.Write("],\"subcategory\":[");
        for (var i = 0; i < stacks.Length; i++)
        {
            writer.Write(i > 0 ? ",0" : "0");
        }

        writer.Write("],\"prefix\":[");
        for (var i = 0; i < stacks.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            var prefix = stacks[i].Prefix;
            if (prefix < 0)
            {
                writer.Write("null");
            }
            else
            {
                ProfileJson.WriteNumber(writer, prefix);
            }
        }

        writer.Write("],\"length\":");
        ProfileJson.WriteNumber(writer, stacks.Length);
        writer.Write('}');
    }

    private static void WriteFrameTable(TextWriter writer, SampledProfile profile)
    {
        var frames = profile._frames;
        var categories = profile._funcCategories;

        // -1 is the format's "no address" sentinel, which is what a frame that was never a machine
        // instruction has; it is also what keeps the document out of the symbolication path.
        writer.Write(",\"frameTable\":{\"address\":[");
        for (var i = 0; i < frames.Length; i++)
        {
            writer.Write(i > 0 ? ",-1" : "-1");
        }

        writer.Write("],\"inlineDepth\":[");
        for (var i = 0; i < frames.Length; i++)
        {
            writer.Write(i > 0 ? ",0" : "0");
        }

        writer.Write("],\"category\":[");
        for (var i = 0; i < frames.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            ProfileJson.WriteNumber(writer, (int) categories[frames[i].Func]);
        }

        writer.Write("],\"subcategory\":[");
        for (var i = 0; i < frames.Length; i++)
        {
            writer.Write(i > 0 ? ",0" : "0");
        }

        writer.Write("],\"func\":[");
        for (var i = 0; i < frames.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            ProfileJson.WriteNumber(writer, frames[i].Func);
        }

        writer.Write("],\"nativeSymbol\":[");
        WriteNulls(writer, frames.Length);

        writer.Write("],\"innerWindowID\":[");
        WriteNulls(writer, frames.Length);

        writer.Write("],\"implementation\":[");
        WriteNulls(writer, frames.Length);

        writer.Write("],\"line\":[");
        for (var i = 0; i < frames.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            WriteOptionalNumber(writer, frames[i].Line < 0 ? null : frames[i].Line);
        }

        writer.Write("],\"column\":[");
        for (var i = 0; i < frames.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            WriteOptionalNumber(writer, frames[i].Column < 0 ? null : frames[i].Column);
        }

        writer.Write("],\"length\":");
        ProfileJson.WriteNumber(writer, frames.Length);
        writer.Write('}');
    }

    private static void WriteFuncTable(
        TextWriter writer,
        SampledProfile profile,
        List<string> strings,
        Dictionary<string, int> stringIndexes)
    {
        var funcs = profile._funcs;
        var categories = profile._funcCategories;

        writer.Write(",\"funcTable\":{\"name\":[");
        for (var i = 0; i < funcs.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            ProfileJson.WriteNumber(writer, InternString(strings, stringIndexes, funcs[i].Name));
        }

        // isJS marks a function the script wrote; relevantForJS marks one that did not come from script but
        // belongs in a JavaScript-only view anyway, which is exactly what a built-in and a host callable are.
        writer.Write("],\"isJS\":[");
        for (var i = 0; i < funcs.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            writer.Write(categories[i] == ProfileFrameCategory.Script ? "true" : "false");
        }

        writer.Write("],\"relevantForJS\":[");
        for (var i = 0; i < funcs.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            writer.Write(categories[i] == ProfileFrameCategory.Script ? "false" : "true");
        }

        writer.Write("],\"resource\":[");
        for (var i = 0; i < funcs.Length; i++)
        {
            writer.Write(i > 0 ? ",-1" : "-1");
        }

        writer.Write("],\"fileName\":[");
        for (var i = 0; i < funcs.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            var file = funcs[i].File;
            if (file is null)
            {
                writer.Write("null");
            }
            else
            {
                ProfileJson.WriteNumber(writer, InternString(strings, stringIndexes, file));
            }
        }

        writer.Write("],\"lineNumber\":[");
        for (var i = 0; i < funcs.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            WriteOptionalNumber(writer, funcs[i].Line);
        }

        writer.Write("],\"columnNumber\":[");
        for (var i = 0; i < funcs.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            WriteOptionalNumber(writer, funcs[i].Column);
        }

        writer.Write("],\"length\":");
        ProfileJson.WriteNumber(writer, funcs.Length);
        writer.Write('}');
    }

    private static int InternString(List<string> strings, Dictionary<string, int> indexes, string value)
    {
        if (indexes.TryGetValue(value, out var index))
        {
            return index;
        }

        index = strings.Count;
        strings.Add(value);
        indexes.Add(value, index);
        return index;
    }

    private static void WriteNulls(TextWriter writer, int count)
    {
        for (var i = 0; i < count; i++)
        {
            writer.Write(i > 0 ? ",null" : "null");
        }
    }

    private static void WriteOptionalNumber(TextWriter writer, int? value)
    {
        if (value is { } number)
        {
            ProfileJson.WriteNumber(writer, number);
        }
        else
        {
            writer.Write("null");
        }
    }
}
