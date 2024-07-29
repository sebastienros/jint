namespace Jint.Runtime.CallStack;

internal sealed class CallStackElementComparer: IEqualityComparer<CallStackElement>
{
    public static readonly CallStackElementComparer Instance = new();

    private CallStackElementComparer()
    {
    }

    public bool Equals(CallStackElement x, CallStackElement y)
    {
        if (x.Function._functionDefinition is not null)
        {
            return ReferenceEquals(x.Function._functionDefinition, y.Function._functionDefinition);
        }

        return ReferenceEquals(x.Function, y.Function);
    }

    public int GetHashCode(CallStackElement obj)
    {
        if (obj.Function._functionDefinition is not null)
        {
            return obj.Function._functionDefinition.GetHashCode();
        }
        return obj.Function.GetHashCode();
    }
}
