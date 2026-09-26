namespace Jint.HtmlParser.Css.Selectors;

// Requested families, not a request to materialize an enhanced control view.
[Flags]
internal enum SelectorControlFactMask
{
    None = 0,
    DefaultSubmit = 1,
    PlaceholderShown = 2,
    ReadWrite = 4,
    Validity = 8,
    Range = 16
}

// Separate validity/range answers preserve inapplicability instead of taking complements.
// DefaultSubmit is only the host-owned submit-button arm of :default.
internal readonly record struct SelectorControlFacts(
    bool DefaultSubmit = false,
    bool PlaceholderShown = false,
    bool ReadWrite = false,
    bool Valid = false,
    bool Invalid = false,
    bool InRange = false,
    bool OutOfRange = false);

// Invocation-local host facts. Implementations charge traversal/helper work to the supplied read,
// observe additional documents before reading them, and never publish work/adapters onto nodes or programs.
internal interface ISelectorControlFacts
{
    SelectorControlFacts Read(Element element, SelectorControlFactMask requested, ref SelectorMatchWork work);
}

// Singleton factory identity + an opaque owning-host context form the environment's value seed.
// Revision reads are constant-time/allocation-free. Create allocates the invocation-local producer,
// but performs no traversal before Read supplies work. Saturated revisions cannot be reused.
internal interface ISelectorControlFactsFactory
{
    ulong ReadRevision(object context, Document document);
    ISelectorControlFacts Create(object context, Document document, ulong capturedRevision);
}
