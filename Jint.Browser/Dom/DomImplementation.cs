using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>DOM §4.5's implementation object, associated with its actual native document.</summary>
internal sealed class DomImplementation
{
    private static readonly ConditionalWeakTable<Document, DomImplementation> Implementations = new();
    private DomImplementation(Document document) => Document = document;
    internal Document Document { get; }
    internal static DomImplementation Of(Document document)
        => Implementations.GetValue(document, static owner => new DomImplementation(owner));
}
