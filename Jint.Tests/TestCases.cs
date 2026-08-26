#nullable enable

using System.Collections;

namespace Jint.Tests;

/// <summary>
/// A typed, collection-initializable set of argument rows for <see cref="NUnit.Framework.TestCaseSourceAttribute" />.
/// </summary>
/// <remarks>
/// <para>
/// NUnit reads a test-case source as a bare <see cref="IEnumerable" /> of <see cref="object" /> arrays, which
/// says nothing about how many arguments a row is supposed to carry or what type each one is: a row with one
/// value too few is a discovery-time failure in a test nobody was editing. These types are what xUnit's
/// <c>TheoryData&lt;…&gt;</c> was for — the compiler checks each row against the parameter list as it is
/// written — and they exist here for the same reason.
/// </para>
/// <para>
/// Public rather than internal because the sources are <see langword="public" /> members of public test
/// classes, and a public member may not return a less accessible type.
/// </para>
/// </remarks>
public sealed class TestCases<T> : IEnumerable<T>
{
    private readonly List<T> _values = new();

    public TestCases()
    {
    }

    public TestCases(IEnumerable<T> values) => _values.AddRange(values);

    public void Add(T value) => _values.Add(value);

    // A one-argument source yields the values themselves rather than one-element arrays: NUnit reads a
    // non-array item as the single argument, and it is what lets a row list be written as a collection
    // expression, which several of these sources are.
    public IEnumerator<T> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <inheritdoc cref="TestCases{T}" />
public sealed class TestCases<T1, T2> : TestCaseRows
{
    public void Add(T1 first, T2 second) => Row(new object?[] { first, second });
}

/// <inheritdoc cref="TestCases{T}" />
public sealed class TestCases<T1, T2, T3> : TestCaseRows
{
    public void Add(T1 first, T2 second, T3 third) => Row(new object?[] { first, second, third });
}

/// <inheritdoc cref="TestCases{T}" />
public sealed class TestCases<T1, T2, T3, T4> : TestCaseRows
{
    public void Add(T1 first, T2 second, T3 third, T4 fourth) => Row(new object?[] { first, second, third, fourth });
}

/// <summary>
/// The rows themselves, in the shape NUnit reads them.
/// </summary>
public abstract class TestCaseRows : IEnumerable<object?[]>
{
    private readonly List<object?[]> _rows = new();

    private protected void Row(object?[] values) => _rows.Add(values);

    public IEnumerator<object?[]> GetEnumerator() => _rows.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
