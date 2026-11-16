using System.Globalization;
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

    private static List<SegmentData> ComputeGraphemeSegments(string input)
    {
        var segments = new List<SegmentData>();
        var enumerator = StringInfo.GetTextElementEnumerator(input);

        while (enumerator.MoveNext())
        {
            segments.Add(new SegmentData
            {
                Segment = enumerator.GetTextElement(),
                Index = enumerator.ElementIndex,
                IsWordLike = false
            });
        }

        return segments;
    }

    private static List<SegmentData> ComputeWordSegments(string input)
    {
        var segments = new List<SegmentData>();

        // First, get grapheme clusters
        var graphemes = new List<GraphemeInfo>();
        var enumerator = StringInfo.GetTextElementEnumerator(input);
        while (enumerator.MoveNext())
        {
            graphemes.Add(new GraphemeInfo(enumerator.GetTextElement(), enumerator.ElementIndex));
        }

        if (graphemes.Count == 0)
        {
            return segments;
        }

        var i = 0;
        while (i < graphemes.Count)
        {
            var grapheme = graphemes[i].Text;
            var startIndex = graphemes[i].Index;
            var firstChar = grapheme[0];

            if (char.IsWhiteSpace(firstChar))
            {
                // Whitespace segment - group consecutive whitespace graphemes
                var endIndex = startIndex + grapheme.Length;
                i++;
                while (i < graphemes.Count && graphemes[i].Text.Length > 0 && char.IsWhiteSpace(graphemes[i].Text[0]))
                {
                    endIndex = graphemes[i].Index + graphemes[i].Text.Length;
                    i++;
                }
                segments.Add(new SegmentData
                {
                    Segment = input.Substring(startIndex, endIndex - startIndex),
                    Index = startIndex,
                    IsWordLike = false
                });
            }
            else if (char.IsLetterOrDigit(firstChar) || char.GetUnicodeCategory(firstChar) == UnicodeCategory.ModifierLetter)
            {
                // Word segment - group consecutive word-like graphemes
                var endIndex = startIndex + grapheme.Length;
                i++;
                while (i < graphemes.Count)
                {
                    var nextGrapheme = graphemes[i].Text;
                    if (nextGrapheme.Length == 0) break;
                    var nextChar = nextGrapheme[0];
                    // Include letters, digits, apostrophe, hyphen, and modifier marks
                    if (char.IsLetterOrDigit(nextChar) ||
                        nextChar == '\'' || nextChar == '-' ||
                        char.GetUnicodeCategory(nextChar) == UnicodeCategory.ModifierLetter ||
                        char.GetUnicodeCategory(nextChar) == UnicodeCategory.NonSpacingMark)
                    {
                        endIndex = graphemes[i].Index + nextGrapheme.Length;
                        i++;
                    }
                    // Include decimal point or comma when between digits (for numbers like 1.23 or 1,000)
                    else if ((nextChar == '.' || nextChar == ',') && i + 1 < graphemes.Count)
                    {
                        var afterNext = graphemes[i + 1].Text;
                        if (afterNext.Length > 0 && char.IsDigit(afterNext[0]))
                        {
                            // Include the decimal/comma and the next digit
                            endIndex = graphemes[i + 1].Index + afterNext.Length;
                            i += 2;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                segments.Add(new SegmentData
                {
                    Segment = input.Substring(startIndex, endIndex - startIndex),
                    Index = startIndex,
                    IsWordLike = true
                });
            }
            else
            {
                // Punctuation/other - each grapheme is its own segment
                segments.Add(new SegmentData
                {
                    Segment = grapheme,
                    Index = startIndex,
                    IsWordLike = false
                });
                i++;
            }
        }

        return segments;
    }

    private readonly record struct GraphemeInfo(string Text, int Index);

    private static List<SegmentData> ComputeSentenceSegments(string input)
    {
        var segments = new List<SegmentData>();
        var index = 0;
        var length = input.Length;

        while (index < length)
        {
            var startIndex = index;

            // Find end of sentence (., !, ?)
            while (index < length)
            {
                var c = input[index];
                index++;

                if (c == '.' || c == '!' || c == '?')
                {
                    // Include trailing whitespace in the sentence
                    while (index < length && char.IsWhiteSpace(input[index]))
                    {
                        index++;
                    }
                    break;
                }
            }

            segments.Add(new SegmentData
            {
                Segment = input.Substring(startIndex, index - startIndex),
                Index = startIndex,
                IsWordLike = false
            });
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
