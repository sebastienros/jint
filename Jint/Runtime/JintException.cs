using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jint.Runtime
{
    public abstract class JintException : Exception
    {
        public JintException()
        {
        }

        public JintException(string message)
            : base(message)
        {

        }
    }
}
