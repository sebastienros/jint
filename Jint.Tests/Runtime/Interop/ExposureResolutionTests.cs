#nullable enable

using System.Collections;
using System.Collections.ObjectModel;
using Jint.Runtime.Interop;
using Newtonsoft.Json.Linq;

namespace Jint.Tests.Runtime.Interop;

/// <summary>
/// Which declared types may hand a list target's exposure to its runtime type (#4524). The answer is resolved once
/// per declared type, and a type that may not defer is what keeps an ordinary crossing at one flag test, so the set
/// is pinned here rather than left to be inferred from behaviour.
/// </summary>
public class ExposureResolutionTests
{
    [TestCase(typeof(JToken), true)]
    [TestCase(typeof(JContainer), true)]
    [TestCase(typeof(object), true)]
    [TestCase(typeof(IEnumerable<JToken>), true)]
    [TestCase(typeof(IDictionary<string, JToken>), true)]
    [TestCase(typeof(Collection<int>), true)]
    [TestCase(typeof(JArray), true)]
    // an explicit list contract names its view, and a type addressed by key alone has none to hand over
    [TestCase(typeof(IList<int>), false)]
    [TestCase(typeof(IReadOnlyList<int>), false)]
    [TestCase(typeof(IList), false)]
    [TestCase(typeof(JObject), false)]
    // a sealed type, an array and a value type have no other runtime type
    [TestCase(typeof(string), false)]
    [TestCase(typeof(int[]), false)]
    [TestCase(typeof(int), false)]
    public void RuntimeTypeMayDecide(Type declared, bool expected)
    {
        ObjectWrapper.ResolveExposure(declared).RuntimeTypeMayDecide.Should().Be(expected);
    }

    /// <summary>
    /// A type addressed by key alone never defers: it has no list view to hand over.
    /// </summary>
    [Test]
    public void ATypeAddressedByKeyAloneNeverDefers()
    {
        TypeDescriptor.Get(typeof(KeyedEntryList)).IsAddressedByKeyOnly.Should().BeTrue();
        ObjectWrapper.ResolveExposure(typeof(KeyedEntryList)).RuntimeTypeMayDecide.Should().BeFalse();
    }

    [TestCase(typeof(JToken), typeof(JObject), "Runtime")]
    [TestCase(typeof(JToken), typeof(JArray), "Runtime")]
    [TestCase(typeof(JContainer), typeof(JObject), "Runtime")]
    [TestCase(typeof(IEnumerable<JToken>), typeof(JObject), "Runtime")]
    [TestCase(typeof(IDictionary<string, JToken>), typeof(JObject), "DeclaredWithoutListView")]
    // a list by the generic contract alone, addressed by key, is the object it is as itself
    [TestCase(typeof(ICollection<KeyValuePair<string, int>>), typeof(KeyedEntryList), "Runtime")]
    [TestCase(typeof(IReadOnlyCollection<KeyValuePair<string, int>>), typeof(KeyedEntryList), "Runtime")]
    // a list that is not addressed by key keeps the exposure a non-list interface gave it
    [TestCase(typeof(IEnumerable<JToken>), typeof(JArray), "Declared")]
    [TestCase(typeof(IEnumerable<int>), typeof(List<int>), "Declared")]
    // and so does a target that is no list at all
    [TestCase(typeof(ICollection<KeyValuePair<string, int>>), typeof(Dictionary<string, int>), "Declared")]
    [TestCase(typeof(ICollection<int>), typeof(HashSet<int>), "Declared")]
    // and a type the target does not implement keeps its answer
    [TestCase(typeof(Collection<int>), typeof(JArray), "Declared")]
    public void Decide(Type declared, Type runtime, string expected)
    {
        // by name, because the enum is internal and a public test method cannot take it
        var expectedExposure = (DeclaredTypeExposure) Enum.Parse(typeof(DeclaredTypeExposure), expected);
        var resolution = ObjectWrapper.ResolveExposure(declared);

        resolution.Decide(runtime, TypeDescriptor.Get(runtime)).Should().Be(expectedExposure);
        resolution.TryGetMemoized(runtime, out var memoized).Should().BeTrue();
        memoized.Should().Be(expectedExposure);
    }

    /// <summary>
    /// Which declared types ask about a target that is no <see cref="IList"/>, which may be a list by the generic
    /// contract alone (<see cref="KeyedEntryList"/>). Only a collection interface naming a count-only view: an
    /// enumerable has no view to take, a dictionary keeps its own, and a class or a list contract never asks, so an
    /// ordinary crossing under any of those still pays one type test.
    /// </summary>
    [TestCase(typeof(ICollection<KeyValuePair<string, int>>), true)]
    [TestCase(typeof(IReadOnlyCollection<KeyValuePair<string, int>>), true)]
    [TestCase(typeof(ICollection<int>), true)]
    [TestCase(typeof(IEnumerable<KeyValuePair<string, int>>), false)]
    [TestCase(typeof(IEnumerable<int>), false)]
    [TestCase(typeof(IEnumerable<JToken>), false)]
    [TestCase(typeof(IDictionary<string, int>), false)]
    [TestCase(typeof(IReadOnlyDictionary<string, object>), false)]
    [TestCase(typeof(IList<int>), false)]
    [TestCase(typeof(JToken), false)]
    [TestCase(typeof(Collection<int>), false)]
    [TestCase(typeof(object), false)]
    [TestCase(typeof(string), false)]
    public void NonListTargetsMayDefer(Type declared, bool expected)
    {
        ObjectWrapper.ResolveExposure(declared).NonListTargetsMayDefer.Should().Be(expected);
    }

    /// <summary>
    /// A declared <see cref="object"/> names nothing about its target, so the target is wrapped as its runtime type,
    /// view and all, as <see cref="ObjectWrapper.ClrType"/> already reported.
    /// </summary>
    [Test]
    public void ADeclaredObjectIsWrappedAsTheRuntimeType()
    {
        var engine = new Engine();
        foreach (var target in new object[] { new List<int> { 1 }, JArray.Parse("[1]"), JObject.Parse("{}"), new HashSet<int>(), new KeyedEntryList() })
        {
            var asObject = ObjectWrapper.Create(engine, target, typeof(object));
            var asItself = ObjectWrapper.Create(engine, target, target.GetType());

            asObject.GetType().Should().Be(asItself.GetType(), "{0} typed object", target.GetType());
            ((ObjectWrapper) asObject).ClrType.Should().Be(target.GetType());
        }
    }

    private sealed class KeyedEntryList : Dictionary<string, int>, IList<KeyValuePair<string, int>>
    {
        KeyValuePair<string, int> IList<KeyValuePair<string, int>>.this[int index]
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        int IList<KeyValuePair<string, int>>.IndexOf(KeyValuePair<string, int> item) => throw new NotSupportedException();

        void IList<KeyValuePair<string, int>>.Insert(int index, KeyValuePair<string, int> item) => throw new NotSupportedException();

        void IList<KeyValuePair<string, int>>.RemoveAt(int index) => throw new NotSupportedException();
    }
}