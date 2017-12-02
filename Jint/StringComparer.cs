using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    public class IgnoreCasingStringComparer : IEqualityComparer<string>
    {
        public static IEqualityComparer<string> Current { get; } = new IgnoreCasingStringComparer();
        public bool Equals(string clrname, string jsname)
        {
            if (clrname == null || jsname == null)
            {
                return clrname == null && jsname == null;
            }
            bool equals = false;
            if (clrname.Length == jsname.Length)
            {
                if (clrname.Length > 0 && jsname.Length > 0)
                {
                    equals = (clrname.ToLowerInvariant()[0] == jsname.ToLowerInvariant()[0]);
                }
                if (clrname.Length > 1 && jsname.Length > 1)
                {
                    equals = equals && (clrname.Substring(1) == jsname.Substring(1));
                }
            }
            return equals;
        }

        public int GetHashCode(string obj)
        {
            char @char;
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            else if (obj.Length == 0)
            {
                return obj.GetHashCode();
            }
            else if ((@char = char.ToLowerInvariant(obj[0])) != obj[0])
            {
                var array = obj.ToArray();
                array[0] = @char;
                return new string(array).GetHashCode();
            }
            else
            {
                return obj.GetHashCode();
            }
        }
    }
}
