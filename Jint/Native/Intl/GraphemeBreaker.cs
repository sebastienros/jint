using System.Runtime.CompilerServices;

namespace Jint.Native.Intl;

/// <summary>
/// Extended grapheme cluster boundaries of Unicode UAX #29, rules GB1 to GB999
/// (https://www.unicode.org/reports/tr29/#Grapheme_Cluster_Boundary_Rules), which is what ECMA-402 leaves to the
/// implementation for <c>granularity: "grapheme"</c> (https://tc39.es/ecma402/#sec-intl.segmenter.prototype.segment). ICU's
/// root rules (icu4c/source/data/brkitr/rules/char.txt) are these rules without a tailoring, so a cluster is the one Node
/// finds.
/// </summary>
/// <remarks>
/// <para>
/// The properties come from a table generated from the Unicode 17.0.0 UCD (tools/text-segmentation/README.md) rather
/// than from <see cref="System.Globalization.StringInfo"/>, which follows the Unicode data of the runtime: that of .NET
/// Framework predates emoji sequences, CR LF and Hangul jamo clusters, and neither it nor .NET 10 or 11 applies GB9c.
/// </para>
/// <para>
/// One call finds the end of one cluster and reads every code point of it once. The left context GB9c, GB11, GB12 and
/// GB13 need is always inside the cluster, because every character it spans is one the rules join, so it is kept in
/// three small counters rather than read again.
/// </para>
/// </remarks>
internal static partial class GraphemeBreaker
{
    /// <summary>
    /// The Grapheme_Cluster_Break property (https://www.unicode.org/reports/tr29/#Grapheme_Cluster_Break_Property_Values),
    /// in the order of the generator.
    /// </summary>
    internal enum GraphemeClass : byte
    {
        Other,
        CR,
        LF,
        Control,
        Extend,
        ZWJ,
        RegionalIndicator,
        Prepend,
        SpacingMark,
        L,
        V,
        T,
        LV,
        LVT,
    }

    private const int ClassMask = 0x0F;

    /// <summary>Extended_Pictographic, which GB11 joins to a ZWJ that follows one.</summary>
    internal const int ExtendedPictographic = 0x10;

    /// <summary>The two bits of the Indic_Conjunct_Break property, which GB9c reads; zero is None.</summary>
    internal const int IndicConjunctBreakMask = 0x60;

    /// <summary>Indic_Conjunct_Break=Consonant.</summary>
    internal const int InCBConsonant = 0x20;

    /// <summary>Indic_Conjunct_Break=Extend.</summary>
    internal const int InCBExtend = 0x40;

    /// <summary>Indic_Conjunct_Break=Linker.</summary>
    internal const int InCBLinker = 0x60;

    // GB9c: what follows the last InCB consonant of the cluster.
    private const int NoConsonant = 0;
    private const int AfterConsonant = 1;
    private const int AfterLinker = 2;

    // GB11: what follows the last Extended_Pictographic character of the cluster.
    private const int NoPictographic = 0;
    private const int AfterPictographic = 1;
    private const int AfterPictographicZwj = 2;

    /// <summary>
    /// The position after the grapheme cluster that starts at <paramref name="start"/>, which is less than the length of
    /// <paramref name="input"/>.
    /// </summary>
    internal static int NextBreak(string input, int start)
    {
        var position = start + 1;

        // Two ASCII characters are two clusters, but CR LF (GB3): no ASCII character extends, prepends, joins or pairs.
        if (input[start] < 0x80 && (position == input.Length || input[position] < 0x80))
        {
            return input[start] == '\r' && position < input.Length && input[position] == '\n' ? position + 1 : position;
        }

        position = start;
        var previous = PropertiesAt(input, ref position);
        var previousClass = ClassOf(previous);

        // GB3 and GB4: nothing but the LF of a CR LF joins a line break or a control.
        if (previousClass is GraphemeClass.CR or GraphemeClass.LF or GraphemeClass.Control)
        {
            return previousClass == GraphemeClass.CR && position < input.Length && input[position] == '\n' ? position + 1 : position;
        }

        var conjunct = NoConsonant;
        var pictographic = NoPictographic;
        var regionalIndicators = 0;
        Track(previous, ref conjunct, ref pictographic, ref regionalIndicators);

        while (position < input.Length)
        {
            var next = position;
            var following = PropertiesAt(input, ref next);
            if (!Joins(previousClass, following, conjunct, pictographic, regionalIndicators))
            {
                break;
            }

            Track(following, ref conjunct, ref pictographic, ref regionalIndicators);
            previousClass = ClassOf(following);
            position = next;
        }

        return position;
    }

