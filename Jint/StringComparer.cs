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

        public int GetHashCode(string obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            else if (obj.Length == 0)
            {
                return obj.GetHashCode();
            }
            else if (char.IsUpper(obj[0]))
            {
                var array = obj.ToArray();
                array[0] = char.ToLower(array[0]);
                return new string(array).GetHashCode();
            }
            else
            {
                return obj.GetHashCode();
            }
        }
    }
}
