using System.Linq;

namespace Jint.Tests.Runtime.ExtensionMethods
{
    public static class CustomStringExtensions
    {
        public static string Backwards(this string value)
        {
            return new string(value.Reverse().ToArray());
        }
    }
}
