using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    public class CamelCasedProperties
    {
        public IStringComparer PropertiesStringComparer { get; set; }
        public IStringComparer FieldsStringComparer { get; set; }
        public IStringComparer MethodsStringComparer { get; set; }
    }
}
