namespace Jint.Native
{
    public interface ICallable
    {
        object Call(object thisObject, object[] arguments);
    }
}