using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// <c>Intl.Segmenter.prototype.segment</c> over about 16K characters of text. <c>segment()</c> computes every segment
/// eagerly, so the <c>Segment*</c> rows measure the boundary rules and the segment strings, and the <c>Iterate*</c> row
/// adds what script pays to walk them. <c>*Ascii</c> is plain English prose with the punctuation the word rules look at
/// (apostrophes, full stops between letters and digits, hyphens, an e-mail address); <c>*NonAscii</c> mixes accented
/// Latin, Cyrillic, Han, Katakana and emoji sequences, so most characters take the table search instead of the ASCII
/// table. <c>SegmentSentencesAscii</c> and <c>SegmentGraphemesAscii</c> are the control rows of a change to the word
/// rules, which cannot reach them.
/// <para><b>Engine isolation.</b> Every row gets its own engine built by <c>CreateEngine</c>, which owns its own copy
/// of the texts, and is warmed with its own script and nothing else (see <see cref="IsolatedScript"/>).</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class IntlSegmenterBenchmark
{
    private IsolatedScript _segmentWordsAscii;
    private IsolatedScript _segmentWordsNonAscii;
    private IsolatedScript _iterateWordsAscii;
    private IsolatedScript _segmentSentencesAscii;
    private IsolatedScript _segmentGraphemesAscii;

    /// <summary>Builds a fresh engine carrying the texts and segmenters every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine(static options => options.Strict = true);
        engine.Execute("""
            var cp = String.fromCodePoint;
            var asciiUnit = "The quick brown fox jumps over the lazy dog. It's 3.14 o'clock, isn't it? "
                + "E-mail me at x@y.com, e.g. on Mon. 12,000 a_b U.S.A. now! ";
            var nonAsciiUnit = "Caf" + cp(0xE9) + " na" + cp(0xEF) + "ve r" + cp(0xE9) + "sum" + cp(0xE9) + ", "
                + cp(0x6771, 0x4EAC) + " " + cp(0x30C6, 0x30B9, 0x30C8) + ", " + cp(0x41F, 0x440, 0x438, 0x432, 0x435, 0x442) + "! "
                + cp(0x1F44D, 0x1F3FD) + " " + cp(0x1F469, 0x200D, 0x1F4BB) + " don" + cp(0x2019) + "t. ";
            var ascii = "";
            while (ascii.length < 16384) {
                ascii += asciiUnit;
            }
            var nonAscii = "";
            while (nonAscii.length < 16384) {
                nonAscii += nonAsciiUnit;
            }
            var words = new Intl.Segmenter("en", { granularity: "word" });
            var sentences = new Intl.Segmenter("en", { granularity: "sentence" });
            var graphemes = new Intl.Segmenter("en", { granularity: "grapheme" });
            """);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _segmentWordsAscii = IsolatedScript.Warm(Engine.PrepareScript("words.segment(ascii).containing(0).index;", strict: true), CreateEngine);
        _segmentWordsNonAscii = IsolatedScript.Warm(Engine.PrepareScript("words.segment(nonAscii).containing(0).index;", strict: true), CreateEngine);
        _iterateWordsAscii = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var count = 0;
                for (var segment of words.segment(ascii)) {
                    if (segment.isWordLike) {
                        count++;
                    }
                }
                return count;
            })();
            """, strict: true), CreateEngine);
        _segmentSentencesAscii = IsolatedScript.Warm(Engine.PrepareScript("sentences.segment(ascii).containing(0).index;", strict: true), CreateEngine);
        _segmentGraphemesAscii = IsolatedScript.Warm(Engine.PrepareScript("graphemes.segment(ascii).containing(0).index;", strict: true), CreateEngine);
    }

    [Benchmark]
    public JsValue SegmentWordsAscii() => _segmentWordsAscii.Run();

    [Benchmark]
    public JsValue SegmentWordsNonAscii() => _segmentWordsNonAscii.Run();

    [Benchmark]
    public JsValue IterateWordsAscii() => _iterateWordsAscii.Run();

    [Benchmark]
    public JsValue SegmentSentencesAscii() => _segmentSentencesAscii.Run();

    [Benchmark]
    public JsValue SegmentGraphemesAscii() => _segmentGraphemesAscii.Run();
}
