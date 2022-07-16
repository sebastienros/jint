namespace Jint.Native.Object;

/// <summary>
/// Keeps track of changes to object, mainly meant for prototype change detection.
/// </summary>
[Flags]
internal enum ObjectChangeFlags
{
    None = 0,
    Property = 1,
    Symbol = 2,
    ArrayIndex = 4,
    NonDefaultDataDescriptorUsage = 8
}
