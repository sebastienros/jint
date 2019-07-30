using System.Text;

namespace Jint.Extensions
{
    public static class JavascriptExtensions
    {
        public static string UpperToLowerCamelCase(this string str)
        {
            var sb = new StringBuilder();
            sb.Append(char.ToLowerInvariant(str[0]));
            sb.Append(str.Substring(1));
            return sb.ToString();
        }
    }
}
