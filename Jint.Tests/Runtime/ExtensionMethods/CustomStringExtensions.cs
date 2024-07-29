using System.Dynamic;
using Newtonsoft.Json;

namespace Jint.Tests.Runtime.ExtensionMethods;

public static class CustomStringExtensions
{
    public static string Backwards(this string value)
    {
        return new string(value.Reverse().ToArray());
    }

    public static T DeserializeObject<T>(this string json)
    {
        return JsonConvert.DeserializeObject<T>(json);
    }

    public static ExpandoObject DeserializeObject(this string json)
    {
        return DeserializeObject<ExpandoObject>(json);
    }

    public static string[] Split(this string value, string split, StringSplitOptions options)
    {
        return Array.Empty<string>();
    }

    public static string[] Split(this string value, int position)
    {
        var first = value.Substring(0, position);
        var second = value.Substring(position);
        return new string[] { first, second };
    }
}