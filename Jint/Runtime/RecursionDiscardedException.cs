using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jint.Runtime
{
    public class RecursionDiscardedException : Exception 
    {
        public RecursionDiscardedException()
            : base("The recursion is forbidden by script host.")
        {
        }
    }
}
