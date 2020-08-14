using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    public class CamelCasedProperties
    {
        public IEqualityComparer<string> PropertiesStringComparer { get; set; }
        public IEqualityComparer<string> FieldsStringComparer { get; set; }
        public IEqualityComparer<string> MethodsStringComparer { get; set; }
    }
}
