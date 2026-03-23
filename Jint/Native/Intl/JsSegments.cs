using System.Globalization;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

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

    internal JsSegments(Engine engine, JsSegmenter segmenter, string input) : base(engine)
    {
        _segmenter = segmenter;
        _input = input;
        _segments = ComputeSegments(input, segmenter.Granularity);
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            ["containing"] = new PropertyDescriptor(new ClrFunction(_engine, "containing", Containing, 1, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.Iterator] = new PropertyDescriptor(new ClrFunction(_engine, "[Symbol.iterator]", GetIterator, 0, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-%segmentsprototype%.containing
    /// </summary>
    private JsValue Containing(JsValue thisObject, JsCallArguments arguments)
    {
        // 1. Let segments be the this value.
        // 2. Perform ? RequireInternalSlot(segments, [[SegmentsSegmenter]]).
        if (thisObject is not JsSegments segments)
        {
            Throw.TypeError(_engine.Realm, "containing requires a Segments object");
            return JsValue.Undefined;
        }

        var index = arguments.At(0);
        var n = TypeConverter.ToInteger(index);

        if (n < 0 || n >= segments._input.Length)
        {
            return JsValue.Undefined;
        }

        // Find the segment containing this index
        var intIndex = (int) n;
        foreach (var segment in segments._segments)
        {
            if (intIndex >= segment.Index && intIndex < segment.Index + segment.Segment.Length)
            {
                return segments.CreateSegmentDataObject(segment);
            }
        }

        return JsValue.Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-%segmentsprototype%-@@iterator
    /// </summary>
    private SegmentIterator GetIterator(JsValue thisObject, JsCallArguments arguments)
    {
        return new SegmentIterator(_engine, this);
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
