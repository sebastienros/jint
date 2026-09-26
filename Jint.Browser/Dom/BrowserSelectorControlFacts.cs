using Jint.Browser.Events;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Browser.Dom;

/// <summary>Demand-only, invocation-owned HTML control facts for the native selector matcher.</summary>
internal sealed class BrowserSelectorControlFacts : ISelectorControlFacts
{
    internal static ISelectorControlFactsFactory Factory { get; } = new ControlFactsFactory();

    // DOM/CSS callers invoke this before capturing any work, environment or query seed. An untouched
    // engine pays only a weak-table lookup; reconciliation uses the operation's existing budget.
    internal static void PrepareControlFactsRead(DomRealm realm, Action<int>? checkpoint, CancellationToken token)
        => Files.FileTransferRealm.IfCreated(realm.Engine)?.PrepareControlFactsRead(checkpoint, token);

    private readonly DomRealm _realm;
    private readonly Document _document;
    private readonly ulong _stamp;
    private readonly ulong _revision;
    private readonly Action<int> _nativeCheckpoint;
    private readonly Func<Action<int>?> _beginNativeRead;
    private readonly Action<int> _chargeBrowserWork;
    private SelectorMatchWork _work;
    private Action<int>? _nativeAdapter;
    private DomReadWork? _browserWork;
    private Dictionary<Element, CachedFacts>? _facts;
    private Dictionary<Node, Dictionary<Element, Element>>? _defaultButtons;
    private bool _reading;

    private BrowserSelectorControlFacts(DomRealm realm, Document document, ulong revision)
    {
        _realm = realm;
        _document = document;
        _stamp = document.MutationStamp;
        _revision = revision;
        _nativeCheckpoint = NativeCheckpoint;
        _beginNativeRead = BeginNativeRead;
        _chargeBrowserWork = ChargeBrowserWork;
    }

    public SelectorControlFacts Read(Element element, SelectorControlFactMask requested, ref SelectorMatchWork work)
    {
        if (_reading) throw new InvalidOperationException(SelectorMatchWork.AlreadyActive);
        Verify(element);
        work.VerifyRead();
        if (requested == SelectorControlFactMask.None) return default;
        // Begin promotes work to its shared cell before the producer keeps a value copy. All subsequent
        // helper deltas charge that same cell, including the caller's polling remainder and observations.
        work.BeginControlProducerRead();
        _work = work;
        _reading = true;
        try
        {
            _browserWork ??= new DomReadWork(_chargeBrowserWork, work.Token);
            _browserWork.Check();
            _facts ??= new Dictionary<Element, CachedFacts>();
            _facts.TryGetValue(element, out var cached);
            var missing = requested & ~cached.Mask;
            var facts = cached.Facts;
            if ((missing & SelectorControlFactMask.DefaultSubmit) != 0)
                facts = facts with { DefaultSubmit = DefaultSubmit(element) };
            if ((missing & SelectorControlFactMask.PlaceholderShown) != 0)
                facts = facts with { PlaceholderShown = PlaceholderShown(element) };
            if ((missing & SelectorControlFactMask.ReadWrite) != 0)
                facts = facts with { ReadWrite = ReadWrite(element) };
            if ((missing & SelectorControlFactMask.Validity) != 0)
                facts = facts with { Validity = Validity(element) };
            if ((missing & SelectorControlFactMask.Range) != 0)
                facts = facts with { Range = Range(element) };
            _browserWork.Check();
            Verify(element);
            _work.VerifyRead();
            // No partial family/cache entry is published when traversal, a callback or validation fails.
            if (missing != SelectorControlFactMask.None)
                _facts[element] = new CachedFacts(cached.Mask | missing, facts);
            Verify(element);
            return facts;
        }
        finally
        {
            _reading = false;
        }
    }

    private void Verify(Element? element = null)
    {
        if (_stamp == ulong.MaxValue || _revision == ulong.MaxValue || _document.MutationStamp != _stamp
            || BrowserSelectorSemanticRevision.Read(_document) != _revision
            || element is not null && !ReferenceEquals(element.OwnerDocument, _document))
            throw new InvalidOperationException(SelectorMatchWork.Invalidated);
    }

    private Action<int> BeginNativeRead()
    {
        Verify();
        _nativeAdapter = _work.BeginControlProducerRead();
        return _nativeCheckpoint;
    }

    private void NativeCheckpoint(int units)
    {
        Verify();
        _work.VerifyRead();
        _nativeAdapter!(units);
        _work.VerifyRead();
        Verify();
    }

    // DomReadWork reports deltas rather than a native helper's cumulative cursor. Each flush starts
    // a fresh bridge cursor; short reads still accumulate on the shared selector invocation counter.
    private void ChargeBrowserWork(int units)
    {
        BeginNativeRead();
        NativeCheckpoint(units);
    }

    private bool DefaultSubmit(Element element)
    {
        var browser = _browserWork!;
        if (!BrowserFormDefaults.IsSubmitButton(element, browser)) return false;
        browser.Step();
        if (HtmlFormState.GetOwner(element) is not { } form) return false;
        var root = browser.Root(element);
        _defaultButtons ??= new Dictionary<Node, Dictionary<Element, Element>>();
        if (!_defaultButtons.TryGetValue(root, out var index))
        {
            var prepared = new Dictionary<Element, Element>();
            foreach (var candidate in BrowserFormDefaults.InclusiveElements(root, browser))
            {
                Verify(candidate);
                if (!BrowserFormDefaults.IsSubmitButton(candidate, browser)) continue;
                browser.Step();
                if (HtmlFormState.GetOwner(candidate) is { } owner) prepared.TryAdd(owner, candidate);
            }
            browser.Check();
            Verify();
            _defaultButtons.Add(root, prepared);
            index = prepared;
        }
        browser.Step();
        return index.TryGetValue(form, out var first) && ReferenceEquals(first, element);
    }

