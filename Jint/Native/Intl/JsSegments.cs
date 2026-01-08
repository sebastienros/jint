#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- iterator protocol requires JsValue

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
        var index = arguments.At(0);
        var n = TypeConverter.ToInteger(index);

        if (n < 0 || n >= _input.Length)
        {
            return JsValue.Undefined;
        }

        // Find the segment containing this index
        var intIndex = (int) n;
        foreach (var segment in _segments)
        {
            if (intIndex >= segment.Index && intIndex < segment.Index + segment.Segment.Length)
            {
                return CreateSegmentDataObject(segment);
            }
        }

        return JsValue.Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-%segmentsprototype%-@@iterator
    /// </summary>
    private JsValue GetIterator(JsValue thisObject, JsCallArguments arguments)
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
        var index = 0;
        var length = input.Length;

        while (index < length)
        {
            var startIndex = index;
            var c = input[index];

            if (char.IsWhiteSpace(c))
            {
                // Whitespace segment
                while (index < length && char.IsWhiteSpace(input[index]))
                {
                    index++;
                }
                segments.Add(new SegmentData
                {
                    Segment = input.Substring(startIndex, index - startIndex),
                    Index = startIndex,
                    IsWordLike = false
                });
            }
            else if (char.IsLetterOrDigit(c))
            {
                // Word segment
                while (index < length && (char.IsLetterOrDigit(input[index]) || input[index] == '\'' || input[index] == '-'))
                {
                    index++;
                }
                segments.Add(new SegmentData
                {
                    Segment = input.Substring(startIndex, index - startIndex),
                    Index = startIndex,
                    IsWordLike = true
                });
            }
            else
            {
                // Punctuation/other segment
                while (index < length && !char.IsWhiteSpace(input[index]) && !char.IsLetterOrDigit(input[index]))
                {
                    index++;
                }
                segments.Add(new SegmentData
                {
                    Segment = input.Substring(startIndex, index - startIndex),
                    Index = startIndex,
                    IsWordLike = false
                });
            }
        }

        return segments;
    }

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
