using System.Dynamic;
using System.Linq;
using Newtonsoft.Json;

namespace Jint.Tests.Runtime.ExtensionMethods
{
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
    }
}