    // https://html.spec.whatwg.org/multipage/semantics-other.html#selector-placeholder-shown
    private bool PlaceholderShown(Element element)
    {
        var browser = _browserWork!;
        if (element.NamespaceUri != Namespaces.Html || element.LocalName is not ("input" or "textarea")) return false;
        if (element.LocalName == "input"
            && !HtmlInputTypes.Info(HtmlInputTypes.Parse(browser.Attribute(element, "type"))).PlaceholderApplies) return false;
        if (browser.Attribute(element, "placeholder") is not { Length: > 0 }) return false;
        if (element.LocalName == "textarea")
            return element.GetHtmlState()!.TextArea!.GetValue(BeginNativeRead(), browser.Token).Length == 0;
        var state = element.GetHtmlState()!.GetInputValueState(BeginNativeRead(), browser.Token)!;
        // An incomplete Number editing buffer (for example "1e") is presented even though API value is empty.
        return state.GetEditingValue(BeginNativeRead(), browser.Token).Length == 0;
    }

    // https://html.spec.whatwg.org/multipage/semantics-other.html#selector-read-write
    private bool ReadWrite(Element element)
    {
        var browser = _browserWork!;
        if (element is { NamespaceUri: Namespaces.Html, LocalName: "input" or "textarea" })
        {
            if (element.LocalName == "input"
                && !HtmlInputTypes.Info(HtmlInputTypes.Parse(browser.Attribute(element, "type"))).ReadOnlyApplies) return false;
            if (browser.Attribute(element, "readonly") is not null) return false;
            return HtmlDisabledness.GetState(element, BeginNativeRead(), browser.Token) != HtmlDisabledState.Disabled;
        }
        return BrowserHtmlSemantics.IsContentEditable(element, browser);
    }

    // https://html.spec.whatwg.org/multipage/semantics-other.html#selector-valid
    private SelectorControlValidity Validity(Element element)
    {
        var browser = _browserWork!;
        if (element is { NamespaceUri: Namespaces.Html, LocalName: "form" or "fieldset" })
        {
            var form = element.LocalName == "form";
            var root = form ? browser.Root(element) : element;
            foreach (var candidate in BrowserFormDefaults.InclusiveElements(root, browser))
            {
                Verify(candidate);
                browser.Step();
                if (form && !ReferenceEquals(HtmlFormState.GetOwner(candidate), element)) continue;
                if (!BrowserControlValidation.WillValidate(_realm, candidate, browser, _beginNativeRead)) continue;
                if (!BrowserControlValidation.Read(_realm, candidate, browser, _beginNativeRead).IsValid)
                    return SelectorControlValidity.Invalid;
            }
            return SelectorControlValidity.Valid;
        }
        if (!BrowserControlValidation.WillValidate(_realm, element, browser, _beginNativeRead))
            return SelectorControlValidity.NotApplicable;
        return BrowserControlValidation.Read(_realm, element, browser, _beginNativeRead).IsValid
            ? SelectorControlValidity.Valid : SelectorControlValidity.Invalid;
    }

    // https://html.spec.whatwg.org/multipage/semantics-other.html#selector-in-range
    private SelectorControlRange Range(Element element)
    {
        var browser = _browserWork!;
        if (element is not { NamespaceUri: Namespaces.Html, LocalName: "input" }) return SelectorControlRange.NotApplicable;
        var type = HtmlInputTypes.Parse(browser.Attribute(element, "type"));
        if (type is not (HtmlInputType.Number or HtmlInputType.Range or HtmlInputType.Date or HtmlInputType.Month
            or HtmlInputType.Week or HtmlInputType.Time or HtmlInputType.DateTimeLocal)) return SelectorControlRange.NotApplicable;
        if (!BrowserControlValidation.WillValidate(_realm, element, browser, _beginNativeRead))
            return SelectorControlRange.NotApplicable;
        var state = element.GetHtmlState()!.GetInputValueState(BeginNativeRead(), browser.Token)!;
        var facts = state.GetNumericFacts(BeginNativeRead(), browser.Token);
        if (!facts.Applies || !facts.HasMinimum && !facts.HasMaximum) return SelectorControlRange.NotApplicable;
        // Native facts preserve empty values, default range bounds and reversed-time intervals. A step
        // mismatch affects validity, but does not make an otherwise bounded value out of range.
        return facts.Underflow || facts.Overflow ? SelectorControlRange.OutOfRange : SelectorControlRange.InRange;
    }

    private readonly record struct CachedFacts(SelectorControlFactMask Mask, SelectorControlFacts Facts);

    private sealed class ControlFactsFactory : ISelectorControlFactsFactory
    {
        public ulong ReadRevision(object context, Document document) => BrowserSelectorSemanticRevision.Read(document);

        public ISelectorControlFacts Create(object context, Document document, ulong capturedRevision)
            => new BrowserSelectorControlFacts((DomRealm) context, document, capturedRevision);
    }
}
