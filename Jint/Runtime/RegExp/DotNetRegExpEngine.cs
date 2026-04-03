using System.Text.RegularExpressions;
using Acornima;

namespace Jint.Runtime.RegExp;

/// <summary>
/// Wraps .NET System.Text.RegularExpressions.Regex to provide regex matching
/// using Acornima-adapted patterns.
/// </summary>
internal sealed class DotNetRegExpEngine
{
    private readonly Regex _regex;
    private readonly RegExpParseResult _parseResult;

    public DotNetRegExpEngine(Regex regex, RegExpParseResult parseResult)
    {
        _regex = regex;
        _parseResult = parseResult;
    }

    /// <summary>The underlying .NET Regex instance.</summary>
    public Regex Regex => _regex;

    /// <summary>The Acornima parse result containing group metadata.</summary>
    public RegExpParseResult ParseResult => _parseResult;

    /// <summary>Actual capture group count (including group 0 for full match).</summary>
    public int ActualGroupCount => _parseResult.Success
        ? _parseResult.ActualRegexGroupCount
        : _regex.GetGroupNumbers().Length;

    /// <summary>Get the name of a capture group by its index.</summary>
    public string? GetGroupName(int index)
    {
        if (index == 0)
        {
            return null;
        }

        if (_parseResult.Success)
        {
            return _parseResult.GetRegexGroupName(index);
        }

        var groupNameFromNumber = _regex.GroupNameFromNumber(index);
        if (groupNameFromNumber.Length == 1 && groupNameFromNumber[0] == (char) (48 + index))
        {
            return null;
        }

        return groupNameFromNumber;
    }
}
