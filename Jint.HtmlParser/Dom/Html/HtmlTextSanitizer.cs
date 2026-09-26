namespace Jint.HtmlParser;

// One invocation counter spans scans, copies and comparisons. Browser can supply
// its native constraint checkpoint; cancellation is checked every 256 work units.
internal struct HtmlTextWork(CancellationToken cancellationToken, Action<int>? checkpoint = null, int initialSteps = 0)
{
    private int _steps = initialSteps;
    internal int Steps => _steps;
    internal void ContinueFrom(int steps) => _steps = steps;

    internal void Finish()
    {
        if ((_steps & 255) != 0) checkpoint?.Invoke(_steps);
        Check();
    }

    internal bool StringEquals(string left, string right)
    {
        Step();
        if (ReferenceEquals(left, right)) return true;
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            Step();
            if (left[i] != right[i]) return false;
        }
        return true;
    }

    internal void Step()
    {
        if ((++_steps & 255) == 0)
        {
            checkpoint?.Invoke(_steps);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    internal void Check() => cancellationToken.ThrowIfCancellationRequested();
}

/// <summary>HTML Living Standard §4.10.5.1 text-type value sanitization and §4.10.11 textarea value/wrap algorithms.</summary>
internal static class HtmlTextSanitizer
{
    internal static string SanitizeInput(HtmlInputType type, string value, bool multiple,
        CancellationToken cancellationToken)
        => SanitizeInput(type, value, multiple, null, cancellationToken);

    internal static string SanitizeInput(HtmlInputType type, string value, bool multiple,
        Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        var work = new HtmlTextWork(cancellationToken, checkpoint);
        return SanitizeInput(type, value, multiple, ref work);
    }

    internal static string SanitizeInput(HtmlInputType type, string value, bool multiple, ref HtmlTextWork work)
    {
        ArgumentNullException.ThrowIfNull(value);
        work.Check();
        var result = type switch
        {
            HtmlInputType.Text or HtmlInputType.Search or HtmlInputType.Tel or HtmlInputType.Password
                => StripNewlines(value, trimEdges: false, ref work),
            HtmlInputType.Url => StripNewlines(value, trimEdges: true, ref work),
            HtmlInputType.Email when multiple => TrimEmailTokens(value, ref work),
            HtmlInputType.Email => StripNewlines(value, trimEdges: true, ref work),
            // b3 owns numeric/date/color/range and b4 owns file; they have no text fallback.
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        work.Check();
        return result;
    }

    internal static string NormalizeTextAreaValue(string rawValue, CancellationToken cancellationToken)
        => NormalizeTextAreaValue(rawValue, null, cancellationToken);

    internal static string NormalizeTextAreaValue(string rawValue, Action<int>? checkpoint,
        CancellationToken cancellationToken)
    {
        var work = new HtmlTextWork(cancellationToken, checkpoint);
        return NormalizeTextAreaValue(rawValue, ref work);
    }

    internal static string NormalizeTextAreaValue(string rawValue, ref HtmlTextWork work)
    {
        ArgumentNullException.ThrowIfNull(rawValue);
        work.Check();
        var firstCr = -1;
        var pairs = 0;
        for (var i = 0; i < rawValue.Length; i++)
        {
            work.Step();
            if (rawValue[i] != '\r')
            {
                continue;
            }

            if (firstCr < 0)
            {
                firstCr = i;
            }

            if (i + 1 < rawValue.Length && rawValue[i + 1] == '\n')
            {
                pairs++;
                i++;
                work.Step();
            }
        }

        if (firstCr < 0)
        {
            work.Check();
            return rawValue;
        }

        work.Check();
        var result = new char[rawValue.Length - pairs];
        var destination = 0;
        for (var i = 0; i < rawValue.Length; i++)
        {
            work.Step();
            var character = rawValue[i];
            result[destination++] = character == '\r' ? '\n' : character;
            if (character == '\r' && i + 1 < rawValue.Length && rawValue[i + 1] == '\n')
            {
                i++;
                work.Step();
            }
        }

        work.Check();
        var normalized = new string(result);
        work.Check();
        return normalized;
    }

    // Standalone, non-layout policy: insert LF before the next Unicode code point after
    // cols code points on a line. Existing LF and exact final boundaries add nothing.
    internal static string GetTextAreaSubmissionValue(string apiValue, HtmlTextAreaWrapMode wrapMode,
        long columns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apiValue);
        cancellationToken.ThrowIfCancellationRequested();
        return wrapMode switch
        {
            HtmlTextAreaWrapMode.Soft => apiValue,
            HtmlTextAreaWrapMode.Hard => WrapTextAreaForSubmission(apiValue, columns, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(wrapMode))
        };
    }

    internal static string WrapTextAreaForSubmission(string apiValue, long columns,
        CancellationToken cancellationToken)
        => WrapTextAreaForSubmission(apiValue, columns, null, cancellationToken);

    internal static string WrapTextAreaForSubmission(string apiValue, long columns,
        Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apiValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);

        var work = new HtmlTextWork(cancellationToken, checkpoint);
        work.Check();
        var breaks = CountWrapBreaks(apiValue, columns, ref work);
        if (breaks == 0)
        {
            work.Check();
            return apiValue;
        }

        work.Check();
        var result = new char[checked(apiValue.Length + breaks)];
        var destination = 0;
        long linePoints = 0;
        for (var i = 0; i < apiValue.Length; i++)
        {
            work.Step();
            var character = apiValue[i];
            if (character == '\n')
            {
                result[destination++] = character;
                linePoints = 0;
                continue;
            }

            if (linePoints == columns)
            {
                result[destination++] = '\n';
                linePoints = 0;
                work.Step();
            }

            result[destination++] = character;
            if (char.IsHighSurrogate(character) && i + 1 < apiValue.Length && char.IsLowSurrogate(apiValue[i + 1]))
            {
                result[destination++] = apiValue[++i];
                work.Step();
            }

            linePoints++;
        }

        work.Check();
        var wrapped = new string(result);
        work.Check();
        return wrapped;
    }

    private static int CountWrapBreaks(string value, long columns, ref HtmlTextWork work)
    {
        var breaks = 0;
        long linePoints = 0;
        for (var i = 0; i < value.Length; i++)
        {
            work.Step();
            var character = value[i];
            if (character == '\n')
            {
                linePoints = 0;
                continue;
            }

            if (linePoints == columns)
            {
                breaks++;
                linePoints = 0;
                work.Step();
            }

            if (char.IsHighSurrogate(character) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
                work.Step();
            }

            linePoints++;
        }

        return breaks;
    }

    private static string StripNewlines(string value, bool trimEdges, ref HtmlTextWork work)
    {
        var first = -1;
        var last = -1;
        var removed = 0;
        for (var i = 0; i < value.Length; i++)
        {
            work.Step();
            var character = value[i];
            if (character is '\r' or '\n')
            {
                removed++;
            }
            else if (!trimEdges || !IsAsciiWhitespace(character))
            {
                if (first < 0)
                {
                    first = i;
                }

                last = i;
            }
        }

        if (first < 0)
        {
            work.Check();
            return string.Empty;
        }

        if (removed == 0)
        {
            work.Check();
            if (first == 0 && last == value.Length - 1)
            {
                return value;
            }

            var trimmed = value.Substring(first, last - first + 1);
            work.Check();
            return trimmed;
        }

        var length = 0;
        for (var i = first; i <= last; i++)
        {
            work.Step();
            if (value[i] is not ('\r' or '\n'))
            {
                length++;
            }
        }

        work.Check();
        var result = new char[length];
        var destination = 0;
        for (var i = first; i <= last; i++)
        {
            work.Step();
            if (value[i] is not ('\r' or '\n'))
            {
                result[destination++] = value[i];
            }
        }

        work.Check();
        var sanitized = new string(result);
        work.Check();
        return sanitized;
    }

    private static string TrimEmailTokens(string value, ref HtmlTextWork work)
    {
        var outputLength = 0;
        var changed = false;
        for (var start = 0; start <= value.Length;)
        {
            TokenBounds(value, start, ref work, out var comma, out var first, out var end);
            outputLength += end - first;
            changed |= first != start || end != comma;
            if (comma == value.Length)
            {
                break;
            }

            outputLength++;
            work.Step();
            start = comma + 1;
        }

        if (!changed)
        {
            work.Check();
            return value;
        }

        work.Check();
        var result = new char[outputLength];
        var destination = 0;
        for (var start = 0; start <= value.Length;)
        {
            TokenBounds(value, start, ref work, out var comma, out var first, out var end);
            for (var i = first; i < end; i++)
            {
                work.Step();
                result[destination++] = value[i];
            }

            if (comma == value.Length)
            {
                break;
            }

            result[destination++] = ',';
            work.Step();
            start = comma + 1;
        }

        work.Check();
        var sanitized = new string(result);
        work.Check();
        return sanitized;
    }

    private static void TokenBounds(string value, int start, ref HtmlTextWork work,
        out int comma, out int first, out int end)
    {
        first = -1;
        end = start;
        comma = start;
        while (comma < value.Length && value[comma] != ',')
        {
            work.Step();
            if (!IsAsciiWhitespace(value[comma]))
            {
                if (first < 0)
                {
                    first = comma;
                }

                end = comma + 1;
            }

            comma++;
        }

        if (first < 0)
        {
            first = end = comma;
        }
    }

    internal static bool IsAsciiWhitespace(char character)
        => character is '\t' or '\n' or '\f' or '\r' or ' ';
}
