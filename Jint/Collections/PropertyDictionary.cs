using Jint.Runtime.Descriptors;

namespace Jint.Collections
{
    internal sealed class PropertyDictionary : DictionarySlim<string, PropertyDescriptor>
    {
        public PropertyDictionary()
        {
        }

        public PropertyDictionary(int capacity) : base(capacity)
        {
        }
    }
}