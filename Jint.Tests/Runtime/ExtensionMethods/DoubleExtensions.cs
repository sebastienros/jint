using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jint.Tests.Runtime.ExtensionMethods
{
    public static class DoubleExtensions
    {
        public static double Add(this double integer, int add)
        {
            return integer + add;
        }
    }
}
