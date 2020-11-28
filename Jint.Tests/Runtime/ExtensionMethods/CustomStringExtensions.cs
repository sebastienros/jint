using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
