using System.Collections.Concurrent;

namespace Jint.Browser.Dom;

/// <summary>
/// From an AngleSharp runtime type to the WebIDL interface whose prototype its instances get.
/// </summary>
/// <remarks>
/// <para>
/// It maps by <b>interface</b> rather than by class, because AngleSharp's concrete element classes are almost
/// all <c>internal</c>: <c>HtmlDivElement</c> is not a type this assembly can name, and even if it were, a
/// class-keyed table would have to be regenerated for every element AngleSharp adds. What is public and
/// stable is the interface set, and the most derived interface a runtime type implements <em>is</em> its DOM
/// interface — which is also what makes an element AngleSharp created for an unknown tag land on
/// <c>HTMLUnknownElement</c> without anything having to say so.
/// </para>
/// <para>
/// The generated candidate list is ordered most-derived first, so the first match is the most specific one.
/// The walk is linear in the number of interfaces, and it runs once per runtime type: the answer is cached in
/// a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed on the CLR type, which is process-wide because the
/// mapping is — a definition is process-shared, and only what it produces is per engine.
/// </para>
/// </remarks>
internal static partial class DomTypeMap
{
    private static readonly ConcurrentDictionary<Type, DomInterfaceDefinition?> _cache = new();

    /// <summary>
    /// The interface <paramref name="type"/>'s instances are wrapped as, or <see langword="null"/> when the
    /// type implements no generated interface at all.
    /// </summary>
    internal static DomInterfaceDefinition? For(Type type) => _cache.GetOrAdd(type, static t =>
    {
        foreach (var candidate in _candidates)
        {
            if (candidate.ClrInterface.IsAssignableFrom(t))
            {
                return candidate;
            }
        }

        return null;
    });
}
