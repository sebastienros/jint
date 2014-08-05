using System;

namespace Jint.Runtime.Interop
{
    public class JintInteropException: Exception
    {
        public JintInteropException(Exception innerException)
            : base("", innerException)
        {
        }
    }
}
