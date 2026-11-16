using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-segments-objects
/// Represents the Segments object returned by Intl.Segmenter.prototype.segment().
/// </summary>
internal sealed class JsSegments : ObjectInstance
{
    private readonly JsSegmenter _segmenter;
    private readonly string _input;
    private readonly List<SegmentData> _segments;

    /// <summary>
    /// https://tc39.es/ecma402/#sec-createsegmentsobject, inheriting <paramref name="prototype"/>: the
    /// %IntlSegmentsPrototype% of the realm of the <c>Intl.Segmenter.prototype.segment</c> that was called, whichever realm
    /// the caller or the segmenter belongs to. A Segments object has no own property: <c>containing</c> and <c>@@iterator</c>
    /// are the prototype's (sebastienros/jint#4452).
    /// </summary>
    internal JsSegments(Engine engine, SegmentsPrototype prototype, JsSegmenter segmenter, string input) : base(engine)
    {
        _prototype = prototype;
        _segmenter = segmenter;
        _input = input;
        _segments = ComputeSegments(input, segmenter.Granularity);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-%intlsegmentsprototype%.containing, from step 5 on: the segment data object, created in
    /// <paramref name="realm"/>, of the segment holding code unit <paramref name="n"/>, or undefined.
    /// </summary>
    internal JsValue Containing(Realm realm, double n)
    {
        if (n < 0 || n >= _input.Length)
        {
            return Undefined;
        }

        var intIndex = (int) n;
        foreach (var segment in _segments)
        {
            if (intIndex >= segment.Index && intIndex < segment.Index + segment.Segment.Length)
            {
                return CreateSegmentDataObject(realm, segment);
            }
        }

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-createsegmentiterator, inheriting <paramref name="prototype"/>, the
    /// %IntlSegmentIteratorPrototype% of the realm of the <c>@@iterator</c> that was called.
    /// </summary>
    internal JsValue CreateIterator(SegmentIteratorPrototype prototype) => new SegmentIterator(_engine, this, prototype);

    internal IEnumerable<SegmentData> GetSegments() => _segments;

    /// <summary>
    /// https://tc39.es/ecma402/#sec-createsegmentdataobject: an ordinary object of the current realm, the realm of the
    /// running <c>containing</c> or segment iterator <c>next</c>, whose properties are defined, not set.
    /// </summary>
    internal JsObject CreateSegmentDataObject(Realm realm, SegmentData data)
    {
        var obj = OrdinaryObjectCreate(_engine, realm.Intrinsics.Object.PrototypeObject);
        obj.CreateDataPropertyOrThrow("segment", data.Segment);
        obj.CreateDataPropertyOrThrow("index", JsNumber.Create(data.Index));
        obj.CreateDataPropertyOrThrow("input", _input);

        if (string.Equals(_segmenter.Granularity, "word", StringComparison.Ordinal))
        {
            obj.CreateDataPropertyOrThrow("isWordLike", data.IsWordLike ? JsBoolean.True : JsBoolean.False);
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
    /// A segment iterator (https://tc39.es/ecma402/#sec-segment-iterator-objects), which inherits the
    /// %IntlSegmentIteratorPrototype% of the realm that created it, and steps in that realm unless a <c>next</c> of another
    /// realm is called on it (<see cref="Step(Realm)"/>).
    /// </summary>
    internal sealed class SegmentIterator : IteratorInstance
    {
        private readonly JsSegments _segments;
        private readonly Realm _realm;
        private readonly IEnumerator<SegmentData> _enumerator;

        public SegmentIterator(Engine engine, JsSegments segments, SegmentIteratorPrototype prototype) : base(engine, prototype, IteratorBrand.Segment)
        {
            _segments = segments;
            _realm = prototype._realm;
            _enumerator = segments.GetSegments().GetEnumerator();
        }

        public override bool TryIteratorStep(out ObjectInstance result) => TryStep(_realm, out result);

        /// <summary>
        /// https://tc39.es/ecma402/#sec-%intlsegmentiteratorprototype%.next, run by the <c>next</c> of
        /// <paramref name="realm"/>, in whose realm the segment data object is created.
        /// </summary>
        internal JsValue Step(Realm realm)
        {
            TryStep(realm, out var result);
            return result;
        }

        private bool TryStep(Realm realm, out ObjectInstance result)
        {
            if (_enumerator.MoveNext())
            {
                var segmentData = _segments.CreateSegmentDataObject(realm, _enumerator.Current);
                result = new Iterator.IteratorResult(_engine, segmentData, JsBoolean.False);
                return true;
            }

            result = new Iterator.IteratorResult(_engine, JsValue.Undefined, JsBoolean.True);
            return false;
        }
    }
}
