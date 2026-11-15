namespace Jint.HtmlParser.Css.Model.Syntax;

internal readonly record struct CssMutationStamp(ulong Value)
{
    private static long _epoch;
    internal static long Epoch => Volatile.Read(ref _epoch);
    internal bool CanReuse => Value != ulong.MaxValue;

    internal static void Advance(ref ulong value)
    {
        if (value != ulong.MaxValue) value++;
        // A process-wide negative witness only: changes in another document cause extra checks,
        // never reuse. Saturation permanently disables the shortcut rather than wrapping.
        var epoch = Epoch;
        while (epoch != long.MaxValue)
        {
            var observed = Interlocked.CompareExchange(ref _epoch, epoch + 1, epoch);
            if (observed == epoch) break;
            epoch = observed;
        }
    }
}
