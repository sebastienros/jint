using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    public interface IStringComparer
    {
        bool Equals(string clrname, string jsname);
    }
    public class IgnoreCasingStringComparer : IStringComparer
    {
        public static IStringComparer Current { get; } = new IgnoreCasingStringComparer();
        public bool Equals(string clrname, string jsname)
        {
            bool equals = false;
            if (clrname.Length == jsname.Length)
            {
                if (clrname.Length > 0 && jsname.Length > 0)
                {
                    equals = (clrname.ToLower()[0] == jsname.ToLower()[0]);
                }
                if (clrname.Length > 1 && jsname.Length > 1)
                {
                    equals = equals && (clrname.Substring(1) == jsname.Substring(1));
                }
            }
            return equals;
        }
    }
    public class DefaultStringComparer : IStringComparer
    {
        public static IStringComparer Current { get; } = new DefaultStringComparer();
        public bool Equals(string clrname, string jsname)
        {
            return clrname.Equals(jsname);
        }
    }
}
