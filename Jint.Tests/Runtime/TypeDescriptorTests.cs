using System.Collections;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class TypeDescriptorTests
{
    [Test]
    public void AnalyzesBclCollectionTypesCorrectly()
    {
        AssertInformation(typeof(ICollection), isArrayLike: true, iterable: true, isIntegerIndexedArray: false, shouldHaveLength: true);

        AssertInformation(typeof(IList), isArrayLike: true, iterable: true, isIntegerIndexedArray: true, shouldHaveLength: true);
        AssertInformation(typeof(ArrayList), isArrayLike: true, iterable: true, isIntegerIndexedArray: true, shouldHaveLength: true);
        AssertInformation(typeof(string[]), isArrayLike: true, iterable: true, isIntegerIndexedArray: true, shouldHaveLength: true);
        AssertInformation(typeof(List<string>), isArrayLike: true, iterable: true, isIntegerIndexedArray: true, shouldHaveLength: true);
        AssertInformation(typeof(IList<string>), isArrayLike: true, iterable: true, isIntegerIndexedArray: true, shouldHaveLength: true);

        AssertInformation(typeof(Dictionary<string, object>), isArrayLike: false, iterable: true, isIntegerIndexedArray: false, shouldHaveLength: false);
        AssertInformation(typeof(IDictionary<string, object>), isArrayLike: false, iterable: true, isIntegerIndexedArray: false, shouldHaveLength: false);
        AssertInformation(typeof(Dictionary<decimal, object>), isArrayLike: false, iterable: true, isIntegerIndexedArray: false, shouldHaveLength: false);
        AssertInformation(typeof(IDictionary<decimal, object>), isArrayLike: false, iterable: true, isIntegerIndexedArray: false, shouldHaveLength: false);

        AssertInformation(typeof(ISet<string>), isArrayLike: true, iterable: true, isIntegerIndexedArray: false, shouldHaveLength: true);
    }

    /// <summary>
    /// A string-keyed dictionary that is also a list is addressed by its keys alone when the list holds something
    /// other than its values, or when the type declares no integer indexer of its own (#4501). Such a type then has
    /// no integer indexer at all.
    /// </summary>
    [Test]
    public void ADictionaryThatIsAlsoAListIsAddressedByKeyUnlessItListsItsValuesByAnIndexerOfItsOwn()
    {
        AssertAddressing(typeof(EntryList), addressedByKeyOnly: true);
        AssertAddressing(typeof(ExplicitValueList), addressedByKeyOnly: true);
        AssertAddressing(typeof(IndexedValueList), addressedByKeyOnly: false);
        AssertAddressing(typeof(NonGenericExplicitValueList), addressedByKeyOnly: true);
        AssertAddressing(typeof(NonGenericIndexedValueList), addressedByKeyOnly: false);
#if NET9_0_OR_GREATER
        AssertAddressing(typeof(OrderedDictionary<string, int>), addressedByKeyOnly: true);
#endif

        AssertAddressing(typeof(Dictionary<string, int>), addressedByKeyOnly: false);
        AssertAddressing(typeof(System.Dynamic.ExpandoObject), addressedByKeyOnly: false);
        AssertAddressing(typeof(List<int>), addressedByKeyOnly: false);
        AssertAddressing(typeof(int[]), addressedByKeyOnly: false);

        TypeDescriptor.Get(typeof(ExplicitValueList)).IsIntegerIndexed.Should().BeFalse("a type addressed by key has no position to index");
        TypeDescriptor.Get(typeof(IndexedValueList)).IsIntegerIndexed.Should().BeTrue();
        TypeDescriptor.Get(typeof(NonGenericIndexedValueList)).IsIntegerIndexed.Should().BeTrue();
    }

    /// <summary>
    /// A key that is not a string makes no difference (#4527): an int-keyed dictionary whose list holds its entries is
    /// addressed by key, and an integer indexer taking the key type is the dictionary's own, not the list's. One that
    /// declares an integer indexer of another type for a list of its values keeps its positions.
    /// </summary>
    [Test]
    public void ADictionaryWhoseKeysAreNotStringsIsAddressedByKeyOnTheSameTerms()
    {
        AssertAddressing(typeof(IntKeyedEntryList), addressedByKeyOnly: true);
        AssertAddressing(typeof(IntKeyedValueList), addressedByKeyOnly: true);
        AssertAddressing(typeof(LongKeyedIndexedValueList), addressedByKeyOnly: false);
#if NET9_0_OR_GREATER
        AssertAddressing(typeof(OrderedDictionary<int, string>), addressedByKeyOnly: true);
        AssertAddressing(typeof(OrderedDictionary<long, string>), addressedByKeyOnly: true);
        AssertAddressing(typeof(OrderedDictionary<Guid, string>), addressedByKeyOnly: true);
#endif

        AssertAddressing(typeof(Dictionary<int, string>), addressedByKeyOnly: false);
        AssertAddressing(typeof(SortedList<int, string>), addressedByKeyOnly: false);
        AssertAddressing(typeof(SortedDictionary<int, string>), addressedByKeyOnly: false);

        TypeDescriptor.Get(typeof(IntKeyedEntryList)).IsIntegerIndexed.Should().BeFalse("a type addressed by key has no position to index");
        TypeDescriptor.Get(typeof(LongKeyedIndexedValueList)).IsIntegerIndexed.Should().BeTrue();
    }

    private static void AssertAddressing(Type type, bool addressedByKeyOnly)
        => TypeDescriptor.Get(type).IsAddressedByKeyOnly.Should().Be(addressedByKeyOnly, "{0}", type);

    /// <summary>A dictionary whose list holds its entries, the way <c>OrderedDictionary&lt;,&gt;</c>'s does.</summary>
    private sealed class EntryList : Dictionary<string, int>, IList<KeyValuePair<string, int>>
    {
        public KeyValuePair<string, int> this[int index] { get => this.ElementAt(index); set => throw new NotSupportedException(); }

        public int IndexOf(KeyValuePair<string, int> item) => throw new NotSupportedException();

        public void Insert(int index, KeyValuePair<string, int> item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();
    }

    /// <summary>A dictionary that lists its values through the interface alone, the way <c>JObject</c> lists its nodes.</summary>
    private sealed class ExplicitValueList : Dictionary<string, int>, IList<int>
    {
        int IList<int>.this[int index] { get => Values.ElementAt(index); set => throw new NotSupportedException(); }

        bool ICollection<int>.IsReadOnly => true;

        int IList<int>.IndexOf(int item) => throw new NotSupportedException();

        void IList<int>.Insert(int index, int item) => throw new NotSupportedException();

        void IList<int>.RemoveAt(int index) => throw new NotSupportedException();

        void ICollection<int>.Add(int item) => throw new NotSupportedException();

        bool ICollection<int>.Contains(int item) => ContainsValue(item);

        void ICollection<int>.CopyTo(int[] array, int arrayIndex) => Values.CopyTo(array, arrayIndex);

        bool ICollection<int>.Remove(int item) => throw new NotSupportedException();

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => Values.GetEnumerator();
    }

    /// <summary>A dictionary that lists its values and declares the integer indexer itself.</summary>
    private sealed class IndexedValueList : Dictionary<string, int>, IList<int>
    {
        public int this[int index] { get => Values.ElementAt(index); set => throw new NotSupportedException(); }

        bool ICollection<int>.IsReadOnly => true;

        public int IndexOf(int item) => throw new NotSupportedException();

        public void Insert(int index, int item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        void ICollection<int>.Add(int item) => throw new NotSupportedException();

        bool ICollection<int>.Contains(int item) => ContainsValue(item);

        void ICollection<int>.CopyTo(int[] array, int arrayIndex) => Values.CopyTo(array, arrayIndex);

        bool ICollection<int>.Remove(int item) => throw new NotSupportedException();

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => Values.GetEnumerator();
    }

    /// <summary>
    /// A dictionary that is also a non-generic <see cref="IList"/>, implemented explicitly: what it holds is unknown,
    /// and it offers no integer indexer of its own.
    /// </summary>
    private sealed class NonGenericExplicitValueList : Dictionary<string, int>, IList
    {
        object IList.this[int index] { get => Values.ElementAt(index); set => throw new NotSupportedException(); }

        bool IList.IsFixedSize => true;

        bool IList.IsReadOnly => true;

        int IList.Add(object value) => throw new NotSupportedException();

        bool IList.Contains(object value) => value is int item && ContainsValue(item);

        int IList.IndexOf(object value) => throw new NotSupportedException();

        void IList.Insert(int index, object value) => throw new NotSupportedException();

        void IList.Remove(object value) => throw new NotSupportedException();

        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }

    /// <summary>A dictionary that is also a non-generic <see cref="IList"/> of its values, with the indexer declared itself.</summary>
    private sealed class NonGenericIndexedValueList : Dictionary<string, int>, IList
    {
        public object this[int index] { get => Values.ElementAt(index); set => throw new NotSupportedException(); }

        bool IList.IsFixedSize => true;

        bool IList.IsReadOnly => true;

        int IList.Add(object value) => throw new NotSupportedException();

        bool IList.Contains(object value) => value is int item && ContainsValue(item);

        int IList.IndexOf(object value) => throw new NotSupportedException();

        void IList.Insert(int index, object value) => throw new NotSupportedException();

        void IList.Remove(object value) => throw new NotSupportedException();

        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }

    /// <summary>An int-keyed dictionary whose list holds its entries, the way <c>OrderedDictionary&lt;int, TValue&gt;</c>'s does.</summary>
    private sealed class IntKeyedEntryList : Dictionary<int, int>, IList<KeyValuePair<int, int>>
    {
        KeyValuePair<int, int> IList<KeyValuePair<int, int>>.this[int index] { get => this.ElementAt(index); set => throw new NotSupportedException(); }

        int IList<KeyValuePair<int, int>>.IndexOf(KeyValuePair<int, int> item) => throw new NotSupportedException();

        void IList<KeyValuePair<int, int>>.Insert(int index, KeyValuePair<int, int> item) => throw new NotSupportedException();

        void IList<KeyValuePair<int, int>>.RemoveAt(int index) => throw new NotSupportedException();
    }

    /// <summary>
    /// An int-keyed dictionary that lists its values through the interface alone: its only integer indexer is the
    /// dictionary's.
    /// </summary>
    private sealed class IntKeyedValueList : Dictionary<int, int>, IList<int>
    {
        int IList<int>.this[int index] { get => Values.ElementAt(index); set => throw new NotSupportedException(); }

        bool ICollection<int>.IsReadOnly => true;

        int IList<int>.IndexOf(int item) => throw new NotSupportedException();

        void IList<int>.Insert(int index, int item) => throw new NotSupportedException();

        void IList<int>.RemoveAt(int index) => throw new NotSupportedException();

        void ICollection<int>.Add(int item) => throw new NotSupportedException();

        bool ICollection<int>.Contains(int item) => ContainsValue(item);

        void ICollection<int>.CopyTo(int[] array, int arrayIndex) => Values.CopyTo(array, arrayIndex);

        bool ICollection<int>.Remove(int item) => throw new NotSupportedException();

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => Values.GetEnumerator();
    }

    /// <summary>A long-keyed dictionary that lists its values and declares the int indexer of that list itself.</summary>
    private sealed class LongKeyedIndexedValueList : Dictionary<long, int>, IList<int>
    {
        public int this[int index] { get => Values.ElementAt(index); set => throw new NotSupportedException(); }

        bool ICollection<int>.IsReadOnly => true;

        public int IndexOf(int item) => throw new NotSupportedException();

        public void Insert(int index, int item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        void ICollection<int>.Add(int item) => throw new NotSupportedException();

        bool ICollection<int>.Contains(int item) => ContainsValue(item);

        void ICollection<int>.CopyTo(int[] array, int arrayIndex) => Values.CopyTo(array, arrayIndex);

        bool ICollection<int>.Remove(int item) => throw new NotSupportedException();

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => Values.GetEnumerator();
    }

    private static void AssertInformation(Type type, bool isArrayLike, bool iterable, bool isIntegerIndexedArray, bool shouldHaveLength)
    {
        var descriptor = TypeDescriptor.Get(type);
        descriptor.IsArrayLike.Should().Be(isArrayLike);
        descriptor.Iterable.Should().Be(iterable);
        descriptor.IsIntegerIndexed.Should().Be(isIntegerIndexedArray);
        if (shouldHaveLength)
        {
            descriptor.LengthProperty.Should().NotBeNull();
        }
    }
}
