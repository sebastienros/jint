using System.Threading;

namespace Jint.Native.String;

/// <summary>
/// Helper to cache common data structures when manipulating strings.
/// </summary>
internal sealed class StringExecutionContext
{
    private static readonly ThreadLocal<StringExecutionContext> _executionContext = new ThreadLocal<StringExecutionContext>(() => new StringExecutionContext());

    private List<JsString>? _splitSegmentList;
    private bool _splitSegmentListInUse;

    private StringExecutionContext()
    {
    }

    /// <summary>
    /// The scratch buffer <c>String.prototype.split</c> collects its segments in, reused across calls so the
    /// common case allocates nothing.
    /// </summary>
    /// <remarks>
    /// The buffer is thread-affine, not engine-affine, and the split loop is not a leaf: it calls
    /// <c>Engine.Constraints.Check()</c> every <see cref="Engine.ConstraintCheckInterval"/> segments, and a
    /// host-supplied <see cref="Constraint"/> may run script — on this engine or on another one sharing the
    /// thread. A second split reached that way used to <c>Clear()</c> the very list the outer one was still
    /// filling, so the outer split silently returned the wrong number of segments. Renting says who owns the
    /// buffer: the re-entrant caller gets one of its own, and only the owner returns the shared one.
    /// </remarks>
    public List<JsString> RentSplitSegmentList()
    {
        if (_splitSegmentListInUse)
        {
            // re-entered while the shared buffer is live; the nested call gets a private one
            return new List<JsString>();
        }

        _splitSegmentListInUse = true;
        var list = _splitSegmentList ??= new List<JsString>();
        list.Clear();
        return list;
    }

    /// <summary>
    /// Releases a buffer taken from <see cref="RentSplitSegmentList"/>. A private buffer handed to a
    /// re-entrant caller is not the shared one and is simply dropped. Clearing on the way out is what stops
    /// the shared buffer holding the last split's <see cref="JsString"/>s — and, through
    /// <see cref="JsString.CreateSliced(string, int, int)"/>, the script source they may be views over —
    /// until the next split on this thread.
    /// </summary>
    public void ReturnSplitSegmentList(List<JsString> list)
    {
        if (ReferenceEquals(list, _splitSegmentList))
        {
            list.Clear();
            _splitSegmentListInUse = false;
        }
    }

    public static StringExecutionContext Current => _executionContext.Value!;
}
