namespace Jint.HtmlParser;

/// <summary>Observes nodes and attributes about to change their node document.</summary>
/// <remarks>
/// Called while the item still reports its old owner document and before any other adoption step,
/// in the middle of a native mutation: an implementation must not mutate or read beyond the item.
/// https://dom.spec.whatwg.org/#concept-node-adopt
/// </remarks>
internal interface INodeAdoptionObserver
{
    void Adopting(Node node);

    void Adopting(Attr attribute);
}
