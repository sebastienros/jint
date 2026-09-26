using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;
using Jint.Runtime;

namespace Jint.Browser.Runtime.Parsing;

internal sealed partial class ParserDriver
{
    private static readonly ConditionalWeakTable<Document, ParserDriver> ActiveParsers = new();
    private readonly Dictionary<Document, NativeParse> _nativeParses = new();
    private readonly HashSet<Document> _xmlParsingDocuments = [];

    private sealed class NativeParse(Document document, HtmlParserSession session, MutationSubscription mutations)
    {
        internal Document Document { get; } = document;
        internal HtmlParserSession Session { get; } = session;
        internal MutationSubscription Mutations { get; } = mutations;
        internal HtmlScriptFrame? Frame { get; set; }
        internal readonly List<(Element Element, FetchedBody? Source)> Deferred = [];
        internal readonly Dictionary<Element, FetchedBody?> PendingScripts = new();
    }

    internal static bool IsParsing(Document document) => ActiveParsers.TryGetValue(document, out _);
    internal static bool HasInsertionPoint(Document document)
        => ActiveParsers.TryGetValue(document, out var driver) && driver._nativeParses.TryGetValue(document, out var parse)
            && parse.Frame is not null;

    internal static void Write(Document document, string text)
    {
        if (!ActiveParsers.TryGetValue(document, out var driver) ||
            !driver._nativeParses.TryGetValue(document, out var parse) || parse.Frame is not { } frame)
            throw new InvalidOperationException("The document has no active parser insertion point.");
        driver._runtime.Engine.Constraints.Check();
        parse.Session.InsertInput(frame, text, driver._cancellationToken);
        driver._runtime.Engine.Constraints.Check();
        if (!parse.Session.ParserPaused) driver.Drive(parse, frame);
    }

    private void Parse(Document document, string markup, bool isSrcdoc = false)
    {
        _runtime.Engine.Constraints.Check();
        if (document.Kind == DocumentKind.Xml)
        {
            _xmlParsingDocuments.Add(document);
            try
            {
                XmlDocumentParser.Parse(markup, document, null, _cancellationToken);
                _runtime.Engine.Constraints.Check();
                WatchDocument(document);
                ProcessResources(document);
            }
            finally { _xmlParsingDocuments.Remove(document); }
            return;
        }
        var session = new HtmlParserSession(document,
            new HtmlParseOptions { ScriptingEnabled = _runtime.ScriptingEnabled },
            new HtmlDocumentContext(IsSrcdoc: isSrcdoc, AllowDeclarativeShadowRoots: true,
                ShadowHostContextProvider: new BrowserShadowHostContextProvider(_runtime, RecordCandidateShadowHost)), enableScriptRequests: true);
        var mutations = WatchDocument(document).Subscription;
        var parse = new NativeParse(document, session, mutations);
        _nativeParses.Add(document, parse);
        ActiveParsers.Add(document, this);
        var wasTokenizing = _tokenizing;
        _tokenizing = true;
        try
        {
            session.AppendInput(markup, isFinal: true);
            Drive(parse, null);
            if (IsFrameDocument(document)) SetFrameReadyState(FrameWindows.DocumentRealm(_runtime, document), "interactive");
            else SetReadyState("interactive");
            foreach (var entry in parse.Deferred)
            {
                _runtime.Engine.Constraints.Check();
                RunNativeClassic(entry.Element, entry.Source);
            }
        }
        catch
        {
            // Abort is cooperative and must finish even when the document's token was cancelled.
            while (session.Abort().Kind == HtmlParseStepKind.Yielded) { }
            throw;
        }
        finally
        {
            _tokenizing = wasTokenizing;
            ActiveParsers.Remove(document);
            _nativeParses.Remove(document);
            var watch = WatchDocument(document);
            if (watch.Deferred) { watch.Deferred = false; QueueResourceDrain(watch); }
        }
    }

