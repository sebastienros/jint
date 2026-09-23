using System;
using System.Collections.Generic;
using System.Threading;

namespace Jint.HtmlParser.Html;

// HTML Standard §13.2.6.4 (2026-09-22). This partial builder stops at the
// first operation assigned to H5-H7; no unsupported token enters a generic rule.
internal sealed partial class HtmlTreeBuilder
{
    private enum Mode { Initial, BeforeHtml, BeforeHead, InHead, InHeadNoscript, AfterHead, InBody, Text, AfterBody, AfterAfterBody }

    private readonly Document _document;
    private readonly HtmlTokenizer _tokenizer;
    private readonly ParseDiagnosticCollector? _diagnostics;
    private readonly HtmlDocumentContext _context;
    private readonly int _maxDepth;
    private readonly bool _scriptingEnabled;
    private readonly List<Element> _open = [];
    private readonly Dictionary<string, List<int>> _nameIndexes = new(StringComparer.Ordinal);
    private readonly List<int> _specialIndexes = [];
    private readonly List<int> _liStops = [];
    private readonly List<int> _ddDtStops = [];
    private int _unexpectedOpenCount;
    private Element? _head;
    private Element? _form;
    private Mode _mode;
    private Mode _originalTextMode;
    private bool _framesetOk = true;
    private bool _ignoreNextLf;
    private bool _acknowledgedSelfClosing;
    private Node? _headInsertionOverride;
    private int _temporaryHeadDepth;
    private bool _hasToken;
    private HtmlToken _token;
    private int _textIndex;
    private HtmlMissingFeature? _missing;
    private int _pendingPopTarget = -1;
    private bool _reprocessAfterPop;
    private long _work;
    private CancellationToken _cancellationToken;
    private long _remaining;

    internal HtmlTreeBuilder(Document document, HtmlTokenizer tokenizer, int maxDepth, bool scriptingEnabled,
        ParseDiagnosticCollector? diagnostics, HtmlDocumentContext context)
    {
        _document = document;
        _tokenizer = tokenizer;
        _maxDepth = maxDepth;
        _scriptingEnabled = scriptingEnabled;
        _diagnostics = diagnostics;
        _context = context;
    }

    internal bool HasToken => _hasToken;
    internal long WorkCount => _work;

    internal void SetToken(HtmlToken token)
    {
        if (_hasToken) throw new InvalidOperationException("Tree work is still pending.");
        _token = token;
        _hasToken = true;
        _textIndex = 0;
        _acknowledgedSelfClosing = false;
    }

    internal HtmlParseStep Process(long quota, CancellationToken cancellationToken)
    {
        if (!_hasToken) throw new InvalidOperationException("No tree token is pending.");
        _cancellationToken = cancellationToken;
        _remaining = quota;
        cancellationToken.ThrowIfCancellationRequested();
        if (_token.Kind == HtmlTokenKind.Text)
        {
            ProcessCharacters();
            if (_textIndex < _token.Data.Length) return new HtmlParseStep(HtmlParseStepKind.Yielded);
            FinishToken();
            return new HtmlParseStep(HtmlParseStepKind.Yielded);
        }

        _ignoreNextLf = false;

        // Reprocessing changes the insertion mode without asking the tokenizer
        // for another token. The chain is bounded by the finite mode inventory.
        for (var pass = 0; pass < 12; pass++)
        {
            if (_pendingPopTarget >= 0)
            {
                while (_open.Count > _pendingPopTarget && _remaining > 0) Pop();
                if (_open.Count > _pendingPopTarget) return new HtmlParseStep(HtmlParseStepKind.Yielded);
                _pendingPopTarget = -1;
                if (_reprocessAfterPop)
                {
                    _reprocessAfterPop = false;
                    continue;
                }
                var poppedEof = _token.Kind == HtmlTokenKind.EndOfFile;
                FinishToken();
                return new HtmlParseStep(poppedEof ? HtmlParseStepKind.Complete : HtmlParseStepKind.Yielded);
            }
            if (_remaining <= 0) return new HtmlParseStep(HtmlParseStepKind.Yielded);
            Charge(1);
            var reprocess = Dispatch(_mode);
            if (_missing is { } family)
                return new HtmlParseStep(HtmlParseStepKind.MissingFeature, family, _token.Offset);
            if (_pendingPopTarget >= 0) continue;
            if (!reprocess)
            {
                var eof = _token.Kind == HtmlTokenKind.EndOfFile;
                FinishToken();
                return new HtmlParseStep(eof ? HtmlParseStepKind.Complete : HtmlParseStepKind.Yielded);
            }
        }
        throw new InvalidOperationException("Insertion mode did not settle.");
    }