    /// <summary>
    /// GB5 to GB13 for the position between a character of class <paramref name="previous"/>, which is not a line break
    /// or a control, and the character <paramref name="following"/>, of the same cluster so far. False is GB999.
    /// </summary>
    private static bool Joins(GraphemeClass previous, int following, int conjunct, int pictographic, int regionalIndicators)
    {
        switch (ClassOf(following))
        {
            case GraphemeClass.CR:
            case GraphemeClass.LF:
            case GraphemeClass.Control:
                // GB5: ÷ (Control | CR | LF)
                return false;
            case GraphemeClass.Extend:
            case GraphemeClass.ZWJ:
            case GraphemeClass.SpacingMark:
                // GB9: × (Extend | ZWJ), GB9a: × SpacingMark
                return true;
        }

        switch (previous)
        {
            case GraphemeClass.Prepend:
                // GB9b: Prepend ×
                return true;
            case GraphemeClass.L:
                // GB6: L × (L | V | LV | LVT)
                if (ClassOf(following) is GraphemeClass.L or GraphemeClass.V or GraphemeClass.LV or GraphemeClass.LVT)
                {
                    return true;
                }

                break;
            case GraphemeClass.LV:
            case GraphemeClass.V:
                // GB7: (LV | V) × (V | T)
                if (ClassOf(following) is GraphemeClass.V or GraphemeClass.T)
                {
                    return true;
                }

                break;
            case GraphemeClass.LVT:
            case GraphemeClass.T:
                // GB8: (LVT | T) × T
                if (ClassOf(following) == GraphemeClass.T)
                {
                    return true;
                }

                break;
            case GraphemeClass.ZWJ:
                // GB11: \p{Extended_Pictographic} Extend* ZWJ × \p{Extended_Pictographic}
                if (pictographic == AfterPictographicZwj && (following & ExtendedPictographic) != 0)
                {
                    return true;
                }

                break;
            case GraphemeClass.RegionalIndicator:
                // GB12 and GB13: an odd number of regional indicators before this one
                if (ClassOf(following) == GraphemeClass.RegionalIndicator && (regionalIndicators & 1) == 1)
                {
                    return true;
                }

                break;
        }

        // GB9c: \p{InCB=Consonant} [\p{InCB=Extend}\p{InCB=Linker}]* \p{InCB=Linker} [\p{InCB=Extend}\p{InCB=Linker}]* × \p{InCB=Consonant}
        return conjunct == AfterLinker && (following & IndicConjunctBreakMask) == InCBConsonant;
    }

    /// <summary>
    /// Moves the left context of GB9c, GB11, GB12 and GB13 past <paramref name="properties"/>, a character the cluster
    /// now ends with.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Track(int properties, ref int conjunct, ref int pictographic, ref int regionalIndicators)
    {
        conjunct = (properties & IndicConjunctBreakMask) switch
        {
            InCBConsonant => AfterConsonant,
            InCBLinker => conjunct == NoConsonant ? NoConsonant : AfterLinker,
            InCBExtend => conjunct,
            _ => NoConsonant,
        };

        var graphemeClass = ClassOf(properties);
        if ((properties & ExtendedPictographic) != 0)
        {
            pictographic = AfterPictographic;
        }
        else if (pictographic != NoPictographic && graphemeClass == GraphemeClass.ZWJ)
        {
            pictographic = pictographic == AfterPictographic ? AfterPictographicZwj : NoPictographic;
        }
        else if (graphemeClass != GraphemeClass.Extend)
        {
            pictographic = NoPictographic;
        }
        else if (pictographic == AfterPictographicZwj)
        {
            pictographic = NoPictographic;
        }

        regionalIndicators = graphemeClass == GraphemeClass.RegionalIndicator ? regionalIndicators + 1 : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GraphemeClass ClassOf(int properties) => (GraphemeClass) (properties & ClassMask);

    /// <summary>
    /// The properties of the code point at <paramref name="position"/>, which moves past it: a lone surrogate is a code
    /// point of its own, of class Other.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PropertiesAt(string input, ref int position)
    {
        var c = input[position];
        if (c < 0x80)
        {
            position++;
            return AsciiProperties[c];
        }

        return NonAsciiPropertiesAt(input, ref position);
    }

    private static int NonAsciiPropertiesAt(string input, ref int position)
    {
        var c = input[position];
        if (char.IsHighSurrogate(c) && position + 1 < input.Length && char.IsLowSurrogate(input[position + 1]))
        {
            var codePoint = char.ConvertToUtf32(c, input[position + 1]);
            position += 2;
            return PropertiesOfNonAscii(codePoint);
        }

        position++;
        return PropertiesOfNonAscii(c);
    }

    /// <summary>
    /// The properties of a code point: its <see cref="GraphemeClass"/> in the low four bits and the flags above them.
    /// </summary>
    internal static int PropertiesOf(int codePoint) => codePoint < 0x80 ? AsciiProperties[codePoint] : PropertiesOfNonAscii(codePoint);

    /// <summary>
    /// A binary search of <see cref="_runs"/> for the last run that starts at or before the code point. The table is the
    /// Unicode Character Database's, not .NET's: the Unicode data of the runtime differs between the target frameworks
    /// Jint runs on, and a cluster must not.
    /// </summary>
    internal static int PropertiesOfNonAscii(int codePoint)
    {
        var runs = _runs;
        var low = 0;
        var high = runs.Length - 1;
        while (low < high)
        {
            var middle = (int) ((uint) (low + high + 1) >> 1);
            if ((runs[middle] >> 8) <= codePoint)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return runs[low] & 0xFF;
    }
}
