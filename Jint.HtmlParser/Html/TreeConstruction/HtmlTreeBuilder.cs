using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace Jint.HtmlParser.Html;

// HTML Standard §13.2.6.4 (2026-09-25). One native tree builder and
// shared cooperative budget implement document and contextual fragment parsing.
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
    private readonly HtmlParserScriptingMode _scriptingMode;
    internal bool ScriptRequestsEnabled { get; set; }
    internal Element? ScriptBoundary { get; private set; }
    internal Element? ClosedScript { get; private set; }
    internal bool ClosedScriptIsSvg { get; private set; }
    private bool _scriptCheckpointCompleted;

    internal void CompleteScriptCheckpoint()
    {
        _scriptCheckpointCompleted = true;
        ScriptBoundary = null;
    }
    internal void TakeClosedScript()
    {
        ClosedScript = null;
        ClosedScriptIsSvg = false;
    }
    private readonly List<Element> _open = [];
    private Dictionary<(string? Namespace, string Name), List<int>> _nameIndexes = [];
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
    private string? _preparedIsValue;
    private int _preparedAttributeIndex;
    private long _preparedAttributeWork;
    private long _work;
    private CancellationToken _cancellationToken;
    private long _remaining;

    internal HtmlTreeBuilder(Document document, HtmlTokenizer tokenizer, int maxDepth, bool scriptingEnabled,
        ParseDiagnosticCollector? diagnostics, HtmlDocumentContext context)
        : this(document, tokenizer, maxDepth,
            scriptingEnabled ? HtmlParserScriptingMode.Normal : HtmlParserScriptingMode.Disabled, diagnostics, context)
    {
    }

    internal HtmlTreeBuilder(Document document, HtmlTokenizer tokenizer, int maxDepth, HtmlParserScriptingMode scriptingMode,
        ParseDiagnosticCollector? diagnostics, HtmlDocumentContext context)
    {
        _document = document;
        _tokenizer = tokenizer;
        _maxDepth = maxDepth;
        _scriptingMode = scriptingMode;
        _scriptingEnabled = scriptingMode != HtmlParserScriptingMode.Disabled;
        _diagnostics = diagnostics;
        _context = context;
    }

    internal bool HasToken => _hasToken || _pendingFormElement is not null;
    internal bool AllowCData => _open.Count != 0 && AdjustedCurrent.NamespaceUri != Namespaces.Html;
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
        _preparedIsValue = null;
        _preparedAttributeIndex = 0;
        _preparedAttributeWork = 0;
        _templateHasFor = false;
        _templateHasValidShadowMode = false;
        _templateForValue = null;
        _templateDelegatesFocus = false;
        _templateSerializable = false;
        _templateClonable = false;
        _templateManualSlotAssignment = false;
        _templateKeepRegistryNull = false;
        ResetFormattingToken();
        _fosterParenting = false;
        _delegateToBody = false;
        _tableFosterCharacterErrorReported = false;
        _foreignHtmlReprocess = false;
        _foreignEndScan = -1;
        _foreignEndStarted = false;
        _foreignInitialEndComparison = false;
        _foreignNameCursor = 0;
        _foreignBreakout = false;
        _foreignAttributeIndex = 0;
        _foreignFontBreakout = false;
        _foreignAnnotationEncoding = false;
    }

    internal HtmlParseStep Process(long quota, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _remaining = quota;
        cancellationToken.ThrowIfCancellationRequested();
        if (_pendingFormElement is not null)
        {
            AdvanceFormInsertion();
            return new HtmlParseStep(HtmlParseStepKind.Yielded);
        }
        if (!_hasToken) throw new InvalidOperationException("No tree token is pending.");
        if (_token.Kind != HtmlTokenKind.Text && _mode == Mode.InTableText)
        {
            if (!FlushTableText()) return new HtmlParseStep(HtmlParseStepKind.Yielded);
            _mode = _tableTextOriginalMode;
            if (_remaining <= 0) return new HtmlParseStep(HtmlParseStepKind.Yielded);
        }
        switch (_token.Kind)
        {
            case HtmlTokenKind.Text:
                {
                    ProcessCharacters();
                    if (_missing is { } textFamily)
                        return new HtmlParseStep(HtmlParseStepKind.MissingFeature, textFamily, _token.Offset);
                    if (_textIndex < _token.DataSlice.Length) return new HtmlParseStep(HtmlParseStepKind.Yielded);
                    FinishToken();
                    return new HtmlParseStep(HtmlParseStepKind.Yielded);
                }
            case HtmlTokenKind.StartTag when _token.Attributes.Count > 0 && !PrepareTokenAttributes():
                return new HtmlParseStep(HtmlParseStepKind.Yielded);
        }

        _ignoreNextLf = false;

        // Reprocessing retains this token. Each dispatch consumes shared work
        // budget, so a long chain yields without an arbitrary pass limit.
        while (true)
        {
            if (HasCompletedStyles) return new HtmlParseStep(HtmlParseStepKind.Yielded);
            if (_templateOperation is not null)
            {
                if (!AdvanceTemplateOperation()) return new HtmlParseStep(HtmlParseStepKind.Yielded);
                if (_pendingPopTarget >= 0) continue;
                FinishToken();
                return new HtmlParseStep(HtmlParseStepKind.Yielded);
            }
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
                while (_open.Count > _pendingPopTarget && _remaining > 0 && !HasCompletedStyles) Pop();
                if (HasCompletedStyles) return new HtmlParseStep(HtmlParseStepKind.Yielded);
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
                if (poppedEof && _open.Count != 0) { SchedulePopTo(0, reprocess: false); continue; }
                FinishToken();
                return new HtmlParseStep(poppedEof ? HtmlParseStepKind.Complete : HtmlParseStepKind.Yielded);
            }
            if (_remaining <= 0) return new HtmlParseStep(HtmlParseStepKind.Yielded);
            Charge(1);
            if (_delegateToBody && _mode != _delegatedFromMode) _delegateToBody = false;
            var reprocess = !_foreignHtmlReprocess && (_foreignBreakout || ShouldUseForeignRules(_token))
                ? InForeign() : Dispatch(_delegateToBody ? Mode.InBody : _mode);
            if (ScriptBoundary is not null) return new HtmlParseStep(HtmlParseStepKind.Yielded);
            if (_missing is { } family)
                return new HtmlParseStep(HtmlParseStepKind.MissingFeature, family, _token.Offset);
            if (_pendingShiftIndex >= 0) continue;
            if (_pendingPopTarget >= 0) continue;
            if (_templateOperation is not null) continue;
            if (!reprocess)
            {
                var eof = _token.Kind == HtmlTokenKind.EndOfFile;
                // HTML Standard §13.2.7 stop parsing empties the open stack.
                if (eof && _open.Count != 0) { SchedulePopTo(0, reprocess: false); continue; }
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
        _foreignHtmlReprocess = false;
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
        long attributeWork = 0, string? isValue = null, bool onlyAddToStack = false)
    {
        CheckDepth();
        var location = FindAdjustedInsertionLocation(parentOverride ?? _headInsertionOverride);
        var owner = location.Parent as Document ?? location.Parent.OwnerDocument!;
        var element = owner.CreateParsedElement(Namespaces.Html, name, null, isValue);
        if (name == "script")
        {
            var state = element.GetHtmlState()!.Script!;
            if (_scriptingMode != HtmlParserScriptingMode.Fragment) state.ParserDocument = _document;
            state.ForceAsync = false;
            state.ParserSourceLocation = _token.ScriptSourceLocation;
            state.ParserSourceChanges = _token.SourceChanges;
            if (_scriptingMode == HtmlParserScriptingMode.Inert) state.AlreadyStarted = true;
        }
        if (attributes is { Length: > 0 })
        {
            element.InitializeParsedAttributes(attributes, _cancellationToken);
            Charge(attributeWork);
        }
        if (!onlyAddToStack && !DeferFormInsertion(element, location, parentOverride ?? _headInsertionOverride ?? CurrentParent)) InsertAt(location, element);
        Push(element);
        return element;
    }

    private Element InsertTokenElement(Node? parentOverride = null) =>
        InsertElement(_token.Name!, _preparedAttributes, parentOverride, _preparedAttributeWork, _preparedIsValue);

    private bool PrepareTokenAttributes()
    {
        var attributes = _token.Attributes;
        _preparedAttributes ??= new ParserAttribute[attributes.Count];
        while (_preparedAttributeIndex < attributes.Count && _remaining > 0)
        {
            var item = attributes[_preparedAttributeIndex];
            var formatting = IsFormatting(_token.Name!);
            if (formatting) item = new HtmlAttribute(item.Name, item.Value);
            _preparedAttributes[_preparedAttributeIndex++] = new ParserAttribute(null, item.Name, null, item.ValueSlice);
            if (item.Name == "is") _preparedIsValue = item.Value;
            if (HtmlColorFaceSizeNames.Match(item.Name)) _foreignFontBreakout = true;
            if (item.Name == "encoding")
                _foreignAnnotationEncoding = AsciiEquals(item.ValueSlice.Span, "text/html") || AsciiEquals(item.ValueSlice.Span, "application/xhtml+xml");
            if (_token.Name == "template")
            {
                if (item.Name == "for") { _templateHasFor = true; _templateForValue = item.Value; }
                if (item.Name == "shadowrootmode" &&
                    (item.ValueSlice.Span.Equals("open", StringComparison.OrdinalIgnoreCase) ||
                     item.ValueSlice.Span.Equals("closed", StringComparison.OrdinalIgnoreCase)))
                    _templateHasValidShadowMode = true;
                if (item.Name == "shadowrootdelegatesfocus") _templateDelegatesFocus = true;
                if (item.Name == "shadowrootserializable") _templateSerializable = true;
                if (item.Name == "shadowrootclonable") _templateClonable = true;
                if (item.Name == "shadowrootcustomelementregistry") _templateKeepRegistryNull = true;
                if (item.Name == "shadowrootslotassignment" && AsciiEquals(item.ValueSlice.Span, "manual")) _templateManualSlotAssignment = true;
            }
            var itemWork = 1L + item.Name.Length + item.ValueSlice.Length;
            _preparedAttributeWork = _preparedAttributeWork > long.MaxValue - itemWork ? long.MaxValue : _preparedAttributeWork + itemWork;
            var formattingWork = formatting ? PrepareFormattingAttribute(item) : 0;
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
        if (!AllowedOpenAtEof(element)) _unexpectedOpenCount++;
        Charge(1);
    }

    private void AddIndexes(Element element, int index)
    {
        if (!_nameIndexes.TryGetValue((element.NamespaceUri, element.LocalName), out var indexes))
            _nameIndexes[(element.NamespaceUri, element.LocalName)] = indexes = [];
        indexes.Add(index);
        if (IsSpecialElement(element)) _specialIndexes.Add(index);
        if (IsSpecialElement(element) && !HtmlAddressDivPNames.Match(element.LocalName))
            _liStops.Add(index);
        if (IsSpecialElement(element) && !HtmlAddressDivPDdNames.Match(element.LocalName))
            _ddDtStops.Add(index);
        if (IsScopeBoundary(element)) _scopeStops.Add(index);
        if (IsResetModeElement(element)) _resetModeIndexes.Add(index);
    }

    private void RemoveIndexes(Element element, int index)
    {
        _annotationXmlHtmlIntegration.Remove(element);
        var names = _nameIndexes[(element.NamespaceUri, element.LocalName)];
        RemoveIndex(names, index);
        if (names.Count == 0) _nameIndexes.Remove((element.NamespaceUri, element.LocalName));
        if (IsSpecialElement(element))
        {
            RemoveIndex(_specialIndexes, index);
            if (!HtmlAddressDivPNames.Match(element.LocalName)) RemoveIndex(_liStops, index);
            if (!HtmlAddressDivPDdNames.Match(element.LocalName)) RemoveIndex(_ddDtStops, index);
        }
        if (IsScopeBoundary(element)) RemoveIndex(_scopeStops, index);
        if (IsResetModeElement(element)) RemoveIndex(_resetModeIndexes, index);
    }

    private void ShiftIndexes(Element element, int oldIndex)
    {
        ShiftIndex(_nameIndexes[(element.NamespaceUri, element.LocalName)], oldIndex);
        if (IsSpecialElement(element))
        {
            ShiftIndex(_specialIndexes, oldIndex);
            if (!HtmlAddressDivPNames.Match(element.LocalName)) ShiftIndex(_liStops, oldIndex);
            if (!HtmlAddressDivPDdNames.Match(element.LocalName)) ShiftIndex(_ddDtStops, oldIndex);
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
        _annotationXmlHtmlIntegration.Remove(element);
        var indexes = _nameIndexes[(element.NamespaceUri, element.LocalName)];
        indexes.RemoveAt(indexes.Count - 1);
        if (indexes.Count == 0) _nameIndexes.Remove((element.NamespaceUri, element.LocalName));
        if (_specialIndexes.Count > 0 && _specialIndexes[^1] == index) _specialIndexes.RemoveAt(_specialIndexes.Count - 1);
        if (_liStops.Count > 0 && _liStops[^1] == index) _liStops.RemoveAt(_liStops.Count - 1);
        if (_ddDtStops.Count > 0 && _ddDtStops[^1] == index) _ddDtStops.RemoveAt(_ddDtStops.Count - 1);
        if (_scopeStops.Count > 0 && _scopeStops[^1] == index) _scopeStops.RemoveAt(_scopeStops.Count - 1);
        if (_resetModeIndexes.Count > 0 && _resetModeIndexes[^1] == index) _resetModeIndexes.RemoveAt(_resetModeIndexes.Count - 1);
        if (!AllowedOpenAtEof(element)) _unexpectedOpenCount--;
        Charge(1);
        // HTML Standard §13.2.6: option completion runs after its contents are
        // parsed, including implied closure and EOF recovery. Native selectedcontent
        // replacement is one coherent mutation boundary, not a cooperative walk.
        if (IsHtmlElement(element, "option"))
        {
            HtmlSelectedContent.MaybeCloneOption(element, _cancellationToken);
            _cancellationToken.ThrowIfCancellationRequested();
        }
        CompleteStyle(element);
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

    private int Last(string name) => _nameIndexes.TryGetValue((Namespaces.Html, name), out var indexes) ? indexes[^1] : -1;
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
    private static bool IsScopeBoundary(Element element) => element.NamespaceUri switch
    {
        Namespaces.Html => HtmlAppletCaptionHtmlNames.Match(element.LocalName),
        Namespaces.MathMl => HtmlMiMoMnNames.Match(element.LocalName),
        Namespaces.Svg => HtmlForeignObjectDescTitleNames.Match(element.LocalName),
        _ => false
    };
    private static bool IsResetModeElement(Element element) => element.NamespaceUri == Namespaces.Html &&
        HtmlTdThTrNames.Match(element.LocalName);

    private bool TryGenerateImpliedEndTags(string? except = null)
    {
        var popped = false;
        while (_open.Count > 0 && Current.NamespaceUri == Namespaces.Html && Current.LocalName != except && IsImpliedEndTag(Current.LocalName))
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
        if (!IsHtmlElement(Current, "p")) Error("misnested-p-end-tag");
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

    private void InsertText(StringSlice text)
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
    private static bool IsHeading(string name) => HtmlHeadingLookup.Match(name);
    private static bool IsImpliedEndTag(string name) => HtmlImpliedEndLookup.Match(name);
    private static bool AllowedOpenAtEof(Element element) => element.NamespaceUri == Namespaces.Html && HtmlDdDtLiNames.Match(element.LocalName);

    private static bool IsSpecial(string name) => HtmlSpecialElementLookup.Match(name);
}
