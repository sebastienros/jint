//HintName: ArrayPrototype.g.cs
#nullable enable

#pragma warning disable CS0219

using System.Diagnostics;
using System.Runtime.CompilerServices;

using Jint.HighPerformance;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Array;

public partial class ArrayPrototype
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __constructor_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __constructor_property
    {
        [DebuggerStepThrough]
        get { return __constructor_property_backingField ??= new PropertyDescriptor(Constructor, PropertyFlag.AllForbidden); }
    }



    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __at_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __at { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __at_backingField ??= new AtFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __at_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __at_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __at_property_backingField ??= new PropertyDescriptor(__at, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __concat_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __concat { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __concat_backingField ??= new ConcatFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __concat_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __concat_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __concat_property_backingField ??= new PropertyDescriptor(__concat, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __copyWithin_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __copyWithin { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __copyWithin_backingField ??= new CopyWithinFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __copyWithin_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __copyWithin_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __copyWithin_property_backingField ??= new PropertyDescriptor(__copyWithin, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __entries_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __entries { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __entries_backingField ??= new EntriesFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __entries_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __entries_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __entries_property_backingField ??= new PropertyDescriptor(__entries, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __every_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __every { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __every_backingField ??= new EveryFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __every_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __every_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __every_property_backingField ??= new PropertyDescriptor(__every, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __fill_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __fill { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __fill_backingField ??= new FillFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __fill_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __fill_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __fill_property_backingField ??= new PropertyDescriptor(__fill, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __filter_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __filter { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __filter_backingField ??= new FilterFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __filter_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __filter_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __filter_property_backingField ??= new PropertyDescriptor(__filter, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __find_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __find { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __find_backingField ??= new FindFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __find_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __find_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __find_property_backingField ??= new PropertyDescriptor(__find, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __findIndex_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __findIndex { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __findIndex_backingField ??= new FindIndexFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __findIndex_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __findIndex_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __findIndex_property_backingField ??= new PropertyDescriptor(__findIndex, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __findLast_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __findLast { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __findLast_backingField ??= new FindLastFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __findLast_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __findLast_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __findLast_property_backingField ??= new PropertyDescriptor(__findLast, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __findLastIndex_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __findLastIndex { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __findLastIndex_backingField ??= new FindLastIndexFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __findLastIndex_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __findLastIndex_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __findLastIndex_property_backingField ??= new PropertyDescriptor(__findLastIndex, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __flat_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __flat { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __flat_backingField ??= new FlatFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __flat_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __flat_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __flat_property_backingField ??= new PropertyDescriptor(__flat, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __flatMap_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __flatMap { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __flatMap_backingField ??= new FlatMapFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __flatMap_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __flatMap_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __flatMap_property_backingField ??= new PropertyDescriptor(__flatMap, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __forEach_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __forEach { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __forEach_backingField ??= new ForEachFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __forEach_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __forEach_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __forEach_property_backingField ??= new PropertyDescriptor(__forEach, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __includes_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __includes { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __includes_backingField ??= new IncludesFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __includes_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __includes_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __includes_property_backingField ??= new PropertyDescriptor(__includes, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __indexOf_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __indexOf { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __indexOf_backingField ??= new IndexOfFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __indexOf_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __indexOf_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __indexOf_property_backingField ??= new PropertyDescriptor(__indexOf, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __join_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __join { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __join_backingField ??= new JoinFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __join_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __join_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __join_property_backingField ??= new PropertyDescriptor(__join, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __keys_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __keys { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __keys_backingField ??= new KeysFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __keys_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __keys_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __keys_property_backingField ??= new PropertyDescriptor(__keys, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __lastIndexOf_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __lastIndexOf { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __lastIndexOf_backingField ??= new LastIndexOfFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __lastIndexOf_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __lastIndexOf_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __lastIndexOf_property_backingField ??= new PropertyDescriptor(__lastIndexOf, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __map_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __map { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __map_backingField ??= new MapFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __map_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __map_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __map_property_backingField ??= new PropertyDescriptor(__map, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __pop_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __pop { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __pop_backingField ??= new PopFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __pop_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __pop_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __pop_property_backingField ??= new PropertyDescriptor(__pop, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __push_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __push { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __push_backingField ??= new PushFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __push_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __push_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __push_property_backingField ??= new PropertyDescriptor(__push, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __reduce_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __reduce { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __reduce_backingField ??= new ReduceFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __reduce_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __reduce_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __reduce_property_backingField ??= new PropertyDescriptor(__reduce, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __reduceRight_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __reduceRight { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __reduceRight_backingField ??= new ReduceRightFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __reduceRight_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __reduceRight_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __reduceRight_property_backingField ??= new PropertyDescriptor(__reduceRight, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __reverse_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __reverse { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __reverse_backingField ??= new ReverseFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __reverse_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __reverse_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __reverse_property_backingField ??= new PropertyDescriptor(__reverse, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __shift_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __shift { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __shift_backingField ??= new ShiftFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __shift_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __shift_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __shift_property_backingField ??= new PropertyDescriptor(__shift, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __slice_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __slice { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __slice_backingField ??= new SliceFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __slice_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __slice_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __slice_property_backingField ??= new PropertyDescriptor(__slice, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __some_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __some { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __some_backingField ??= new SomeFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __some_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __some_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __some_property_backingField ??= new PropertyDescriptor(__some, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __sort_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __sort { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __sort_backingField ??= new SortFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __sort_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __sort_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __sort_property_backingField ??= new PropertyDescriptor(__sort, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __splice_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __splice { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __splice_backingField ??= new SpliceFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __splice_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __splice_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __splice_property_backingField ??= new PropertyDescriptor(__splice, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __toLocaleString_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __toLocaleString { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __toLocaleString_backingField ??= new ToLocaleStringFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __toLocaleString_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __toLocaleString_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __toLocaleString_property_backingField ??= new PropertyDescriptor(__toLocaleString, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __toString_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __toString { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __toString_backingField ??= new ToStringFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __toString_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __toString_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __toString_property_backingField ??= new PropertyDescriptor(__toString, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __unshift_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __unshift { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __unshift_backingField ??= new UnshiftFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __unshift_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __unshift_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __unshift_property_backingField ??= new PropertyDescriptor(__unshift, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __values_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __values { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __values_backingField ??= new ValuesFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __values_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __values_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __values_property_backingField ??= new PropertyDescriptor(__values, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    protected override void Initialize()
    {
        CreateProperties();
        CreateSymbols();
    }

    protected override bool TryGetProperty(JsValue property, out PropertyDescriptor? descriptor)
    {
        if (property is JsString jsString)
        {
            var str = jsString._value;
            PropertyDescriptor? match = null;
            switch (str.Length)
            {
                case 2:
                    if (str == "at")
                    {
                        match = __at_property;
                    }
                    break;

                case 3:
                    var disc3 = str[0];
                    if (disc3 == 'm' && str == "map")
                    {
                        match = __map_property;
                    }
                    else if (disc3 == 'p' && str == "pop")
                    {
                        match = __pop_property;
                    }
                    break;

                case 4:
                    var disc4 = str[2];
                    if (disc4 == 'l' && str == "fill")
                    {
                        match = __fill_property;
                    }
                    else if (disc4 == 'n' && str == "find")
                    {
                        match = __find_property;
                    }
                    else if (disc4 == 'a' && str == "flat")
                    {
                        match = __flat_property;
                    }
                    else if (disc4 == 'i' && str == "join")
                    {
                        match = __join_property;
                    }
                    else if (disc4 == 'y' && str == "keys")
                    {
                        match = __keys_property;
                    }
                    else if (disc4 == 's' && str == "push")
                    {
                        match = __push_property;
                    }
                    else if (disc4 == 'm' && str == "some")
                    {
                        match = __some_property;
                    }
                    else if (disc4 == 'r' && str == "sort")
                    {
                        match = __sort_property;
                    }
                    break;

                case 5:
                    var disc5 = str[1];
                    if (disc5 == 'v' && str == "every")
                    {
                        match = __every_property;
                    }
                    else if (disc5 == 'h' && str == "shift")
                    {
                        match = __shift_property;
                    }
                    else if (disc5 == 'l' && str == "slice")
                    {
                        match = __slice_property;
                    }
                    break;

                case 6:
                    var disc6 = str[0];
                    if (disc6 == 'c' && str == "concat")
                    {
                        match = __concat_property;
                    }
                    else if (disc6 == 'f' && str == "filter")
                    {
                        match = __filter_property;
                    }
                    else if (disc6 == 'r' && str == "reduce")
                    {
                        match = __reduce_property;
                    }
                    else if (disc6 == 's' && str == "splice")
                    {
                        match = __splice_property;
                    }
                    else if (disc6 == 'v' && str == "values")
                    {
                        match = __values_property;
                    }
                    break;

                case 7:
                    var disc7 = str[2];
                    if (disc7 == 't' && str == "entries")
                    {
                        match = __entries_property;
                    }
                    else if (disc7 == 'a' && str == "flatMap")
                    {
                        match = __flatMap_property;
                    }
                    else if (disc7 == 'r' && str == "forEach")
                    {
                        match = __forEach_property;
                    }
                    else if (disc7 == 'd' && str == "indexOf")
                    {
                        match = __indexOf_property;
                    }
                    else if (disc7 == 'v' && str == "reverse")
                    {
                        match = __reverse_property;
                    }
                    else if (disc7 == 's' && str == "unshift")
                    {
                        match = __unshift_property;
                    }
                    break;

                case 8:
                    var disc8 = str[0];
                    if (disc8 == 'f' && str == "findLast")
                    {
                        match = __findLast_property;
                    }
                    else if (disc8 == 'i' && str == "includes")
                    {
                        match = __includes_property;
                    }
                    else if (disc8 == 't' && str == "toString")
                    {
                        match = __toString_property;
                    }
                    break;

                case 9:
                    if (str == "findIndex")
                    {
                        match = __findIndex_property;
                    }
                    break;

                case 10:
                    if (str == "copyWithin")
                    {
                        match = __copyWithin_property;
                    }
                    break;

                case 11:
                    var disc11 = str[0];
                    if (disc11 == 'l' && str == "lastIndexOf")
                    {
                        match = __lastIndexOf_property;
                    }
                    else if (disc11 == 'r' && str == "reduceRight")
                    {
                        match = __reduceRight_property;
                    }
                    else if (disc11 == 'c' && str == "constructor")
                    {
                        match = __constructor_property;
                    }
                    break;

                case 13:
                    if (str == "findLastIndex")
                    {
                        match = __findLastIndex_property;
                    }
                    break;

                case 14:
                    if (str == "toLocaleString")
                    {
                        match = __toLocaleString_property;
                    }
                    break;

            }

            if (match is not null)
            {
                descriptor = match;
                return true;
            }
        }
        return base.TryGetProperty(property, out descriptor);
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (property is JsString jsString)
        {
            var str = jsString._value;
            PropertyDescriptor? match = null;
            switch (str.Length)
            {
                case 2:
                    if (str == "at")
                    {
                        match = __at_property;
                    }
                    break;

                case 3:
                    var disc3 = str[0];
                    if (disc3 == 'm' && str == "map")
                    {
                        match = __map_property;
                    }
                    else if (disc3 == 'p' && str == "pop")
                    {
                        match = __pop_property;
                    }
                    break;

                case 4:
                    var disc4 = str[2];
                    if (disc4 == 'l' && str == "fill")
                    {
                        match = __fill_property;
                    }
                    else if (disc4 == 'n' && str == "find")
                    {
                        match = __find_property;
                    }
                    else if (disc4 == 'a' && str == "flat")
                    {
                        match = __flat_property;
                    }
                    else if (disc4 == 'i' && str == "join")
                    {
                        match = __join_property;
                    }
                    else if (disc4 == 'y' && str == "keys")
                    {
                        match = __keys_property;
                    }
                    else if (disc4 == 's' && str == "push")
                    {
                        match = __push_property;
                    }
                    else if (disc4 == 'm' && str == "some")
                    {
                        match = __some_property;
                    }
                    else if (disc4 == 'r' && str == "sort")
                    {
                        match = __sort_property;
                    }
                    break;

                case 5:
                    var disc5 = str[1];
                    if (disc5 == 'v' && str == "every")
                    {
                        match = __every_property;
                    }
                    else if (disc5 == 'h' && str == "shift")
                    {
                        match = __shift_property;
                    }
                    else if (disc5 == 'l' && str == "slice")
                    {
                        match = __slice_property;
                    }
                    break;

                case 6:
                    var disc6 = str[0];
                    if (disc6 == 'c' && str == "concat")
                    {
                        match = __concat_property;
                    }
                    else if (disc6 == 'f' && str == "filter")
                    {
                        match = __filter_property;
                    }
                    else if (disc6 == 'r' && str == "reduce")
                    {
                        match = __reduce_property;
                    }
                    else if (disc6 == 's' && str == "splice")
                    {
                        match = __splice_property;
                    }
                    else if (disc6 == 'v' && str == "values")
                    {
                        match = __values_property;
                    }
                    break;

                case 7:
                    var disc7 = str[2];
                    if (disc7 == 't' && str == "entries")
                    {
                        match = __entries_property;
                    }
                    else if (disc7 == 'a' && str == "flatMap")
                    {
                        match = __flatMap_property;
                    }
                    else if (disc7 == 'r' && str == "forEach")
                    {
                        match = __forEach_property;
                    }
                    else if (disc7 == 'd' && str == "indexOf")
                    {
                        match = __indexOf_property;
                    }
                    else if (disc7 == 'v' && str == "reverse")
                    {
                        match = __reverse_property;
                    }
                    else if (disc7 == 's' && str == "unshift")
                    {
                        match = __unshift_property;
                    }
                    break;

                case 8:
                    var disc8 = str[0];
                    if (disc8 == 'f' && str == "findLast")
                    {
                        match = __findLast_property;
                    }
                    else if (disc8 == 'i' && str == "includes")
                    {
                        match = __includes_property;
                    }
                    else if (disc8 == 't' && str == "toString")
                    {
                        match = __toString_property;
                    }
                    break;

                case 9:
                    if (str == "findIndex")
                    {
                        match = __findIndex_property;
                    }
                    break;

                case 10:
                    if (str == "copyWithin")
                    {
                        match = __copyWithin_property;
                    }
                    break;

                case 11:
                    var disc11 = str[0];
                    if (disc11 == 'l' && str == "lastIndexOf")
                    {
                        match = __lastIndexOf_property;
                    }
                    else if (disc11 == 'r' && str == "reduceRight")
                    {
                        match = __reduceRight_property;
                    }
                    else if (disc11 == 'c' && str == "constructor")
                    {
                        match = __constructor_property;
                    }
                    break;

                case 13:
                    if (str == "findLastIndex")
                    {
                        match = __findLastIndex_property;
                    }
                    break;

                case 14:
                    if (str == "toLocaleString")
                    {
                        match = __toLocaleString_property;
                    }
                    break;

            }

            if (match is not null)
            {
                return match;
            }
        }
        return base.GetOwnProperty(property);
    }

    protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (property is JsString jsString)
        {
            var str = jsString._value;
            PropertyDescriptor? match = null;
            switch (str.Length)
            {
                case 2:
                    if (str == "at")
                    {
                        match = __at_property;
                    }
                    break;

                case 3:
                    var disc3 = str[0];
                    if (disc3 == 'm' && str == "map")
                    {
                        match = __map_property;
                    }
                    else if (disc3 == 'p' && str == "pop")
                    {
                        match = __pop_property;
                    }
                    break;

                case 4:
                    var disc4 = str[2];
                    if (disc4 == 'l' && str == "fill")
                    {
                        match = __fill_property;
                    }
                    else if (disc4 == 'n' && str == "find")
                    {
                        match = __find_property;
                    }
                    else if (disc4 == 'a' && str == "flat")
                    {
                        match = __flat_property;
                    }
                    else if (disc4 == 'i' && str == "join")
                    {
                        match = __join_property;
                    }
                    else if (disc4 == 'y' && str == "keys")
                    {
                        match = __keys_property;
                    }
                    else if (disc4 == 's' && str == "push")
                    {
                        match = __push_property;
                    }
                    else if (disc4 == 'm' && str == "some")
                    {
                        match = __some_property;
                    }
                    else if (disc4 == 'r' && str == "sort")
                    {
                        match = __sort_property;
                    }
                    break;

                case 5:
                    var disc5 = str[1];
                    if (disc5 == 'v' && str == "every")
                    {
                        match = __every_property;
                    }
                    else if (disc5 == 'h' && str == "shift")
                    {
                        match = __shift_property;
                    }
                    else if (disc5 == 'l' && str == "slice")
                    {
                        match = __slice_property;
                    }
                    break;

                case 6:
                    var disc6 = str[0];
                    if (disc6 == 'c' && str == "concat")
                    {
                        match = __concat_property;
                    }
                    else if (disc6 == 'f' && str == "filter")
                    {
                        match = __filter_property;
                    }
                    else if (disc6 == 'r' && str == "reduce")
                    {
                        match = __reduce_property;
                    }
                    else if (disc6 == 's' && str == "splice")
                    {
                        match = __splice_property;
                    }
                    else if (disc6 == 'v' && str == "values")
                    {
                        match = __values_property;
                    }
                    break;

                case 7:
                    var disc7 = str[2];
                    if (disc7 == 't' && str == "entries")
                    {
                        match = __entries_property;
                    }
                    else if (disc7 == 'a' && str == "flatMap")
                    {
                        match = __flatMap_property;
                    }
                    else if (disc7 == 'r' && str == "forEach")
                    {
                        match = __forEach_property;
                    }
                    else if (disc7 == 'd' && str == "indexOf")
                    {
                        match = __indexOf_property;
                    }
                    else if (disc7 == 'v' && str == "reverse")
                    {
                        match = __reverse_property;
                    }
                    else if (disc7 == 's' && str == "unshift")
                    {
                        match = __unshift_property;
                    }
                    break;

                case 8:
                    var disc8 = str[0];
                    if (disc8 == 'f' && str == "findLast")
                    {
                        match = __findLast_property;
                    }
                    else if (disc8 == 'i' && str == "includes")
                    {
                        match = __includes_property;
                    }
                    else if (disc8 == 't' && str == "toString")
                    {
                        match = __toString_property;
                    }
                    break;

                case 9:
                    if (str == "findIndex")
                    {
                        match = __findIndex_property;
                    }
                    break;

                case 10:
                    if (str == "copyWithin")
                    {
                        match = __copyWithin_property;
                    }
                    break;

                case 11:
                    var disc11 = str[0];
                    if (disc11 == 'l' && str == "lastIndexOf")
                    {
                        match = __lastIndexOf_property;
                    }
                    else if (disc11 == 'r' && str == "reduceRight")
                    {
                        match = __reduceRight_property;
                    }
                    else if (disc11 == 'c' && str == "constructor")
                    {
                        match = __constructor_property;
                    }
                    break;

                case 13:
                    if (str == "findLastIndex")
                    {
                        match = __findLastIndex_property;
                    }
                    break;

                case 14:
                    if (str == "toLocaleString")
                    {
                        match = __toLocaleString_property;
                    }
                    break;

            }

            if (match is not null)
            {
throw new System.Exception("TROUBLE");
          //      return;
            }
        }
    }
    private sealed class AtFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("at");
        private readonly ArrayPrototype _host;

        public AtFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.At(thisObject, arguments.At(0));
        }

        public override string ToString() => "function at() { [native code] }";
    }

    private sealed class ConcatFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("concat");
        private readonly ArrayPrototype _host;

        public ConcatFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Concat(thisObject, arguments);
        }

        public override string ToString() => "function concat() { [native code] }";
    }

    private sealed class CopyWithinFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("copyWithin");
        private readonly ArrayPrototype _host;

        public CopyWithinFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(99), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.CopyWithin(thisObject, arguments.At(0), arguments.At(1), arguments.At(2));
        }

        public override string ToString() => "function copyWithin() { [native code] }";
    }

    private sealed class EntriesFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("entries");
        private readonly ArrayPrototype _host;

        public EntriesFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(0), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Entries(thisObject);
        }

        public override string ToString() => "function entries() { [native code] }";
    }

    private sealed class EveryFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("every");
        private readonly ArrayPrototype _host;

        public EveryFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Every(thisObject, arguments.At(0), arguments.At(1));
        }

        public override string ToString() => "function every() { [native code] }";
    }

    private sealed class FillFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("fill");
        private readonly ArrayPrototype _host;

        public FillFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Fill(thisObject, arguments.At(0), arguments.At(1), arguments.At(2));
        }

        public override string ToString() => "function fill() { [native code] }";
    }

    private sealed class FilterFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("filter");
        private readonly ArrayPrototype _host;

        public FilterFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Filter(thisObject, arguments.At(0), arguments.At(1));
        }

        public override string ToString() => "function filter() { [native code] }";
    }

    private sealed class FindFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("find");
        private readonly ArrayPrototype _host;

        public FindFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Find(thisObject, arguments);
        }

        public override string ToString() => "function find() { [native code] }";
    }

    private sealed class FindIndexFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("findIndex");
        private readonly ArrayPrototype _host;

        public FindIndexFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.FindIndex(thisObject, arguments);
        }

        public override string ToString() => "function findIndex() { [native code] }";
    }

    private sealed class FindLastFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("findLast");
        private readonly ArrayPrototype _host;

        public FindLastFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.FindLast(thisObject, arguments);
        }

        public override string ToString() => "function findLast() { [native code] }";
    }

    private sealed class FindLastIndexFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("findLastIndex");
        private readonly ArrayPrototype _host;

        public FindLastIndexFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.FindLastIndex(thisObject, arguments);
        }

        public override string ToString() => "function findLastIndex() { [native code] }";
    }

    private sealed class FlatFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("flat");
        private readonly ArrayPrototype _host;

        public FlatFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(0), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Flat(thisObject, arguments.At(0));
        }

        public override string ToString() => "function flat() { [native code] }";
    }

    private sealed class FlatMapFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("flatMap");
        private readonly ArrayPrototype _host;

        public FlatMapFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.FlatMap(thisObject, arguments.At(0), arguments.At(1));
        }

        public override string ToString() => "function flatMap() { [native code] }";
    }

    private sealed class ForEachFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("forEach");
        private readonly ArrayPrototype _host;

        public ForEachFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.ForEach(thisObject, arguments.At(0), arguments.At(1));
        }

        public override string ToString() => "function forEach() { [native code] }";
    }

    private sealed class IncludesFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("includes");
        private readonly ArrayPrototype _host;

        public IncludesFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Includes(thisObject, arguments.At(0), arguments.At(1));
        }

        public override string ToString() => "function includes() { [native code] }";
    }

    private sealed class IndexOfFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("indexOf");
        private readonly ArrayPrototype _host;

        public IndexOfFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.IndexOf(thisObject, arguments);
        }

        public override string ToString() => "function indexOf() { [native code] }";
    }

    private sealed class JoinFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("join");
        private readonly ArrayPrototype _host;

        public JoinFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Join(thisObject, arguments.At(0));
        }

        public override string ToString() => "function join() { [native code] }";
    }

    private sealed class KeysFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("keys");
        private readonly ArrayPrototype _host;

        public KeysFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(0), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Keys(thisObject);
        }

        public override string ToString() => "function keys() { [native code] }";
    }

    private sealed class LastIndexOfFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("lastIndexOf");
        private readonly ArrayPrototype _host;

        public LastIndexOfFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.LastIndexOf(thisObject, arguments);
        }

        public override string ToString() => "function lastIndexOf() { [native code] }";
    }

    private sealed class MapFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("map");
        private readonly ArrayPrototype _host;

        public MapFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Map(thisObject, arguments);
        }

        public override string ToString() => "function map() { [native code] }";
    }

    private sealed class PopFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("pop");
        private readonly ArrayPrototype _host;

        public PopFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(0), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Pop(thisObject);
        }

        public override string ToString() => "function pop() { [native code] }";
    }

    private sealed class PushFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("push");
        private readonly ArrayPrototype _host;

        public PushFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Push(thisObject, arguments);
        }

        public override string ToString() => "function push() { [native code] }";
    }

    private sealed class ReduceFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("reduce");
        private readonly ArrayPrototype _host;

        public ReduceFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Reduce(thisObject, arguments);
        }

        public override string ToString() => "function reduce() { [native code] }";
    }

    private sealed class ReduceRightFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("reduceRight");
        private readonly ArrayPrototype _host;

        public ReduceRightFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.ReduceRight(thisObject, arguments);
        }

        public override string ToString() => "function reduceRight() { [native code] }";
    }

    private sealed class ReverseFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("reverse");
        private readonly ArrayPrototype _host;

        public ReverseFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(0), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Reverse(thisObject);
        }

        public override string ToString() => "function reverse() { [native code] }";
    }

    private sealed class ShiftFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("shift");
        private readonly ArrayPrototype _host;

        public ShiftFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(0), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Shift(thisObject);
        }

        public override string ToString() => "function shift() { [native code] }";
    }

    private sealed class SliceFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("slice");
        private readonly ArrayPrototype _host;

        public SliceFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Slice(thisObject, arguments);
        }

        public override string ToString() => "function slice() { [native code] }";
    }

    private sealed class SomeFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("some");
        private readonly ArrayPrototype _host;

        public SomeFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Some(thisObject, arguments);
        }

        public override string ToString() => "function some() { [native code] }";
    }

    private sealed class SortFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("sort");
        private readonly ArrayPrototype _host;

        public SortFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Sort(thisObject, arguments);
        }

        public override string ToString() => "function sort() { [native code] }";
    }

    private sealed class SpliceFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("splice");
        private readonly ArrayPrototype _host;

        public SpliceFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(3), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Splice(thisObject, arguments.At(0), arguments.At(1), arguments);
        }

        public override string ToString() => "function splice() { [native code] }";
    }

    private sealed class ToLocaleStringFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("toLocaleString");
        private readonly ArrayPrototype _host;

        public ToLocaleStringFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(0), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.ToLocaleString(thisObject);
        }

        public override string ToString() => "function toLocaleString() { [native code] }";
    }

    private sealed class ToStringFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("toString");
        private readonly ArrayPrototype _host;

        public ToStringFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(0), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.ToString(thisObject, arguments);
        }

        public override string ToString() => "function toString() { [native code] }";
    }

    private sealed class UnshiftFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("unshift");
        private readonly ArrayPrototype _host;

        public UnshiftFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Unshift(thisObject, arguments);
        }

        public override string ToString() => "function unshift() { [native code] }";
    }

    private sealed class ValuesFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("values");
        private readonly ArrayPrototype _host;

        public ValuesFunction(ArrayPrototype host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(0), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Values(thisObject, arguments);
        }

        public override string ToString() => "function values() { [native code] }";
    }

}
