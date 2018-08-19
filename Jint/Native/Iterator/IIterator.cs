using Jint.Native.Object;

namespace Jint.Native.Iterator
{
    public interface IIterator
    {
        ObjectInstance Next();
        void Return();
    }
}