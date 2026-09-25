using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace Jint.HtmlParser.Html;

// HTML Standard §13.2.6.4 (2026-09-22). This partial builder stops at the
// first unimplemented later-family operation; no unsupported token enters a generic rule.
internal sealed partial class HtmlTreeBuilder
{
    private enum Mode
    {
        Initial, BeforeHtml, BeforeHead, InHead, InHeadNoscript, AfterHead, InBody, Text,
        InTable, InTableText, InCaption, InColumnGroup, InTableBody, InRow, InCell, InTemplate,
        AfterBody, InFrameset, AfterFrameset, AfterAfterBody, AfterAfterFrameset
    }

    private readonly Document _document;
    private readonly HtmlTokenizer _tokenizer;
    private readonly ParseDiagnosticCollector? _diagnostics;
    private readonly HtmlDocumentContext _context;
    private readonly int _maxDepth;
    private readonly bool _scriptingEnabled;
    private readonly List<Element> _open = [];
    private Dictionary<string, List<int>> _nameIndexes = new(StringComparer.Ordinal);
    private List<int> _specialIndexes = [];
    private List<int> _liStops = [];
    private List<int> _ddDtStops = [];
    private List<int> _scopeStops = [];
    private List<int> _resetModeIndexes = [];
    private int _unexpectedOpenCount;
    private Element? _head;
    private Element? _form;
    private Mode _mode;
    private Mode _originalTextMode;
    private bool _framesetOk = true;
    private int _framesetReplacementStage;
    private Node? _framesetScanNode;
    private bool _framesetScanUnwinding;
    private bool _ignoreNextLf;
    private bool _acknowledgedSelfClosing;
    private bool _fosterParenting;
    private bool _delegateToBody;
    private Mode _delegatedFromMode;
    private bool _tableFosterCharacterErrorReported;
    private Node? _headInsertionOverride;
    private int _temporaryHeadDepth;
    private bool _hasToken;
    private HtmlToken _token;
    private int _textIndex;
    private HtmlMissingFeature? _missing;
    private int _pendingPopTarget = -1;
    private bool _reprocessAfterPop;
    private Mode? _modeAfterPop;
    private bool _clearFormattingAfterPop;
    private bool _resetAfterPop;
    private bool _popTemplateModeAfterPop;
    private int _pendingShiftIndex = -1;
    private int _inputAttributeIndex;
    private bool _inputTypeFound;
    private bool _inputTypeHidden;
    private ParserAttribute[]? _preparedAttributes;
    private int _preparedAttributeIndex;
    private long _preparedAttributeWork;
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
        _inputAttributeIndex = 0;
        _inputTypeFound = false;
        _inputTypeHidden = false;
        _preparedAttributes = null;
        _preparedAttributeIndex = 0;
        _preparedAttributeWork = 0;
        _templateHasFor = false;
        _templateOrdinaryShadowFallback = false;
        ResetFormattingToken();
        _fosterParenting = false;
        _delegateToBody = false;
        _tableFosterCharacterErrorReported = false;
    }

    internal HtmlParseStep Process(long quota, CancellationToken cancellationToken)
    {
        if (!_hasToken) throw new InvalidOperationException("No tree token is pending.");
        _cancellationToken = cancellationToken;
        _remaining = quota;
        cancellationToken.ThrowIfCancellationRequested();
        if (_token.Kind != HtmlTokenKind.Text && _mode == Mode.InTableText)
        {
            if (!FlushTableText()) return new HtmlParseStep(HtmlParseStepKind.Yielded);
            _mode = _tableTextOriginalMode;
            if (_remaining <= 0) return new HtmlParseStep(HtmlParseStepKind.Yielded);
        }
        if (_token.Kind == HtmlTokenKind.Text)
        {
            ProcessCharacters();
            if (_missing is { } textFamily)
                return new HtmlParseStep(HtmlParseStepKind.MissingFeature, textFamily, _token.Offset);
            if (_textIndex < _token.Data.Length) return new HtmlParseStep(HtmlParseStepKind.Yielded);
            FinishToken();
            return new HtmlParseStep(HtmlParseStepKind.Yielded);
        }

        if (_token.Kind == HtmlTokenKind.StartTag && _token.Attributes.Count > 0 && !PrepareTokenAttributes())
            return new HtmlParseStep(HtmlParseStepKind.Yielded);

        _ignoreNextLf = false;

        // Reprocessing retains this token. Each dispatch consumes shared work
        // budget, so a long chain yields without an arbitrary pass limit.
        while (true)
        {
            if (_framesetReplacementStage != 0)
            {
                if (!AdvanceFramesetReplacement()) return new HtmlParseStep(HtmlParseStepKind.Yielded);
                FinishToken();
                return new HtmlParseStep(HtmlParseStepKind.Yielded);
            }
            if (_pendingShiftIndex >= 0)
            {
                while (_pendingShiftIndex < _open.Count - 1 && _remaining > 0)
                {
                    var moved = _open[_pendingShiftIndex + 1];
                    _open[_pendingShiftIndex] = moved;
                    ShiftIndexes(moved, _pendingShiftIndex + 1);
                    _pendingShiftIndex++;
                }
                if (_pendingShiftIndex < _open.Count - 1) return new HtmlParseStep(HtmlParseStepKind.Yielded);
                _open.RemoveAt(_open.Count - 1);
                _pendingShiftIndex = -1;
                FinishToken();
                return new HtmlParseStep(HtmlParseStepKind.Yielded);
            }
            if (_pendingPopTarget >= 0)
            {
                while (_open.Count > _pendingPopTarget && _remaining > 0) Pop();
                if (_open.Count > _pendingPopTarget) return new HtmlParseStep(HtmlParseStepKind.Yielded);
                if (_clearFormattingAfterPop)
                {
                    if (!TryClearFormattingToMarker()) return new HtmlParseStep(HtmlParseStepKind.Yielded);
                    _clearFormattingAfterPop = false;
                }
                if (_popTemplateModeAfterPop)
                {
                    _templateModes.RemoveAt(_templateModes.Count - 1);
                    Charge(1);
                    _popTemplateModeAfterPop = false;
                }
                _pendingPopTarget = -1;
                if (_modeAfterPop is { } nextMode) { _mode = nextMode; _modeAfterPop = null; }
                if (_resetAfterPop)
                {
                    _resetAfterPop = false;
                    ResetInsertionMode();
                    if (_missing is { } resetFamily)
                        return new HtmlParseStep(HtmlParseStepKind.MissingFeature, resetFamily, _token.Offset);
                }
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
            if (_delegateToBody && _mode != _delegatedFromMode) _delegateToBody = false;
            var reprocess = Dispatch(_delegateToBody ? Mode.InBody : _mode);
            if (_missing is { } family)
                return new HtmlParseStep(HtmlParseStepKind.MissingFeature, family, _token.Offset);
            if (_pendingShiftIndex >= 0) continue;
            if (_pendingPopTarget >= 0) continue;
            if (!reprocess)
            {
                var eof = _token.Kind == HtmlTokenKind.EndOfFile;
                FinishToken();
                return new HtmlParseStep(eof ? HtmlParseStepKind.Complete : HtmlParseStepKind.Yielded);
            }
        }
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
        Mode.InTable => InTable(),
        Mode.InTableText => throw new InvalidOperationException("Table text must flush before dispatch."),
        Mode.InCaption => InCaption(),
        Mode.InColumnGroup => InColumnGroup(),
        Mode.InTableBody => InTableBody(),
        Mode.InRow => InRow(),
        Mode.InCell => InCell(),
        Mode.InTemplate => InTemplate(),
        Mode.AfterBody => InAfterBody(),
        Mode.InFrameset => InFrameset(),
        Mode.AfterFrameset => InAfterFrameset(),
        Mode.AfterAfterBody => InAfterAfterBody(),
        Mode.AfterAfterFrameset => InAfterAfterFrameset(),
        _ => throw new InvalidOperationException("Unknown HTML insertion mode.")
    };

    private void FinishToken()
    {
        if (_token.Kind == HtmlTokenKind.StartTag && _token.SelfClosing && !_acknowledgedSelfClosing)
            Error("unacknowledged-self-closing-flag");
        _hasToken = false;
        _missing = null;
        _fosterParenting = false;
        _delegateToBody = false;
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

    private Element InsertElement(string name, ParserAttribute[]? attributes = null, Node? parentOverride = null,
        long attributeWork = 0)
    {
        CheckDepth();
        var location = FindAdjustedInsertionLocation(parentOverride ?? _headInsertionOverride);
        var owner = location.Parent as Document ?? location.Parent.OwnerDocument!;
        var element = owner.CreateParsedElement(Namespaces.Html, name, null);
        if (attributes is { Length: > 0 })
        {
            element.InitializeParsedAttributes(attributes, _cancellationToken);
            Charge(attributeWork);
        }
        InsertAt(location, element);
        Push(element);
        return element;
    }

    private Element InsertTokenElement(Node? parentOverride = null) =>
        InsertElement(_token.Name!, _preparedAttributes, parentOverride, _preparedAttributeWork);

    private bool PrepareTokenAttributes()
    {
        var attributes = _token.Attributes;
        _preparedAttributes ??= new ParserAttribute[attributes.Count];
        while (_preparedAttributeIndex < attributes.Count && _remaining > 0)
        {
            var item = attributes[_preparedAttributeIndex];
            _preparedAttributes[_preparedAttributeIndex++] = new ParserAttribute(null, item.Name, null, item.Value);
            if (_token.Name == "template")
            {
                if (item.Name == "for") _templateHasFor = true;
                if (item.Name == "shadowrootmode" &&
                    (string.Equals(item.Value, "open", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(item.Value, "closed", StringComparison.OrdinalIgnoreCase)))
                    _templateOrdinaryShadowFallback = true;
            }
            var itemWork = 1L + item.Name.Length + item.Value.Length;
            _preparedAttributeWork = _preparedAttributeWork > long.MaxValue - itemWork ? long.MaxValue : _preparedAttributeWork + itemWork;
            var formattingWork = IsFormatting(_token.Name!) ? PrepareFormattingAttribute(item) : 0;
            Charge(itemWork + formattingWork);
        }
        return _preparedAttributeIndex == attributes.Count;
    }

    private void MergeAttributes(Element target)
    {
        if (_token.Attributes.Count == 0) return;
        var existing = target.AttributeCount;
        target.AddMissingParsedAttributes(_preparedAttributes!, _cancellationToken);
        Charge(existing + _preparedAttributes!.Length);
    }

    private void Push(Element element)
    {
        var index = _open.Count;
        _open.Add(element);
        _openIdentity.Add(element);
        AddIndexes(element, index);
        if (!AllowedOpenAtEof(element.LocalName)) _unexpectedOpenCount++;
        Charge(1);
    }

    private void AddIndexes(Element element, int index)
    {
        if (!_nameIndexes.TryGetValue(element.LocalName, out var indexes))
            _nameIndexes[element.LocalName] = indexes = [];
        indexes.Add(index);
        if (IsSpecialElement(element)) _specialIndexes.Add(index);
        if (IsSpecialElement(element) && element.LocalName is not ("address" or "div" or "p" or "li"))
            _liStops.Add(index);
        if (IsSpecialElement(element) && element.LocalName is not ("address" or "div" or "p" or "dd" or "dt"))
            _ddDtStops.Add(index);
        if (IsScopeBoundary(element)) _scopeStops.Add(index);
        if (IsResetModeElement(element)) _resetModeIndexes.Add(index);
    }

    private void RemoveIndexes(Element element, int index)
    {
        var names = _nameIndexes[element.LocalName];
        RemoveIndex(names, index);
        if (names.Count == 0) _nameIndexes.Remove(element.LocalName);
        if (IsSpecialElement(element))
        {
            RemoveIndex(_specialIndexes, index);
            if (element.LocalName is not ("address" or "div" or "p" or "li")) RemoveIndex(_liStops, index);
            if (element.LocalName is not ("address" or "div" or "p" or "dd" or "dt")) RemoveIndex(_ddDtStops, index);
        }
        if (IsScopeBoundary(element)) RemoveIndex(_scopeStops, index);
        if (IsResetModeElement(element)) RemoveIndex(_resetModeIndexes, index);
    }

    private void ShiftIndexes(Element element, int oldIndex)
    {
        ShiftIndex(_nameIndexes[element.LocalName], oldIndex);
        if (IsSpecialElement(element))
        {
            ShiftIndex(_specialIndexes, oldIndex);
            if (element.LocalName is not ("address" or "div" or "p" or "li")) ShiftIndex(_liStops, oldIndex);
            if (element.LocalName is not ("address" or "div" or "p" or "dd" or "dt")) ShiftIndex(_ddDtStops, oldIndex);
        }
        if (IsScopeBoundary(element)) ShiftIndex(_scopeStops, oldIndex);
        if (IsResetModeElement(element)) ShiftIndex(_resetModeIndexes, oldIndex);
        Charge(1);
    }

    private void RemoveIndex(List<int> indexes, int index)
    {
        var position = indexes.BinarySearch(index);
        if (position < 0) throw new InvalidOperationException("HTML stack index was not found.");
        var shifted = indexes.Count - position - 1;
        var search = SearchCost(indexes.Count);
        indexes.RemoveAt(position);
        Charge(search + (long) shifted);
    }

    private void ShiftIndex(List<int> indexes, int oldIndex)
    {
        var position = indexes.BinarySearch(oldIndex);
        if (position < 0) throw new InvalidOperationException("HTML stack index was not found.");
        indexes[position] = oldIndex - 1;
        Charge(SearchCost(indexes.Count));
    }

    private static int SearchCost(int count) => count > 0 ? BitOperations.Log2((uint) count) + 1 : 0;

    private Element Pop()
    {
        var index = _open.Count - 1;
        var element = _open[index];
        _open.RemoveAt(index);
        _openIdentity.Remove(element);
        var indexes = _nameIndexes[element.LocalName];
        indexes.RemoveAt(indexes.Count - 1);
        if (indexes.Count == 0) _nameIndexes.Remove(element.LocalName);
        if (_specialIndexes.Count > 0 && _specialIndexes[^1] == index) _specialIndexes.RemoveAt(_specialIndexes.Count - 1);
        if (_liStops.Count > 0 && _liStops[^1] == index) _liStops.RemoveAt(_liStops.Count - 1);
        if (_ddDtStops.Count > 0 && _ddDtStops[^1] == index) _ddDtStops.RemoveAt(_ddDtStops.Count - 1);
        if (_scopeStops.Count > 0 && _scopeStops[^1] == index) _scopeStops.RemoveAt(_scopeStops.Count - 1);
        if (_resetModeIndexes.Count > 0 && _resetModeIndexes[^1] == index) _resetModeIndexes.RemoveAt(_resetModeIndexes.Count - 1);
        if (!AllowedOpenAtEof(element.LocalName)) _unexpectedOpenCount--;
        Charge(1);
        return element;
    }

    private void SchedulePopTo(int index, bool reprocess, Mode? nextMode = null,
        bool clearFormatting = false, bool resetMode = false)
    {
        if (index < 0 || index >= _open.Count) throw new InvalidOperationException("Invalid HTML stack pop target.");
        _pendingPopTarget = index;
        _reprocessAfterPop = reprocess;
        _modeAfterPop = nextMode;
        _clearFormattingAfterPop = clearFormatting;
        _resetAfterPop = resetMode;
    }

    private int Last(string name) => _nameIndexes.TryGetValue(name, out var indexes) ? indexes[^1] : -1;
    private int LastSpecial => _specialIndexes.Count == 0 ? -1 : _specialIndexes[^1];
    private int LastLiStop => _liStops.Count == 0 ? -1 : _liStops[^1];
    private int LastDdDtStop => _ddDtStops.Count == 0 ? -1 : _ddDtStops[^1];
    private int LastScopeStop => _scopeStops.Count == 0 ? -1 : _scopeStops[^1];
    private bool InScope(string name) => Last(name) >= 0 && Last(name) >= LastScopeStop;
    private bool InButtonScope(string name) => Last(name) >= 0 && Last(name) >= Math.Max(LastScopeStop, Last("button"));
    private bool InListItemScope(string name) => Last(name) >= 0 && Last(name) >= Math.Max(LastScopeStop, Math.Max(Last("ol"), Last("ul")));
    private bool InTableScope(string name) => Last(name) >= 0 && Last(name) >= Math.Max(Last("html"), Math.Max(Last("table"), Last("template")));
    private static bool IsHtmlElement(Element element, string name) =>
        element.NamespaceUri == Namespaces.Html && element.LocalName == name;
    private static bool IsScopeBoundary(Element element) => element.NamespaceUri == Namespaces.Html &&
        element.LocalName is "applet" or "caption" or "html" or "table" or "td" or "th" or "marquee" or "object" or "select" or "template";
    private static bool IsResetModeElement(Element element) => element.NamespaceUri == Namespaces.Html &&
        element.LocalName is "td" or "th" or "tr" or "tbody" or "thead" or "tfoot" or "caption" or
            "colgroup" or "table" or "template" or "head" or "body" or "frameset" or "html";

    private bool TryGenerateImpliedEndTags(string? except = null)
    {
        var popped = false;
        while (_open.Count > 0 && Current.LocalName != except && IsImpliedEndTag(Current.LocalName))
        {
            // Dispatch itself costs one unit. Permit one pop when quota is one
            // so resuming this token always advances the explicit stack cursor.
            if (_remaining <= 0 && popped) return false;
            Pop();
            popped = true;
        }
        return true;
    }

    private bool CloseP(bool reprocess)
    {
        if (!TryGenerateImpliedEndTags("p")) return false;
        if (Current.LocalName != "p") Error("misnested-p-end-tag");
        SchedulePopTo(Last("p"), reprocess);
        return true;
    }

    private void InsertComment(Node? parent = null)
    {
        var location = FindAdjustedInsertionLocation(parent);
        var owner = location.Parent as Document ?? location.Parent.OwnerDocument!;
        InsertAt(location, owner.CreateComment(_token.Data));
        Charge(_token.Data.Length + 1L);
    }

    private void InsertProcessingInstruction(Node? parent = null)
    {
        var location = FindAdjustedInsertionLocation(parent);
        var owner = location.Parent as Document ?? location.Parent.OwnerDocument!;
        InsertAt(location, owner.CreateProcessingInstruction(_token.Name!, _token.Data));
        Charge(_token.Data.Length + (_token.Name?.Length ?? 0) + 1L);
    }

    private void InsertText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return;
        var location = FindAdjustedInsertionLocation();
        var predecessor = location.Before?.PreviousSibling ?? (location.Before is null ? location.Parent.LastChild : null);
        if (predecessor is Text previous)
            previous.AppendParsedData(text, _cancellationToken);
        else
        {
            var owner = location.Parent as Document ?? location.Parent.OwnerDocument!;
            var node = owner.CreateTextNode(string.Empty);
            node.AppendParsedData(text, _cancellationToken);
            InsertAt(location, node);
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

    private static bool White(char c) => c is '\t' or '\n' or '\f' or '\r' or ' ';
    private static bool IsHeading(string name) => name is "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
    private static bool IsImpliedEndTag(string name) => name is "dd" or "dt" or "li" or "optgroup" or "option" or "p" or "rb" or "rp" or "rt" or "rtc";
    private static bool AllowedOpenAtEof(string name) => name is "dd" or "dt" or "li" or "optgroup" or
        "option" or "p" or "rb" or "rp" or "rt" or "rtc" or "tbody" or "td" or "tfoot" or "th" or
        "thead" or "tr" or "body" or "html";

    private static bool IsSpecial(string name) => name is
        "address" or "applet" or "area" or "article" or "aside" or "base" or "basefont" or "bgsound" or
        "blockquote" or "body" or "br" or "button" or "caption" or "center" or "col" or "colgroup" or
        "dd" or "details" or "dialog" or "dir" or "div" or "dl" or "dt" or "embed" or "fieldset" or "figcaption" or
        "figure" or "footer" or "form" or "frame" or "frameset" or "h1" or "h2" or "h3" or "h4" or
        "h5" or "h6" or "head" or "header" or "hgroup" or "hr" or "html" or "iframe" or "img" or
        "input" or "keygen" or "li" or "link" or "listing" or "main" or "marquee" or "menu" or
        "meta" or "nav" or "noembed" or "noframes" or "noscript" or "object" or "ol" or "p" or
        "param" or "plaintext" or "pre" or "script" or "search" or "section" or "select" or "source" or
        "style" or "summary" or "table" or "tbody" or "td" or "template" or "textarea" or "tfoot" or
        "th" or "thead" or "title" or "tr" or "track" or "ul" or "wbr" or "xmp";
}
