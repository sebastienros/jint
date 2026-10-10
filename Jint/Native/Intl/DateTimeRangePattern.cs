namespace Jint.Native.Intl;

/// <summary>
/// Which of a range's dates a part is written from (https://tc39.es/ecma402/#sec-datetimeformat-range-pattern-part-record).
/// </summary>
internal enum DateTimeRangeSource : byte
{
    Shared,
    StartRange,
    EndRange,
}

/// <summary>
/// A DateTime Range Pattern Format Record's <c>[[PatternParts]]</c> (https://tc39.es/ecma402/#sec-datetimeformat-range-pattern-format-record),
/// as the runs of the pattern and the source of each run: a maximal stretch of runs with one source is a part.
/// </summary>
internal sealed class DateTimeRangePattern
{
    internal DateTimeRangePattern(DateTimePatternRun[] runs, DateTimeRangeSource[] sources)
    {
        Runs = runs;
        Sources = sources;
    }

    internal DateTimePatternRun[] Runs { get; }

    internal DateTimeRangeSource[] Sources { get; }

    /// <summary>
    /// The sources of the runs ICU writes a range with, as V8 reports them wherever that is what
    /// https://tc39.es/ecma402/#sec-partitiondatetimerangepattern writes: null when no field occurs twice, in which case
    /// V8 writes the single start date.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ICU's <c>FormattedDateInterval</c> spans are the extent of the first and of the second occurrence of every field
    /// that occurs twice (<c>addOverlapSpans</c>); the first span belongs to whichever date is written first, and V8
    /// reports a run inside a span as that date's and every other run as shared. The specification writes a shared part
    /// from the start date, while ICU writes the pattern's second part from the end date, so the two disagree where CLDR
    /// writes a field of the second date that ICU does not pair: Jint pairs a stand-alone month or weekday with the
    /// format one (<c>fa</c>'s <c>d LLL – d MMM y</c>, two fields to ICU), and names the end date for a field ICU writes
    /// once from it and that can differ, being no larger than the field that does (<c>sw</c>'s month in
    /// <c>d – d MMM y</c>). V8 reports both as shared.
    /// </para>
    /// <para>
    /// CLDR writes U+2009 THIN SPACE around the dashes of its intervals and of its fallback, and a range keeps it, as V8
    /// does; U+202F is U+0020 here as in every lane.
    /// </para>
    /// </remarks>
    /// <param name="runs">The runs, each with the date ICU writes it from (0 the start, 1 the end, -1 for text of its own).</param>
    /// <param name="differingLevel">The level (<see cref="DateTimeIntervalFormat.LevelOf"/>) of the largest field that differs.</param>
    internal static DateTimeRangePattern? FromRuns(List<(DateTimePatternRun Run, int Date)> runs, int differingLevel)
    {
        int firstStart = -1, firstEnd = -1, secondStart = -1, secondEnd = -1;
        for (var i = 0; i < runs.Count; i++)
        {
            if (runs[i].Run.IsLiteral)
            {
                continue;
            }

            for (var j = i + 1; j < runs.Count; j++)
            {
                if (!runs[j].Run.IsLiteral && CalendarField(runs[j].Run.Field) == CalendarField(runs[i].Run.Field))
                {
                    firstStart = firstStart < 0 ? i : System.Math.Min(firstStart, i);
                    firstEnd = System.Math.Max(firstEnd, i);
                    secondStart = secondStart < 0 ? j : System.Math.Min(secondStart, j);
                    secondEnd = System.Math.Max(secondEnd, j);
                    break;
                }
            }
        }

        if (firstStart < 0)
        {
            return null;
        }

        var firstSource = runs[firstStart].Date == 0 ? DateTimeRangeSource.StartRange : DateTimeRangeSource.EndRange;
        var secondSource = firstSource == DateTimeRangeSource.StartRange ? DateTimeRangeSource.EndRange : DateTimeRangeSource.StartRange;
        var result = new DateTimePatternRun[runs.Count];
        var sources = new DateTimeRangeSource[runs.Count];
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i].Run;
            result[i] = run;
            if (i >= firstStart && i <= firstEnd)
            {
                sources[i] = firstSource;
            }
            else if (i >= secondStart && i <= secondEnd)
            {
                sources[i] = secondSource;
            }
            else if (!run.IsLiteral && runs[i].Date == 1 && DateTimeIntervalFormat.LevelOf(run.Field) >= differingLevel)
            {
                sources[i] = DateTimeRangeSource.EndRange;
            }
            else
            {
                sources[i] = DateTimeRangeSource.Shared;
            }
        }

        return new DateTimeRangePattern(result, sources);
    }

    /// <summary>The field a letter writes, with the stand-alone month and weekday read as the format ones.</summary>
    private static char CalendarField(char letter) => letter switch
    {
        'L' => 'M',
        'c' => 'E',
        _ => letter,
    };
}
