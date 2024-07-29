namespace Jint.Tests.Runtime.ExtensionMethods;

public static class OverrideStringPrototypeExtensions
{
    public static string[] Split(this string value, string delimiter)
    {
        return value.Split(delimiter.ToCharArray()).Select(v => v.ToUpper()).ToArray();
    }
}