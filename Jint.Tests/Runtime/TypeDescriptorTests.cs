using System.Collections;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class TypeDescriptorTests
{
    [Fact]
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
