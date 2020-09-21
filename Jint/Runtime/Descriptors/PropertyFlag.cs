using System;

namespace Jint.Runtime.Descriptors
{
    [Flags]
    public enum PropertyFlag
    {
        None = 0,
        /// <summary>
        /// 可数
        /// </summary>
        Enumerable = 1,
        /// <summary>
        /// 可数集
        /// </summary>
        EnumerableSet = 2,
        /// <summary>
        /// 可写
        /// </summary>
        Writable = 4,
        /// <summary>
        /// 可写集
        /// </summary>
        WritableSet = 8,
        /// <summary>
        /// 可配置
        /// </summary>
        Configurable = 16,
        /// <summary>
        /// 可配置集
        /// </summary>
        ConfigurableSet = 32,
        /// <summary>
        /// 自定义Js值
        /// </summary>
        CustomJsValue = 256,
        /// <summary>
        /// 可变绑定
        /// </summary>
        // we can check for mutable binding and do some fast assignments
        MutableBinding = 512,

        // common helpers
        AllForbidden = ConfigurableSet | EnumerableSet | WritableSet,
        ConfigurableEnumerableWritable = Configurable | Enumerable | Writable,
        NonConfigurable = ConfigurableSet | Enumerable | Writable,
        OnlyEnumerable = Enumerable | ConfigurableSet | WritableSet,
        NonEnumerable = Configurable | EnumerableSet | Writable,
        OnlyWritable = EnumerableSet | Writable | ConfigurableSet
    }
}