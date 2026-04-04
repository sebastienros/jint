using System.Threading;

namespace Jint.Runtime.RegExp;

/// <summary>
/// Custom JavaScript regex engine based on QuickJS's libregexp.
/// Used when .NET Regex cannot handle the pattern (v-flag, unicode property escapes,
/// forward backreferences, semantic differences).
/// </summary>
internal sealed class JintRegExpEngine
{
    private readonly byte[] _bytecode;
    private readonly int _captureCount;
    private readonly string?[] _groupNames = [];
    private readonly RegExpFlags _flags;

    private JintRegExpEngine(byte[] bytecode)
    {
        _bytecode = bytecode;
        _captureCount = RegExpInterpreter.GetCaptureCount(_bytecode);
        _flags = RegExpInterpreter.GetFlags(_bytecode);
        _groupNames = RegExpInterpreter.GetGroupNames(_bytecode) ?? [];
    }

    /// <summary>Compile a JavaScript regex pattern into bytecode.</summary>
    public static JintRegExpEngine Compile(string pattern, RegExpFlags flags, CancellationToken cancellationToken = default)
    {
        var bytecode = RegExpCompiler.Compile(pattern, flags, cancellationToken);
        return new JintRegExpEngine(bytecode);
    }

    /// <summary>Capture group count (including group 0 for full match).</summary>
    public int CaptureCount => _captureCount;

    /// <summary>Compiled flags.</summary>
    public RegExpFlags Flags => _flags;

    /// <summary>Get the name of a capture group by its 1-based index.</summary>
    public string? GetGroupName(int index)
    {
        if (index <= 0 || index >= _groupNames.Length)
        {
            return null;
        }

        return _groupNames[index];
    }

    /// <summary>Execute the regex against the input string starting at the given index.</summary>
    public RegExpMatchResult Execute(string input, int startIndex, CancellationToken cancellationToken = default)
    {
        var captures = RegExpInterpreter.Execute(_bytecode, input, startIndex, cancellationToken);
        if (captures is null)
        {
            return RegExpMatchResult.NoMatch;
        }

        return BuildResult(input, captures);
    }

    /// <summary>Test if the regex matches the input string.</summary>
    public bool IsMatch(string input, int startIndex = 0, CancellationToken cancellationToken = default)
    {
        return RegExpInterpreter.Execute(_bytecode, input, startIndex, cancellationToken) is not null;
    }

    private RegExpMatchResult BuildResult(string input, int[] captures)
    {
        // captures[0] = start of full match, captures[1] = end of full match
        var matchStart = captures[0];
        var matchEnd = captures[1];
        var matchValue = input.Substring(matchStart, matchEnd - matchStart);

        RegExpGroupResult[]? groups = null;
        if (_captureCount > 0)
        {
            groups = new RegExpGroupResult[_captureCount];

            // Group 0 = full match
            groups[0] = new RegExpGroupResult(
                Success: true,
                Index: matchStart,
                Length: matchEnd - matchStart,
                Value: matchValue,
                Name: null);

            // Groups 1..N
            for (var i = 1; i < _captureCount; i++)
            {
                var startIdx = captures[i * 2];
                var endIdx = captures[i * 2 + 1];
                if (startIdx >= 0 && endIdx >= 0)
                {
                    var groupValue = input.Substring(startIdx, endIdx - startIdx);
                    groups[i] = new RegExpGroupResult(
                        Success: true,
                        Index: startIdx,
                        Length: endIdx - startIdx,
                        Value: groupValue,
                        Name: GetGroupName(i));
                }
                else
                {
                    groups[i] = new RegExpGroupResult(
                        Success: false,
                        Index: -1,
                        Length: 0,
                        Value: string.Empty,
                        Name: GetGroupName(i));
                }
            }
        }

        return new RegExpMatchResult(
            Success: true,
            Index: matchStart,
            Length: matchEnd - matchStart,
            Value: matchValue,
            Groups: groups);
    }
}
