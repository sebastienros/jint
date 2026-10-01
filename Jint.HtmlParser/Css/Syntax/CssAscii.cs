namespace Jint.HtmlParser.Css.Syntax;

internal static class CssAscii
{
    internal static bool EqualsIgnoreCase(string left, string right)
    {
        if (left.Length != right.Length) return false;
        for (var index = 0; index < left.Length; index++)
        {
            var a = left[index];
            var b = right[index];
            if (a is >= 'A' and <= 'Z') a = (char) (a + ('a' - 'A'));
            if (b is >= 'A' and <= 'Z') b = (char) (b + ('a' - 'A'));
            if (a != b) return false;
        }
        return true;
    }
}
