using System.Threading;

namespace Jint.Native.String;

/// <summary>
/// Helper to cache common data structures when manipulating strings.
/// </summary>
internal sealed class StringExecutionContext
{
    private static readonly ThreadLocal<StringExecutionContext> _executionContext = new ThreadLocal<StringExecutionContext>(() => new StringExecutionContext());

    private List<JsString>? _splitSegmentList;

    private StringExecutionContext()
    {
    }

    public List<JsString> SplitSegmentList => _splitSegmentList ??= new List<JsString>();

    public static StringExecutionContext Current => _executionContext.Value!;
}
