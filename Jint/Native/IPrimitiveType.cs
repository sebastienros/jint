using System;

namespace Jint.Native
{
    public interface IPrimitiveType
    {
        TypeCode TypeCode { get; } 
        object PrimitiveValue { get; }
    }
}
