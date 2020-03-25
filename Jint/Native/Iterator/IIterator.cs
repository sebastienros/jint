using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Iterator
{
    public interface IIterator
    {
        bool TryIteratorStep(out ObjectInstance nextItem);
        void Close(CompletionType completion);
    }
}