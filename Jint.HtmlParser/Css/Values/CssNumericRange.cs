namespace Jint.HtmlParser.Css.Values;

internal readonly struct CssNumericRange
{
    internal CssNumericRange(CssNumber? lower, bool includeLower, CssNumber? upper, bool includeUpper)
    {
        Lower = lower;
        IncludeLower = includeLower;
        Upper = upper;
        IncludeUpper = includeUpper;
    }

    public CssNumber? Lower { get; }
    public bool IncludeLower { get; }
    public CssNumber? Upper { get; }
    public bool IncludeUpper { get; }

    internal bool Contains(CssNumber number, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        if (Lower is { } lower)
        {
            var comparison = number.CompareTo(lower, work);
            if (comparison < 0 || comparison == 0 && !IncludeLower)
            {
                work.CheckCancellation();
                return false;
            }
        }
        if (Upper is { } upper)
        {
            var comparison = number.CompareTo(upper, work);
            if (comparison > 0 || comparison == 0 && !IncludeUpper)
            {
                work.CheckCancellation();
                return false;
            }
        }
        work.CheckCancellation();
        return true;
    }
}
