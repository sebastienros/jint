namespace Jint.Runtime
{
    /// <summary>
    /// Workaround for situation where engine is not easily accessible.
    /// </summary>
    internal sealed class TypeErrorException : JintException
    {
        public TypeErrorException(string message) : base(message)
        {
        }
    }
}