#if NET8_0_OR_GREATER
#nullable enable

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Jint.Runtime;
using Jint.WebApi.Encoding;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The legacy encodings <c>TextDecoder</c> understands beyond the three Unicode ones: the single-byte
/// encodings of https://encoding.spec.whatwg.org/#legacy-single-byte-encodings, <c>x-user-defined</c> and
/// <c>replacement</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two of these facts are checked exhaustively rather than by sample, and both read the Encoding Standard's
/// own data files (vendored under <c>tools/whatwg-encoding/</c> and embedded into this assembly): every
/// label the specification lists resolves to the encoding it names, and every one of the 28 single-byte
/// encodings decodes all 256 byte values the way its index table says. The generated C# tables are the
/// thing under test and the JSON is the reference they are checked against, so a transcription slip
/// anywhere in the 3,456 index entries fails a test rather than waiting for a script to notice.
/// </para>
/// </remarks>
public class LegacyEncodingTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Encoding));

    private static string Decode(Engine engine, string label, params int[] bytes)
    {
        engine.SetValue("label", label);
        return engine.Evaluate($"new TextDecoder(label).decode(new Uint8Array([{string.Join(",", bytes)}]))").AsString();
    }

    #region Labels

    // https://encoding.spec.whatwg.org/#names-and-labels, one row per single-byte encoding. The name the
    // `encoding` getter reports is the specification's name, ASCII-lowercased.
    [TestCase("ibm866", "ibm866")]
    [TestCase("866", "ibm866")]
    [TestCase("csibm866", "ibm866")]
    [TestCase("iso-8859-2", "iso-8859-2")]
    [TestCase("latin2", "iso-8859-2")]
    [TestCase("l2", "iso-8859-2")]
    [TestCase("iso_8859-2:1987", "iso-8859-2")]
    [TestCase("iso-8859-3", "iso-8859-3")]
    [TestCase("latin3", "iso-8859-3")]
    [TestCase("iso-8859-4", "iso-8859-4")]
    [TestCase("latin4", "iso-8859-4")]
    [TestCase("iso-8859-5", "iso-8859-5")]
    [TestCase("cyrillic", "iso-8859-5")]
    [TestCase("iso-8859-6", "iso-8859-6")]
    [TestCase("arabic", "iso-8859-6")]
    [TestCase("iso-8859-7", "iso-8859-7")]
    [TestCase("greek", "iso-8859-7")]
    [TestCase("sun_eu_greek", "iso-8859-7")]
    [TestCase("iso-8859-8", "iso-8859-8")]
    [TestCase("hebrew", "iso-8859-8")]
    [TestCase("visual", "iso-8859-8")]
    [TestCase("iso-8859-10", "iso-8859-10")]
    [TestCase("latin6", "iso-8859-10")]
    [TestCase("iso-8859-13", "iso-8859-13")]
    [TestCase("iso-8859-14", "iso-8859-14")]
    [TestCase("iso-8859-15", "iso-8859-15")]
    [TestCase("l9", "iso-8859-15")]
    [TestCase("iso-8859-16", "iso-8859-16")]
    [TestCase("koi8-r", "koi8-r")]
    [TestCase("koi", "koi8-r")]
    [TestCase("koi8-u", "koi8-u")]
    [TestCase("koi8-ru", "koi8-u")]
    [TestCase("macintosh", "macintosh")]
    [TestCase("mac", "macintosh")]
    [TestCase("x-mac-roman", "macintosh")]
    [TestCase("windows-874", "windows-874")]
    [TestCase("tis-620", "windows-874")]
    [TestCase("iso-8859-11", "windows-874")]
    [TestCase("windows-1250", "windows-1250")]
    [TestCase("x-cp1250", "windows-1250")]
    [TestCase("windows-1251", "windows-1251")]
    [TestCase("windows-1252", "windows-1252")]
    [TestCase("windows-1253", "windows-1253")]
    [TestCase("windows-1254", "windows-1254")]
    [TestCase("iso-8859-9", "windows-1254")]
    [TestCase("latin5", "windows-1254")]
    [TestCase("windows-1255", "windows-1255")]
    [TestCase("windows-1256", "windows-1256")]
    [TestCase("windows-1257", "windows-1257")]
    [TestCase("windows-1258", "windows-1258")]
    [TestCase("x-mac-cyrillic", "x-mac-cyrillic")]
    [TestCase("x-mac-ukrainian", "x-mac-cyrillic")]
    // x-user-defined is not a single-byte encoding, since its decoder is an algorithm rather than an index,
    // but it is named the same way.
    [TestCase("x-user-defined", "x-user-defined")]
    public void ResolvesALegacyLabelToItsEncodingName(string label, string expected)
    {
        var engine = WebEngine();

        engine.SetValue("label", label);
        engine.Evaluate("new TextDecoder(label).encoding").AsString().Should().Be(expected);
    }

    // The classic trap: ISO-8859-1 and every one of its aliases, "ascii" and "us-ascii" included, are labels
    // for windows-1252. The Encoding Standard has no ISO-8859-1 encoding at all.
    [TestCase("iso-8859-1")]
    [TestCase("iso8859-1")]
    [TestCase("iso88591")]
    [TestCase("iso_8859-1")]
    [TestCase("iso_8859-1:1987")]
    [TestCase("latin1")]
    [TestCase("l1")]
    [TestCase("csisolatin1")]
    [TestCase("ascii")]
    [TestCase("us-ascii")]
    [TestCase("ansi_x3.4-1968")]
    [TestCase("cp819")]
    [TestCase("ibm819")]
    [TestCase("iso-ir-100")]
    [TestCase("cp1252")]
    [TestCase("x-cp1252")]
    public void MapsEveryLatin1LabelToWindows1252(string label)
    {
        var engine = WebEngine();

        engine.SetValue("label", label);
        engine.Evaluate("new TextDecoder(label).encoding").AsString().Should().Be("windows-1252");

        // ... and it really is windows-1252 doing the decoding: 0x80 is U+20AC there and a C1 control in
        // ISO-8859-1, which is the difference the label mapping exists to erase.
        Decode(engine, label, 0x80).Should().Be("€");
    }

    [Test]
    public void KeepsIso88598IApartFromIso88598()
    {
        var engine = WebEngine();

        // Two distinct encodings, because ISO-8859-8 influences layout direction and ISO-8859-8-I does not,
        // so the name is preserved even though they share one index.
        foreach (var label in new[] { "iso-8859-8-i", "csiso88598i", "logical" })
        {
            engine.SetValue("label", label);
            engine.Evaluate("new TextDecoder(label).encoding").AsString().Should().Be("iso-8859-8-i");
        }

        foreach (var label in new[] { "iso-8859-8", "iso-8859-8-e", "csiso88598e", "visual", "hebrew" })
        {
            engine.SetValue("label", label);
            engine.Evaluate("new TextDecoder(label).encoding").AsString().Should().Be("iso-8859-8");
        }

        // The shared index: both decode 0xE0 to U+05D0 HEBREW LETTER ALEF.
        Decode(engine, "iso-8859-8", 0xE0).Should().Be("א");
        Decode(engine, "iso-8859-8-i", 0xE0).Should().Be("א");

        // ISO-8859-6 has no "-I" encoding of its own: both of its directional labels name ISO-8859-6, which
        // is the asymmetry the specification calls out.
        foreach (var label in new[] { "iso-8859-6-i", "csiso88596i", "iso-8859-6-e", "csiso88596e" })
        {
            engine.SetValue("label", label);
            engine.Evaluate("new TextDecoder(label).encoding").AsString().Should().Be("iso-8859-6");
        }
    }

    // "Get an encoding" strips leading and trailing ASCII whitespace and matches ASCII case-insensitively.
    [TestCase("WINDOWS-1252")]
    [TestCase("Windows-1252")]
    [TestCase("  windows-1252  ")]
    [TestCase("\t\n\f\r windows-1252 \t\n\f\r")]
    [TestCase("LATIN1")]
    [TestCase(" Latin1\n")]
    public void TrimsAndCaseFoldsALegacyLabel(string label)
    {
        var engine = WebEngine();

        engine.SetValue("label", label);
        engine.Evaluate("new TextDecoder(label).encoding").AsString().Should().Be("windows-1252");
    }

    // U+000B VT and U+00A0 NBSP are not ASCII whitespace, so neither is stripped, and an inner space is not
    // stripped at all.
    [TestCase("windows-1252")]
    [TestCase("windows-1252\u00A0")]
    [TestCase("windows -1252")]
    [TestCase("windows-1252x")]
    [TestCase("windows-125")]
    [TestCase("windows-1249")]
    [TestCase("iso-8859-12")]
    // Nor is the match anything but ASCII case-insensitive: U+017F must not fold onto 's'.
    [TestCase("i\u017Fo-8859-2")]
    public void StillRefusesALabelThatIsNotOne(string label)
    {
        var engine = WebEngine();

        engine.SetValue("label", label);
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TextDecoder(label)"))!
            .Error.Get("name").AsString().Should().Be("RangeError");
    }

    #endregion

    #region Decoding

    // One vector per single-byte index, taken from the index file the specification links for it.
    [TestCase("ibm866", 0x80, 0x0410)]
    [TestCase("ibm866", 0xE0, 0x0440)]
    [TestCase("iso-8859-2", 0xB1, 0x0105)]
    [TestCase("iso-8859-2", 0xE1, 0x00E1)]
    [TestCase("iso-8859-3", 0xA1, 0x0126)]
    [TestCase("iso-8859-4", 0xA1, 0x0104)]
    [TestCase("iso-8859-5", 0xB0, 0x0410)]
    [TestCase("iso-8859-6", 0xC7, 0x0627)]
    [TestCase("iso-8859-7", 0xE1, 0x03B1)]
    [TestCase("iso-8859-8", 0xE0, 0x05D0)]
    [TestCase("iso-8859-8-i", 0xE0, 0x05D0)]
    [TestCase("iso-8859-10", 0xA1, 0x0104)]
    [TestCase("iso-8859-13", 0xA8, 0x00D8)]
    [TestCase("iso-8859-14", 0xA1, 0x1E02)]
    [TestCase("iso-8859-15", 0xA4, 0x20AC)]
    [TestCase("iso-8859-16", 0xA4, 0x20AC)]
    [TestCase("koi8-r", 0xC1, 0x0430)]
    [TestCase("koi8-u", 0xA4, 0x0454)]
    [TestCase("macintosh", 0x80, 0x00C4)]
    [TestCase("windows-874", 0xA1, 0x0E01)]
    [TestCase("windows-874", 0x85, 0x2026)]
    [TestCase("windows-1250", 0x8A, 0x0160)]
    [TestCase("windows-1251", 0xC0, 0x0410)]
    [TestCase("windows-1252", 0x80, 0x20AC)]
    [TestCase("windows-1252", 0x92, 0x2019)]
    // windows-1252 maps its five otherwise-undefined bytes to the C1 controls rather than leaving holes,
    // which is one of the places the Encoding Standard's windows-1252 differs from the vendor's CP1252.
    [TestCase("windows-1252", 0x81, 0x0081)]
    [TestCase("windows-1252", 0x8D, 0x008D)]
    [TestCase("windows-1253", 0xE1, 0x03B1)]
    [TestCase("windows-1254", 0xD0, 0x011E)]
    [TestCase("windows-1255", 0xE0, 0x05D0)]
    [TestCase("windows-1256", 0xC7, 0x0627)]
    [TestCase("windows-1257", 0xC0, 0x0104)]
    [TestCase("windows-1258", 0xC0, 0x00C0)]
    [TestCase("x-mac-cyrillic", 0x80, 0x0410)]
    public void DecodesAByteThroughItsIndex(string label, int b, int expected)
    {
        var engine = WebEngine();

        Decode(engine, label, b).Should().Be(((char) expected).ToString());
    }

    [Test]
    public void PassesAsciiBytesThroughUntouched()
    {
        var engine = WebEngine();

        // Step 2 of the single-byte decoder: an ASCII byte is its own code point, whatever the index says.
        var ascii = new int[0x80];
        var expected = new StringBuilder(0x80);
        for (var i = 0; i < 0x80; i++)
        {
            ascii[i] = i;
            expected.Append((char) i);
        }

        foreach (var label in new[] { "windows-1252", "koi8-r", "iso-8859-7", "x-mac-cyrillic", "x-user-defined" })
        {
            Decode(engine, label, ascii).Should().Be(expected.ToString(), $"{label} must pass ASCII through");
        }
    }

    // Every single-byte index with a hole in it, and one of its unmapped bytes.
    [TestCase("iso-8859-3", 0xA5)]
    [TestCase("iso-8859-6", 0xA1)]
    [TestCase("iso-8859-7", 0xAE)]
    [TestCase("iso-8859-8", 0xBF)]
    [TestCase("iso-8859-8-i", 0xBF)]
    [TestCase("windows-874", 0xDB)]
    [TestCase("windows-1253", 0xAA)]
    [TestCase("windows-1255", 0xD9)]
    [TestCase("windows-1257", 0xA1)]
    public void ReplacesAnUnmappedByteAndThrowsForItWhenFatal(string label, int b)
    {
        var engine = WebEngine();

        // The default error mode substitutes one U+FFFD for the byte and carries on with the rest.
        Decode(engine, label, 0x61, b, 0x62).Should().Be("a\uFFFDb");

        engine.SetValue("label", label);
        engine.SetValue("byte", b);
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TextDecoder(label, { fatal: true }).decode(new Uint8Array([byte]))"))!
            .Error.Get("name").AsString().Should().Be("TypeError");

        // A decoder that threw is usable again afterwards.
        engine.Evaluate("""
            const decoder = new TextDecoder(label, { fatal: true });
            try { decoder.decode(new Uint8Array([byte])); } catch (e) { }
            decoder.decode(new Uint8Array([0x61]));
            """).AsString().Should().Be("a");
    }

    [Test]
    public void NeverStripsAByteOrderMarkForASingleByteEncoding()
    {
        var engine = WebEngine();

        // "Serialize I/O queue" drops a leading U+FEFF only when the encoding is UTF-8 or UTF-16BE/LE, and
        // a legacy decoder never produces one anyway: the three bytes of a UTF-8 BOM are simply three
        // windows-1252 characters, where the same call on a UTF-8 decoder returns the empty string.
        Decode(engine, "windows-1252", 0xEF, 0xBB, 0xBF, 0x61).Should().Be("ï»¿a");

        // ignoreBOM changes nothing here, and it still reports what was asked for.
        engine.Evaluate("new TextDecoder('windows-1252', { ignoreBOM: true }).decode(new Uint8Array([0xEF, 0xBB, 0xBF])).length")
            .AsNumber().Should().Be(3);
        engine.Evaluate("new TextDecoder('windows-1252', { ignoreBOM: true }).ignoreBOM").AsBoolean().Should().BeTrue();
        engine.Evaluate("new TextDecoder('windows-1252').ignoreBOM").AsBoolean().Should().BeFalse();

        // The UTF-16 BOM bytes are just as ordinary here.
        Decode(engine, "koi8-r", 0xFF, 0xFE).Should().Be("ЪЧ");
    }

    #endregion

    #region Streaming

    [Test]
    public void StreamsASingleByteEncodingWithoutHoldingBytesOver()
    {
        var engine = WebEngine();

        // Every byte stands alone, so a streaming chunk never withholds output waiting for a continuation
        // byte, which is the whole difference from the UTF-8 decoder's behaviour on the same call.
        engine.Evaluate("""
            const decoder = new TextDecoder('windows-1252');
            const first = decoder.decode(new Uint8Array([0x61, 0x80]), { stream: true });
            const second = decoder.decode(new Uint8Array([0x92]), { stream: true });
            const third = decoder.decode();
            [first, second, third.length].join('|');
            """).AsString().Should().Be("a€|\u2019|0");
    }

    [Test]
    public void KeepsFatalWorkingAcrossStreamingChunks()
    {
        var engine = WebEngine();

        engine.SetValue("label", "windows-1257");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("""
            const decoder = new TextDecoder(label, { fatal: true });
            decoder.decode(new Uint8Array([0x61]), { stream: true });
            decoder.decode(new Uint8Array([0xA1]), { stream: true });
            """))!.Error.Get("name").AsString().Should().Be("TypeError");
    }

    #endregion

    #region x-user-defined

    [Test]
    public void MapsTheUpperHalfAlgorithmicallyForXUserDefined()
    {
        var engine = WebEngine();

        // https://encoding.spec.whatwg.org/#x-user-defined-decoder: 0xF780 + byte - 0x80, for every byte
        // that is not an ASCII one. There is no index table behind this.
        var bytes = new int[0x80];
        var expected = new StringBuilder(0x80);
        for (var i = 0; i < 0x80; i++)
        {
            bytes[i] = 0x80 + i;
            expected.Append((char) (0xF780 + i));
        }

        Decode(engine, "x-user-defined", bytes).Should().Be(expected.ToString());
        Decode(engine, "x-user-defined", 0x80).Should().Be("\uF780");
        Decode(engine, "x-user-defined", 0xFF).Should().Be("\uF7FF");
        Decode(engine, "x-user-defined", 0x7F).Should().Be("\u007F");
    }

    [Test]
    public void NeverFailsForXUserDefined()
    {
        var engine = WebEngine();

        // The decoder has no error step at all, so fatal has nothing to fire on, and no BOM is stripped.
        engine.Evaluate("new TextDecoder('x-user-defined', { fatal: true }).decode(new Uint8Array([0xFF, 0x80]))")
            .AsString().Should().Be("\uF7FF\uF780");
        engine.Evaluate("new TextDecoder('x-user-defined').decode(new Uint8Array([0xEF, 0xBB, 0xBF])).length")
            .AsNumber().Should().Be(3);
    }

    #endregion

    #region replacement and the encodings Jint does not implement

    // https://encoding.spec.whatwg.org/#dom-textdecoder step 2: a label for the replacement encoding is a
    // RangeError, exactly like a label that names nothing. The encoding exists to stop an attacker smuggling
    // one of these charsets past a filter, so a decoder for it is never handed out.
    [TestCase("replacement")]
    [TestCase("csiso2022kr")]
    [TestCase("hz-gb-2312")]
    [TestCase("iso-2022-cn")]
    [TestCase("iso-2022-cn-ext")]
    [TestCase("iso-2022-kr")]
    [TestCase("ISO-2022-KR")]
    [TestCase("  replacement  ")]
    public void RefusesTheReplacementEncoding(string label)
    {
        var engine = WebEngine();

        engine.SetValue("label", label);
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TextDecoder(label)"))!
            .Error.Get("name").AsString().Should().Be("RangeError");
    }

    // The legacy multi-byte encodings each need an index of their own and are not implemented; Jint reports
    // the RangeError it reports for any label it will not decode. This is a documented deviation, since a
    // conforming implementation decodes these.
    [TestCase("gbk")]
    [TestCase("gb18030")]
    [TestCase("big5")]
    [TestCase("euc-jp")]
    [TestCase("iso-2022-jp")]
    [TestCase("shift_jis")]
    [TestCase("euc-kr")]
    [TestCase("chinese")]
    [TestCase("korean")]
    public void StillRefusesTheMultiByteEncodings(string label)
    {
        var engine = WebEngine();

        engine.SetValue("label", label);
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TextDecoder(label)"))!
            .Error.Get("name").AsString().Should().Be("RangeError");
    }

    #endregion

    #region Exhaustive checks against the specification's own data

    [Test]
    public void ResolvesEveryLabelTheSpecificationLists()
    {
        var engine = WebEngine();
        var failures = new List<string>();
        var labels = 0;

        // The multi-byte encodings Jint does not implement, and the replacement encoding the constructor is
        // required to refuse: for these the expectation is a RangeError rather than an encoding name.
        var refused = new HashSet<string>(StringComparer.Ordinal)
        {
            "replacement", "GBK", "gb18030", "Big5", "EUC-JP", "ISO-2022-JP", "Shift_JIS", "EUC-KR",
        };

        using var document = LoadSpecData("whatwg-encoding-encodings.json");
        foreach (var group in document.RootElement.EnumerateArray())
        {
            foreach (var encoding in group.GetProperty("encodings").EnumerateArray())
            {
                var name = encoding.GetProperty("name").GetString()!;
                var expected = name.ToLowerInvariant();

                foreach (var labelElement in encoding.GetProperty("labels").EnumerateArray())
                {
                    var label = labelElement.GetString()!;
                    labels++;
                    engine.SetValue("label", label);

                    if (refused.Contains(name))
                    {
                        try
                        {
                            engine.Evaluate("new TextDecoder(label)");
                            failures.Add($"{label}: expected a RangeError for {name}, got a decoder");
                        }
                        catch (JavaScriptException e) when (string.Equals(e.Error.Get("name").AsString(), "RangeError", StringComparison.Ordinal))
                        {
                        }

                        continue;
                    }

                    var actual = engine.Evaluate("new TextDecoder(label).encoding").AsString();
                    if (!string.Equals(actual, expected, StringComparison.Ordinal))
                    {
                        failures.Add($"{label}: expected {expected}, got {actual}");
                    }
                }
            }
        }

        failures.Should().BeEmpty();

        // Every label of every encoding in the table, so a label the generator dropped fails here rather
        // than becoming a silent RangeError in someone's script.
        labels.Should().Be(228);
    }

    [Test]
    public void DecodesEveryByteOfEverySingleByteIndexTheWayTheIndexSays()
    {
        var engine = WebEngine();
        var failures = new List<string>();
        var checkedEncodings = 0;

        using var indexes = LoadSpecData("whatwg-encoding-indexes.json");
        using var encodings = LoadSpecData("whatwg-encoding-encodings.json");

        var allBytes = new int[256];
        for (var i = 0; i < allBytes.Length; i++)
        {
            allBytes[i] = i;
        }

        foreach (var group in encodings.RootElement.EnumerateArray())
        {
            if (!string.Equals(group.GetProperty("heading").GetString(), "Legacy single-byte encodings", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var encoding in group.GetProperty("encodings").EnumerateArray())
            {
                var name = encoding.GetProperty("name").GetString()!.ToLowerInvariant();
                checkedEncodings++;

                // ISO-8859-8-I is a distinct encoding with no index of its own: it shares ISO-8859-8's.
                var indexName = string.Equals(name, "iso-8859-8-i", StringComparison.Ordinal) ? "iso-8859-8" : name;
                var index = indexes.RootElement.GetProperty(indexName);

                var expected = new StringBuilder(256);
                for (var b = 0; b < 0x80; b++)
                {
                    expected.Append((char) b);
                }

                for (var b = 0x80; b <= 0xFF; b++)
                {
                    var codePoint = index[b - 0x80];
                    expected.Append(codePoint.ValueKind == JsonValueKind.Null ? '\uFFFD' : (char) codePoint.GetUInt16());
                }

                var actual = Decode(engine, name, allBytes);
                if (string.Equals(actual, expected.ToString(), StringComparison.Ordinal))
                {
                    continue;
                }

                for (var i = 0; i < Math.Min(actual.Length, expected.Length); i++)
                {
                    if (actual[i] != expected[i])
                    {
                        failures.Add($"{name}: byte 0x{i:X2} decoded to U+{(int) actual[i]:X4}, expected U+{(int) expected[i]:X4}");
                        break;
                    }
                }

                if (actual.Length != expected.Length)
                {
                    failures.Add($"{name}: decoded {actual.Length} characters from 256 bytes, expected {expected.Length}");
                }
            }
        }

        failures.Should().BeEmpty();
        checkedEncodings.Should().Be(28);
    }

    [Test]
    public void KeepsTheIndexTablesInTheAssemblysDataSection()
    {
        // The generated tables are ReadOnlySpan-valued properties, which the compiler lowers to a pointer
        // into the assembly's data section rather than to a fresh array per call: two reads see the very
        // same memory, and building a decoder allocates and initializes nothing. It is worth pinning
        // because the property would go on working while quietly allocating 256 bytes per decode if the
        // shape ever stopped qualifying for that lowering.
        ref var first = ref MemoryMarshal.GetReference(EncodingTables.IndexFor(SingleByteIndex.Windows1252));
        ref var second = ref MemoryMarshal.GetReference(EncodingTables.IndexFor(SingleByteIndex.Windows1252));

        Unsafe.AreSame(ref first, ref second).Should().BeTrue();
        EncodingTables.IndexFor(SingleByteIndex.Windows1252).Length.Should().Be(128);

        // ISO-8859-8-I decodes through ISO-8859-8's index, and it is literally the same table.
        EncodingLabels.TryLookup("iso-8859-8-i", out var withDirection).Should().BeTrue();
        EncodingLabels.TryLookup("iso-8859-8", out var visual).Should().BeTrue();
        withDirection.Name.Should().Be("iso-8859-8-i");
        visual.Name.Should().Be("iso-8859-8");
        withDirection.Index.Should().Be(visual.Index);
    }

    private static JsonDocument LoadSpecData(string resourceName)
    {
        var assembly = typeof(LegacyEncodingTests).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        stream.Should().NotBeNull($"{resourceName} must be embedded from tools/whatwg-encoding");
        return JsonDocument.Parse(stream!);
    }

    #endregion
}
#endif
