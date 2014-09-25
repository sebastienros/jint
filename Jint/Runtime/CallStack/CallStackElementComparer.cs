namespace Jint.Runtime.CallStack
{
    using System.Collections.Generic;

    public class CallStackElementComparer: IEqualityComparer<CallStackElement>
    {
        public bool Equals(CallStackElement x, CallStackElement y)
        {
            return x.CallExpression == y.CallExpression || x.Function == y.Function;
        }

        public int GetHashCode(CallStackElement obj)
        {
            // TO DO have not found good hash function yet
            // not sure it can be done for OR logic
            return 0;
        }
    }
}