    private void Drive(NativeParse parse, HtmlScriptFrame? insertion)
    {
        while (true)
        {
            _runtime.Engine.Constraints.Check();
            var step = insertion is null
                ? parse.Session.Drive(4096, _cancellationToken)
                : parse.Session.DriveInsertedInput(insertion, 4096, _cancellationToken);
            _runtime.Engine.Constraints.Check();
            List<Element>? completedStyles = null;
            while (parse.Session.TryTakeCompletedStyle(out var completed, _cancellationToken))
            {
                _runtime.Engine.Constraints.Check();
                (completedStyles ??= []).Add(completed!);
                _pendingStyleCompletions.Add(completed!);
            }
            try
            {
                DiscoverCandidateShadowRoots();
                ProcessResourceRecords(parse);
                if (completedStyles is not null)
                {
                    foreach (var completed in completedStyles)
                    {
                        _runtime.Engine.Constraints.Check();
                        _pendingStyleCompletions.Remove(completed);
                        NativeCssStyleSheets.DisassociateOwner(_runtime.Dom.RealmOfDocument(parse.Document), parse.Document, completed);
                        _inlineStyles.Add(completed);
                        InstallInlineStyle(completed);
                    }
                }
            }
            finally
            {
                if (completedStyles is not null)
                    foreach (var completed in completedStyles) _pendingStyleCompletions.Remove(completed);
            }
            if (step.Kind is HtmlParseStepKind.HostRequest or HtmlParseStepKind.Complete) InstallInlineStyles(parse.Document);
            switch (step.Kind)
            {
                case HtmlParseStepKind.Yielded:
                    continue;
                case HtmlParseStepKind.Complete:
                    return;
                case HtmlParseStepKind.InsertionBoundary or HtmlParseStepKind.PendingBlocker or HtmlParseStepKind.ParserPaused
                    when insertion is not null:
                    return;
                case HtmlParseStepKind.HostRequest:
                    HandleRequest(parse, step.HostRequest!);
                    continue;
                default:
                    throw new InvalidOperationException("The native page parser returned " + step.Kind + ".");
            }
        }
    }

    private void HandleRequest(NativeParse parse, HtmlHostRequest request)
    {
        var outcome = HtmlHostRequestOutcome.Finished;
        var previous = parse.Frame;
        parse.Frame = request.Frame ?? previous;
        try
        {
            switch (request.Kind)
            {
                case HtmlHostRequestKind.MicrotaskCheckpoint:
                    _runtime.Engine.CleanUpAfterRunningScript();
                    break;
                case HtmlHostRequestKind.PrepareScript:
                    outcome = PrepareNativeScript(parse, request.Script);
                    break;
                case HtmlHostRequestKind.WaitForPendingScript:
                    parse.PendingScripts[request.Script] = FetchScriptSource(request.Script, mayPump: !_runtime.Engine.IsEvaluationInProgress);
                    break;
                case HtmlHostRequestKind.ExecutePendingScript:
                    var source = parse.PendingScripts[request.Script];
                    parse.PendingScripts.Remove(request.Script);
                    if (source is not null) RunNativeClassic(request.Script, source);
                    break;
                case HtmlHostRequestKind.ProcessSvgScript:
                    RunSvgScript(request.Script);
                    break;
            }
        }
        finally
        {
            parse.Frame = previous;
        }
        parse.Session.CompleteHostRequest(request.Id, outcome, _cancellationToken);
        _runtime.Engine.Constraints.Check();
    }

    // HTML §4.12.1.1: native script flags are authoritative across moves and clones.
    private HtmlHostRequestOutcome PrepareNativeScript(NativeParse parse, Element script)
    {
        var flags = script.GetHtmlState()!.Script!;
        if (flags.AlreadyStarted) return HtmlHostRequestOutcome.Finished;
        var parserDocument = flags.ParserDocument;
        flags.ParserDocument = null;
        if (parserDocument is not null && Attribute(script, "async") is null) flags.ForceAsync = true;
        var src = Attribute(script, "src");
        var text = ScriptTextOf(script);
        var type = ScriptType(script);
        if ((src is null && text.Length == 0) || !ShadowTree.IsConnected(script, _cancellationToken) || type == NativeScriptType.Data)
            return HtmlHostRequestOutcome.Finished;
        flags.ParserDocument = parserDocument;
        if (parserDocument is not null) flags.ForceAsync = false;
        flags.AlreadyStarted = true;
        flags.PreparationTimeDocument = script.OwnerDocument;
        if (parserDocument is not null && !ReferenceEquals(parserDocument, script.OwnerDocument) ||
            !_runtime.ScriptingEnabled || IsFrameDocument(script.OwnerDocument!) && !CanRunFrame(script.OwnerDocument!))
            return HtmlHostRequestOutcome.Finished;
        if (type == NativeScriptType.ImportMap)
        {
            if (IsFrameDocument(script.OwnerDocument!)) return HtmlHostRequestOutcome.Finished;
            ReadImportMapEarly(script.OwnerDocument!);
            return HtmlHostRequestOutcome.Finished;
        }
        if (type == NativeScriptType.Module || Attribute(script, "nomodule") is not null)
            return HtmlHostRequestOutcome.Finished;
        if (src is not null)
        {
            if (src.Length == 0)
            {
                FireAt(script, "error");
                return HtmlHostRequestOutcome.Finished;
            }
            if (Attribute(script, "defer") is not null || Attribute(script, "async") is not null)
            {
                // Browser's existing sequential-fetch scheduling: both queues run after parsing.
                var fetched = FetchScriptSource(script, mayPump: !_runtime.Engine.IsEvaluationInProgress);
                if (fetched is not null) parse.Deferred.Add((script, fetched));
                return HtmlHostRequestOutcome.Finished;
            }
            // A nested write returns to its caller; the native session waits only at nesting level zero.
            return HtmlHostRequestOutcome.PendingParsingBlockingScript;
        }
        RunNativeClassic(script, null);
        return HtmlHostRequestOutcome.Finished;
    }

