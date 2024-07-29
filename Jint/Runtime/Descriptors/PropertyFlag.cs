namespace Jint.Runtime.Descriptors;

[Flags]
public enum PropertyFlag
{
    None = 0,
    Enumerable = 1,
    EnumerableSet = 2,
    Writable = 4,
    WritableSet = 8,
    Configurable = 16,
    ConfigurableSet = 32,

    CustomJsValue = 256,

    // we can check for mutable binding and do some fast assignments
    MutableBinding = 512,

    // mark PropertyDescriptor as non data to accelerate IsDataDescriptor and avoid the side effect of CustomValue
    NonData = 1024,

    // common helpers
    AllForbidden = ConfigurableSet | EnumerableSet | WritableSet,
    ConfigurableEnumerableWritable = Configurable | Enumerable | Writable,
    NonConfigurable = ConfigurableSet | Enumerable | Writable,
    OnlyEnumerable = Enumerable | ConfigurableSet | WritableSet,
    NonEnumerable = Configurable | EnumerableSet | Writable,
    OnlyWritable = EnumerableSet | Writable | ConfigurableSet
}
