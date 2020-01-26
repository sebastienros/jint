using Jint.Runtime.Descriptors;

namespace Jint.Collections
{
    internal sealed class PropertyDictionary : HybridDictionary<PropertyDescriptor>
    {
        public PropertyDictionary()
        {
        }

        public PropertyDictionary(int capacity, bool checkExistingKeys) : base(capacity, checkExistingKeys)
        {
        }
    }
}