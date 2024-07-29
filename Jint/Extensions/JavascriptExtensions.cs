namespace Jint.Extensions;

internal static class JavascriptExtensions
{
    internal static string UpperToLowerCamelCase(this string str)
    {
        if (char.IsLower(str[0]))
        {
            return str;
        }

        var arr = str.ToCharArray();
        arr[0] = char.ToLowerInvariant(arr[0]);
        return new string(arr);
    }
}
