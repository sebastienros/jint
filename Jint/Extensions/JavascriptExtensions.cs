using System.Text;

namespace Jint.Extensions
{
    internal static class JavascriptExtensions
    {
        internal static string UpperToLowerCamelCase(this string str)
        {
            var sb = new StringBuilder();
            sb.Append(char.ToLowerInvariant(str[0]));
            sb.Append(str.Substring(1));
            return sb.ToString();
        }
    }
}
