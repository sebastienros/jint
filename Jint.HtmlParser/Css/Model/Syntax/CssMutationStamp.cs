namespace Jint.HtmlParser.Css.Model.Syntax;

internal readonly record struct CssMutationStamp(ulong Value)
{
    internal bool CanReuse => Value != ulong.MaxValue;

    internal static void Advance(ref ulong value)
    {
        if (value != ulong.MaxValue) value++;
    }
}
