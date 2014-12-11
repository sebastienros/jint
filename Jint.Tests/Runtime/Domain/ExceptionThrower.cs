using System;

namespace Jint.Tests.Runtime.Domain
{
    public class ExceptionThrower
    {
        public void Execute()
        {
            throw new ApplicationException("Runtime exception");
        }
    }
}
