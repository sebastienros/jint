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

    // Common helpers: the eight full data-attribute combinations, each naming a descriptor in which all three
    // of writable/enumerable/configurable are decided. Non<X> is "all on except X, explicitly off"; Only<X> is
    // "X on, the other two explicitly off". Partial spellings such as `Configurable | Writable`, which leave an
    // attribute undecided so a merge can fill it in, are deliberately not named here.
    AllForbidden = ConfigurableSet | EnumerableSet | WritableSet,
    ConfigurableEnumerableWritable = Configurable | Enumerable | Writable,
    NonConfigurable = ConfigurableSet | Enumerable | Writable,
    OnlyEnumerable = Enumerable | ConfigurableSet | WritableSet,
    NonEnumerable = Configurable | EnumerableSet | Writable,
    OnlyWritable = EnumerableSet | Writable | ConfigurableSet,

    /// <summary>
    /// <c>{ writable: false, enumerable: true, configurable: true }</c> — read-only data that the owner can
    /// still redefine or delete. The shape for a projected host member scripts may read and enumerate but
    /// never assign.
    /// </summary>
    NonWritable = Configurable | Enumerable | WritableSet,

    /// <summary>
    /// <c>{ writable: false, enumerable: false, configurable: true }</c> — configurable only. The attribute
    /// shape the specification gives well-known-symbol members such as <c>Symbol.toStringTag</c>, and the one
    /// for a host member that must stay redefinable because its value is not stable between reads.
    /// </summary>
    OnlyConfigurable = Configurable | EnumerableSet | WritableSet
}