    private bool Dispatch(Mode mode) => mode switch
    {
        Mode.Initial => InInitial(),
        Mode.BeforeHtml => InBeforeHtml(),
        Mode.BeforeHead => InBeforeHead(),
        Mode.InHead => InHead(),
        Mode.InHeadNoscript => InHeadNoscript(),
        Mode.AfterHead => InAfterHead(),
        Mode.InBody => InBody(),
        Mode.Text => InText(),
        Mode.AfterBody => InAfterBody(),
        Mode.AfterAfterBody => InAfterAfterBody(),
        _ => throw new InvalidOperationException("Unknown HTML insertion mode.")
    };

    private void FinishToken()
    {
        if (_token.Kind == HtmlTokenKind.StartTag && _token.SelfClosing && !_acknowledgedSelfClosing)
            Error("unacknowledged-self-closing-flag");
        _hasToken = false;
        _missing = null;
    }

    private void Missing(HtmlMissingFeature family) => _missing = family;
    private void Error(string code) => _diagnostics?.Add("html/tree-" + code, _token.Offset);

    private void Charge(long units)
    {
        if (units <= 0) return;
        _work = _work > long.MaxValue - units ? long.MaxValue : _work + units;
        _remaining -= units;
        _cancellationToken.ThrowIfCancellationRequested();
    }

    private void CheckDepth(int additional = 1)
    {
        if (_maxDepth > 0 && _open.Count + _temporaryHeadDepth + additional > _maxDepth)
            throw new ParseLimitException(ParseLimitKind.NestingDepth, _maxDepth, _open.Count + _temporaryHeadDepth + additional);
    }

    private Element Current => _open.Count != 0 ? _open[^1] : throw new InvalidOperationException("No open element.");
    private Node CurrentParent => _open.Count == 0 ? _document : Current;

    private Element InsertElement(string name, IReadOnlyList<HtmlAttribute>? attributes = null, Node? parentOverride = null)
    {
        CheckDepth();
        var parent = parentOverride ?? _headInsertionOverride ?? CurrentParent;
        var owner = parent as Document ?? parent.OwnerDocument!;
        var element = owner.CreateParsedElement(Namespaces.Html, name, null);
        if (attributes is { Count: > 0 })
        {
            var prepared = PrepareAttributes(attributes);
            element.InitializeParsedAttributes(prepared, _cancellationToken);
        }
        parent.AppendParsedChild(element);
        Push(element);
        return element;
    }

    private Element InsertTokenElement(Node? parentOverride = null) => InsertElement(_token.Name!, _token.Attributes, parentOverride);

    private ParserAttribute[] PrepareAttributes(IReadOnlyList<HtmlAttribute> attributes)
    {
        var result = new ParserAttribute[attributes.Count];
        for (var i = 0; i < result.Length; i++)
        {
            if ((i & 4095) == 0) _cancellationToken.ThrowIfCancellationRequested();
            var item = attributes[i];
            result[i] = new ParserAttribute(null, item.Name, null, item.Value);
            Charge(1L + item.Name.Length + item.Value.Length);
        }
        return result;
    }

    private void MergeAttributes(Element target)
    {
        if (_token.Attributes.Count == 0) return;
        var prepared = PrepareAttributes(_token.Attributes);
        target.AddMissingParsedAttributes(prepared, _cancellationToken);
        Charge(prepared.Length);
    }

    private void Push(Element element)
    {
        var index = _open.Count;
        _open.Add(element);
        if (!_nameIndexes.TryGetValue(element.LocalName, out var indexes))
            _nameIndexes[element.LocalName] = indexes = [];
        indexes.Add(index);
        if (IsSpecial(element.LocalName)) _specialIndexes.Add(index);
        if (IsSpecial(element.LocalName) && element.LocalName is not ("address" or "div" or "p" or "li"))
            _liStops.Add(index);
        if (IsSpecial(element.LocalName) && element.LocalName is not ("address" or "div" or "p" or "dd" or "dt"))
            _ddDtStops.Add(index);
        if (!AllowedOpenAtEof(element.LocalName)) _unexpectedOpenCount++;
        Charge(1);
    }

    private Element Pop()
    {
        var index = _open.Count - 1;
        var element = _open[index];
        _open.RemoveAt(index);
        var indexes = _nameIndexes[element.LocalName];
        indexes.RemoveAt(indexes.Count - 1);
        if (indexes.Count == 0) _nameIndexes.Remove(element.LocalName);
        if (_specialIndexes.Count > 0 && _specialIndexes[^1] == index) _specialIndexes.RemoveAt(_specialIndexes.Count - 1);
        if (_liStops.Count > 0 && _liStops[^1] == index) _liStops.RemoveAt(_liStops.Count - 1);
        if (_ddDtStops.Count > 0 && _ddDtStops[^1] == index) _ddDtStops.RemoveAt(_ddDtStops.Count - 1);
        if (!AllowedOpenAtEof(element.LocalName)) _unexpectedOpenCount--;
        Charge(1);
        return element;
    }

