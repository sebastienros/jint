using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Jint.Runtime
{
    [Serializable]
    public class PromiseRejectedException : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public PromiseRejectedException()
        {
        }

        public PromiseRejectedException(string message) : base(message)
        {
        }

        public PromiseRejectedException(string message, Exception inner) : base(message, inner)
        {
        }

        protected PromiseRejectedException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}
