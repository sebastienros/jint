using Jint.Native;
using Jint.Runtime.Descriptors;

namespace Jint.Collections
{
    internal sealed class SymbolDictionary : DictionarySlim<JsSymbol, PropertyDescriptor>
    {
        public SymbolDictionary()
        {
        }

        public SymbolDictionary(int capacity) : base(capacity)
        {
        }
    }
}
