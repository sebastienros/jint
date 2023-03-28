#if !NET6_0_OR_GREATER

namespace System.Runtime.CompilerServices;

[AttributeUsage(
    AttributeTargets.Module
    | AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Interface
    | AttributeTargets.Constructor
    | AttributeTargets.Method
    | AttributeTargets.Property
    | AttributeTargets.Event, Inherited = false)]
internal sealed class SkipLocalsInitAttribute : Attribute
{
    public SkipLocalsInitAttribute() { }
}

#endif
