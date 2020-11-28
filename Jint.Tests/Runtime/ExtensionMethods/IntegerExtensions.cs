using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jint.Tests.Runtime.ExtensionMethods
{
    public static class IntegerExtensions
    {
        public static int Add(this int integer, int add)
        {
            return integer + add;
        }
    }
}
