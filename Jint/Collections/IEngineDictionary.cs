using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Jint;

/// <summary>
/// Contract for custom dictionaries that Jint uses.
/// </summary>
internal interface IEngineDictionary<in TKey, TValue>
{
    int Count { get; }

    ref TValue this[TKey name] { get; }

    public ref TValue GetValueRefOrNullRef(TKey key);

    public ref TValue GetValueRefOrAddDefault(TKey key, out bool exists);
}