    private void SchedulePopTo(int index, bool reprocess)
    {
        if (index < 0 || index >= _open.Count) throw new InvalidOperationException("Invalid HTML stack pop target.");
        _pendingPopTarget = index;
        _reprocessAfterPop = reprocess;
    }

    private int Last(string name) => _nameIndexes.TryGetValue(name, out var indexes) ? indexes[^1] : -1;
    private int LastSpecial => _specialIndexes.Count == 0 ? -1 : _specialIndexes[^1];
    private int LastLiStop => _liStops.Count == 0 ? -1 : _liStops[^1];
    private int LastDdDtStop => _ddDtStops.Count == 0 ? -1 : _ddDtStops[^1];
    private bool InScope(string name) => Last(name) > Last("html") || name == "html" && Last("html") >= 0;
    private bool InButtonScope(string name) => Last(name) >= 0 && Last(name) > Math.Max(Last("html"), name == "button" ? -1 : Last("button"));
    private bool InListItemScope(string name) => Last(name) >= 0 && Last(name) > Math.Max(Last("html"), Math.Max(Last("ol"), Last("ul")));

    private void GenerateImpliedEndTags(string? except = null)
    {
        while (_open.Count > 0 && Current.LocalName != except && IsImpliedEndTag(Current.LocalName)) Pop();
    }

    private void CloseP(bool reprocess)
    {
        GenerateImpliedEndTags("p");
        if (Current.LocalName != "p") Error("misnested-p-end-tag");
        SchedulePopTo(Last("p"), reprocess);
    }

    private void InsertComment(Node? parent = null)
    {
        parent ??= CurrentParent;
        var owner = parent as Document ?? parent.OwnerDocument!;
        parent.AppendParsedChild(owner.CreateComment(_token.Data));
        Charge(_token.Data.Length + 1L);
    }

    private void InsertProcessingInstruction(Node? parent = null)
    {
        parent ??= CurrentParent;
        var owner = parent as Document ?? parent.OwnerDocument!;
        parent.AppendParsedChild(owner.CreateProcessingInstruction(_token.Name!, _token.Data));
        Charge(_token.Data.Length + (_token.Name?.Length ?? 0) + 1L);
    }

    private void InsertText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return;
        var parent = CurrentParent;
        if (parent.LastChild is Text previous)
            previous.AppendParsedData(text, _cancellationToken);
        else
        {
            var owner = parent as Document ?? parent.OwnerDocument!;
            var node = owner.CreateTextNode(string.Empty);
            node.AppendParsedData(text, _cancellationToken);
            parent.AppendParsedChild(node);
        }
        Charge(text.Length);
    }

    private void EnterText(HtmlTextMode textMode, string name)
    {
        InsertTokenElement();
        _tokenizer.SetTextMode(textMode, name);
        _originalTextMode = _mode;
        _mode = Mode.Text;
    }

    private static bool White(char c) => c is '\t' or '\n' or '\f' or ' ';
    private static bool IsHeading(string name) => name is "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
    private static bool IsImpliedEndTag(string name) => name is "dd" or "dt" or "li" or "optgroup" or "option" or "p" or "rb" or "rp" or "rt" or "rtc";
    private static bool AllowedOpenAtEof(string name) => name is "dd" or "dt" or "li" or "optgroup" or
        "option" or "p" or "rb" or "rp" or "rt" or "rtc" or "tbody" or "td" or "tfoot" or "th" or
        "thead" or "tr" or "body" or "html";

    private static bool IsSpecial(string name) => name is
        "address" or "applet" or "area" or "article" or "aside" or "base" or "basefont" or "bgsound" or
        "blockquote" or "body" or "br" or "button" or "caption" or "center" or "col" or "colgroup" or
        "dd" or "details" or "dir" or "div" or "dl" or "dt" or "embed" or "fieldset" or "figcaption" or
        "figure" or "footer" or "form" or "frame" or "frameset" or "h1" or "h2" or "h3" or "h4" or
        "h5" or "h6" or "head" or "header" or "hgroup" or "hr" or "html" or "iframe" or "img" or
        "input" or "keygen" or "li" or "link" or "listing" or "main" or "marquee" or "menu" or
        "meta" or "nav" or "noembed" or "noframes" or "noscript" or "object" or "ol" or "p" or
        "param" or "plaintext" or "pre" or "script" or "search" or "section" or "select" or "source" or
        "style" or "summary" or "table" or "tbody" or "td" or "template" or "textarea" or "tfoot" or
        "th" or "thead" or "title" or "tr" or "track" or "ul" or "wbr" or "xmp";
}
