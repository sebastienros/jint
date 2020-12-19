#nullable enable

using System.Collections.Generic;

namespace Jint.Runtime.CallStack
{
    internal sealed class CallStackElementComparer: IEqualityComparer<CallStackElement>
    {
        public static readonly CallStackElementComparer Instance = new();
        
        private CallStackElementComparer()
        {
        }

        public bool Equals(CallStackElement x, CallStackElement y)
        {
            return ReferenceEquals(x.Function, y.Function);
        }

        public int GetHashCode(CallStackElement obj)
        {
            return obj.Function.GetHashCode();
        }
    }
}