    private FetchedBody? FetchScriptSource(Element script, bool mayPump)
    {
        var source = Attribute(script, "src") ?? "";
        var resolved = PageUrl.Resolve(source, BaseUrlOf(script.OwnerDocument!));
        if (resolved is null)
        {
            FailSubresource(script, source, "The script source is not a URL a page can load.");
            return null;
        }
        return FetchBytes(resolved, script, "script", PageRequestKind.Script, mayPump);
    }

    private void RunNativeClassic(Element script, FetchedBody? source)
    {
        if (script.GetHtmlState()?.Script is { } flags &&
            !ReferenceEquals(flags.PreparationTimeDocument, script.OwnerDocument)) return;
        var document = script.OwnerDocument!;
        _runtime.CustomElementsIfCreated?.UpgradeParsedElements();
        if (!IsFrameDocument(document)) ReadImportMapEarly(document);
        if (IsFrameDocument(document))
        {
            if (!CanRunFrame(document)) return;
            FrameWindows.ForDocument(_runtime, document);
            using var scope = new RealmScope(_runtime.Engine, FrameWindows.DocumentRealm(_runtime, document).OwningRealm);
            Execute(source, script);
        }
        else Execute(source, script);
    }

    private void RunSvgScript(Element script)
    {
        if (!_runtime.ScriptingEnabled || !ShadowTree.IsConnected(script, _cancellationToken)) return;
        var source = Attribute(script, "href") ?? script.GetAttributeNS("http://www.w3.org/1999/xlink", "href");
        if (source is null) RunNativeClassic(script, null);
        else if (PageUrl.Resolve(source, BaseUrlOf(script.OwnerDocument!)) is { } url &&
            FetchBytes(url, script, "script", PageRequestKind.Script, mayPump: false) is { } fetched)
            RunNativeClassic(script, fetched);
    }

    private enum NativeScriptType { Classic, Module, ImportMap, Data }

    private NativeScriptType ScriptType(Element script)
    {
        var type = Attribute(script, "type");
        if (type is null)
        {
            var language = Attribute(script, "language");
            type = string.IsNullOrEmpty(language) ? "" : "text/" + language;
        }
        type = type.Trim(' ', '\t', '\r', '\n', '\f');
        if (type.Length == 0 || JavaScriptMime.IsJavaScript(type)) return NativeScriptType.Classic;
        if (type.Equals("module", StringComparison.OrdinalIgnoreCase)) return NativeScriptType.Module;
        if (type.Equals("importmap", StringComparison.OrdinalIgnoreCase)) return NativeScriptType.ImportMap;
        return NativeScriptType.Data;
    }

    private string ScriptTextOf(Element element)
        => DomDescendantText.ReadChildren(element, _runtime.Dom.NativeReadCheckpoint, _cancellationToken);

    private string TextOf(Element element)
        => DomDescendantText.Read(element, _runtime.Dom.NativeReadCheckpoint, _cancellationToken);

    private string? Attribute(Element element, string name)
    {
        var work = new DomReadWork(_ => _runtime.Engine.Constraints.Check(), _cancellationToken);
        work.Check();
        var value = work.Attribute(element, name);
        work.Check();
        return value;
    }

    private static bool IsHtml(Element element, string name)
        => element.NamespaceUri == Namespaces.Html && element.LocalName == name;

    private IEnumerable<Element> NativeElements(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        var count = 0;
        while (stack.TryPop(out var node))
        {
            if ((++count & 255) == 0) _runtime.Engine.Constraints.Check();
            if (node is Element element) yield return element;
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                if ((++count & 255) == 0) _runtime.Engine.Constraints.Check();
                stack.Push(child);
            }
        }
        _runtime.Engine.Constraints.Check();
    }
}
