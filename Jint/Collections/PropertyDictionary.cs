using System.Collections.Generic;
using Jint.Runtime.Descriptors;

namespace Jint.Collections
{
    internal sealed class PropertyDictionary : HybridDictionary<string, PropertyDescriptor>
    {
        public PropertyDictionary()
        {
        }

        public PropertyDictionary(int capacity) : base(capacity)
        {
        }
    }
}