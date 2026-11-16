using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-segments-objects
/// Represents the Segments object returned by Intl.Segmenter.prototype.segment().
/// </summary>
[JsObject]
internal sealed partial class JsSegments : ObjectInstance
{
    private readonly Realm _realm;
    private readonly JsSegmenter _segmenter;
    private readonly string _input;
    private readonly List<SegmentData> _segments;

    internal JsSegments(Engine engine, JsSegmenter segmenter, string input) : base(engine)
    {
        _realm = engine.Realm;
        _segmenter = segmenter;
        _input = input;
        _segments = ComputeSegments(input, segmenter.Granularity);
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-%segmentsprototype%.containing
    /// </summary>
    [JsFunction]
    private static JsValue Containing(JsSegments thisObject, [ToInteger] double n)
    {
        if (n < 0 || n >= thisObject._input.Length)
        {
            return Undefined;
        }

        var intIndex = (int) n;
        foreach (var segment in thisObject._segments)
        {
            if (intIndex >= segment.Index && intIndex < segment.Index + segment.Segment.Length)
            {
                return thisObject.CreateSegmentDataObject(segment);
            }
        }

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-%segmentsprototype%-@@iterator
    /// </summary>
    [JsSymbolFunction("Iterator", Flags = PropertyFlag.Writable | PropertyFlag.Configurable)]
    private static SegmentIterator GetIterator(JsSegments thisObject)
    {
        return new SegmentIterator(thisObject._engine, thisObject);
    }

    internal IEnumerable<SegmentData> GetSegments() => _segments;

    internal JsObject CreateSegmentDataObject(SegmentData data)
    {
        var obj = OrdinaryObjectCreate(_engine, _engine.Realm.Intrinsics.Object.PrototypeObject);
        obj.Set("segment", data.Segment);
        obj.Set("index", data.Index);
        obj.Set("input", _input);

        if (string.Equals(_segmenter.Granularity, "word", StringComparison.Ordinal))
        {
            obj.Set("isWordLike", data.IsWordLike);
        }

        return obj;
    }

    private static List<SegmentData> ComputeSegments(string input, string granularity)
    {
        var segments = new List<SegmentData>();

        if (string.IsNullOrEmpty(input))
        {
            return segments;
        }

        return granularity switch
        {
            "grapheme" => ComputeGraphemeSegments(input),
            "word" => ComputeWordSegments(input),
            "sentence" => ComputeSentenceSegments(input),
            _ => ComputeGraphemeSegments(input)
        };
    }

    /// <summary>
    /// The extended grapheme clusters of <paramref name="input"/> by the grapheme cluster boundary rules of UAX #29, see
    /// <see cref="GraphemeBreaker"/>.
    /// </summary>
    private static List<SegmentData> ComputeGraphemeSegments(string input)
    {
        var segments = new List<SegmentData>();
        var index = 0;

        while (index < input.Length)
        {
            var end = GraphemeBreaker.NextBreak(input, index);
            segments.Add(new SegmentData
            {
                Segment = input.Substring(index, end - index),
                Index = index,
                IsWordLike = false
            });
            index = end;
        }

        return segments;
    }

    /// <summary>
    /// The words of <paramref name="input"/> by the word boundary rules of UAX #29, see <see cref="WordBreaker"/>.
    /// </summary>
    private static List<SegmentData> ComputeWordSegments(string input)
    {
        var segments = new List<SegmentData>();
        var index = 0;

        while (index < input.Length)
        {
            var end = WordBreaker.NextBreak(input, index, out var isWordLike);
            segments.Add(new SegmentData
            {
                Segment = input.Substring(index, end - index),
                Index = index,
                IsWordLike = isWordLike
            });
            index = end;
        }

        return segments;
    }

    /// <summary>
    /// The sentences of <paramref name="input"/> by the sentence boundary rules of UAX #29, see <see cref="SentenceBreaker"/>.
    /// </summary>
    private static List<SegmentData> ComputeSentenceSegments(string input)
    {
        var segments = new List<SegmentData>();
        var index = 0;

        while (index < input.Length)
        {
            var end = SentenceBreaker.NextBreak(input, index);
            segments.Add(new SegmentData
            {
                Segment = input.Substring(index, end - index),
                Index = index,
                IsWordLike = false
            });
            index = end;
        }

        return segments;
    }

    internal struct SegmentData
    {
        public string Segment;
        public int Index;
        public bool IsWordLike;
    }

    /// <summary>
    /// Iterator for Segments object.
    /// </summary>
    private sealed class SegmentIterator : IteratorInstance
    {
        private readonly JsSegments _segments;
        private readonly IEnumerator<SegmentData> _enumerator;

        public SegmentIterator(Engine engine, JsSegments segments) : base(engine)
        {
            _segments = segments;
            _enumerator = segments.GetSegments().GetEnumerator();
        }

        public override bool TryIteratorStep(out ObjectInstance result)
        {
            if (_enumerator.MoveNext())
            {
                var segmentData = _segments.CreateSegmentDataObject(_enumerator.Current);
                result = new Iterator.IteratorResult(_engine, segmentData, JsBoolean.False);
                return true;
            }

            result = new Iterator.IteratorResult(_engine, JsValue.Undefined, JsBoolean.True);
            return false;
        }
    }
}
