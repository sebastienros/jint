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

internal enum SelectorControlValidity { NotApplicable, Valid, Invalid }
internal enum SelectorControlRange { NotApplicable, InRange, OutOfRange }

// Three-state results preserve applicability and cannot represent contradictory predicate answers.
// DefaultSubmit is only the host-owned submit-button arm of :default.
internal readonly record struct SelectorControlFacts(
    bool DefaultSubmit = false,
    bool PlaceholderShown = false,
    bool ReadWrite = false,
    SelectorControlValidity Validity = SelectorControlValidity.NotApplicable,
    SelectorControlRange Range = SelectorControlRange.NotApplicable);

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
