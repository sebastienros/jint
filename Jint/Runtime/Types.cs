namespace Jint.Runtime;

[Flags]
public enum Types
{
    Empty = 0,
    Undefined = 1,
    Null = 2,
    Boolean = 4,
    String = 8,
    Number = 16,
    Symbol = 64,
    BigInt = 128,
    Object = 256
}
