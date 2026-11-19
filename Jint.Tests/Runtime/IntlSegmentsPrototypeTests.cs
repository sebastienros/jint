#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// The <c>containing</c> method of a Segments object
/// (<a href="https://tc39.es/ecma402/#sec-%segmentsprototype%.containing">%SegmentsPrototype%.containing</a>), which
/// answers the segment holding a code unit.
/// </summary>
public class IntlSegmentsPrototypeTests
{
    private static string Run(string script) => new Engine().Evaluate(script).AsString();

    /// <summary>
    /// <c>containing(i)</c> answers, for every code unit of the input and in every granularity, the segment the iterator
    /// yields over that code unit, surrogate pairs, clusters and lone surrogates included; one past either end is undefined.
    /// The iterator is the oracle because it reads the segments in order, where <c>containing</c> searches them.
    /// </summary>
    [Fact]
    public void ContainingAnswersTheIteratedSegmentOfEveryCodeUnit()
    {
        Run("""
            const inputs = ['x', 'Ab cd. Ef', '\u{1F600} ok \u{1F600}', 'Mr. Smith went. Then \u{1D400}. Done.',
                'a\r\nb \u{1F469}‍\u{1F4BB} é \u{1F1EB}\u{1F1EE}!', '\uD800a\uDC00'];
            const out = [];
            let checked = 0;
            for (const granularity of ['grapheme', 'word', 'sentence']) {
                for (const input of inputs) {
                    const s = new Intl.Segmenter('en', { granularity }).segment(input);
                    const iterated = [...s];
                    for (let i = 0; i < input.length; i++) {
                        const owner = iterated.find(x => x.index <= i && i < x.index + x.segment.length);
                        checked++;
                        if (JSON.stringify(s.containing(i)) !== JSON.stringify(owner)) { out.push(granularity + ':' + JSON.stringify(input) + ':' + i); }
                    }
                    if (s.containing(input.length) !== undefined || s.containing(-1) !== undefined) { out.push(granularity + ':' + JSON.stringify(input) + ':outside'); }
                }
            }
            out.length === 0 ? 'ok ' + checked : out.join('|')
            """).Should().Be("ok 210");
    }

    /// <summary>
    /// The first and last code units of a string that starts and ends with a surrogate pair belong to the first and last
    /// segment, the low surrogate to its pair's segment; an empty string has no segment, so <c>containing(0)</c> is
    /// undefined. The expected values are Node 26's.
    /// </summary>
    [Fact]
    public void ContainingFindsTheFirstAndLastSegmentAndNoneInAnEmptyString()
    {
        Run("""
            const out = [];
            for (const granularity of ['grapheme', 'word', 'sentence']) {
                const s = new Intl.Segmenter('en', { granularity }).segment('\u{1F600} ok \u{1F600}');
                const empty = new Intl.Segmenter('en', { granularity }).segment('');
                out.push([0, 1, 7].map(i => s.containing(i).index + ':' + s.containing(i).segment.length).join(',')
                    + ',' + s.containing(8) + ',' + empty.containing(0) + ',' + [...empty].length);
            }
            out.join('|')
            """).Should().Be("0:2,0:2,6:2,undefined,undefined,0|0:2,0:2,6:2,undefined,undefined,0|0:8,0:8,0:8,undefined,undefined,0");
    }
}
