using System.Threading;

namespace Jint.Native.String;

/// <summary>
/// Helper to cache common data structures when manipulating strings.
/// </summary>
internal sealed class StringExecutionContext
{
    private static readonly ThreadLocal<StringExecutionContext> _executionContext = new ThreadLocal<StringExecutionContext>(() => new StringExecutionContext());

    private List<string>? _splitSegmentList;
    private string[]? _splitArray1;

    private StringExecutionContext()
    {
    }

    public List<string> SplitSegmentList => _splitSegmentList ??= new List<string>();

    public string[] SplitArray1 => _splitArray1 ??= new string[1];

    public static StringExecutionContext Current => _executionContext.Value!;
}
