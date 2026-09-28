using System;
using System.Buffers;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6.4.1–8, .17, .20 (2026-09-22).
    private bool InInitial()
    {
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment:
                { InsertComment(_document); return false; }
            case HtmlTokenKind.ProcessingInstruction:
                { InsertProcessingInstruction(_document); return false; }
            case HtmlTokenKind.Doctype:
                {
                    if (_token.Name != "html" || _token.PublicIdentifier is not null ||
                        _token.SystemIdentifier is not null and not "about:legacy-compat")
                        Error("invalid-doctype");
                    if (_document.Doctype is null && _document.DocumentElement is null)
                        _document.AppendParsedChild(_document.CreateDocumentType(_token.Name ?? string.Empty,
                            _token.PublicIdentifier ?? string.Empty, _token.SystemIdentifier ?? string.Empty));
                    if (!_context.IsSrcdoc && !_context.CannotChangeMode)
                        _document.SetParserMode(HtmlDoctypeClassifier.Classify(_token));
                    _mode = Mode.BeforeHtml;
                    return false;
                }
        }
        if (!_context.IsSrcdoc) Error("missing-doctype");
        if (!_context.CannotChangeMode && !_context.IsSrcdoc) _document.SetParserMode(DocumentMode.Quirks);
        _mode = Mode.BeforeHtml;
        return true;
    }

    private bool InBeforeHtml()
    {
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment: InsertComment(_document); return false;
            case HtmlTokenKind.ProcessingInstruction: InsertProcessingInstruction(_document); return false;
            case HtmlTokenKind.Doctype: Error("unexpected-doctype"); return false;
            case HtmlTokenKind.StartTag when _token.Name == "html":
                InsertTokenElement(_document);
                _mode = Mode.BeforeHead;
                return false;
            case HtmlTokenKind.EndTag when !HtmlHeadBodyHtmlNames.Match(_token.Name):
                Error("unexpected-end-tag"); return false;
            default:
                InsertElement("html", parentOverride: _document);
                _mode = Mode.BeforeHead;
                return true;
        }
    }

    private bool InBeforeHead()
    {
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment: InsertComment(); return false;
            case HtmlTokenKind.ProcessingInstruction: InsertProcessingInstruction(); return false;
            case HtmlTokenKind.Doctype: Error("unexpected-doctype"); return false;
            case HtmlTokenKind.StartTag when _token.Name == "html": InBody(); return false;
            case HtmlTokenKind.StartTag when _token.Name == "head":
                _head = InsertTokenElement();
                _mode = Mode.InHead;
                return false;
            case HtmlTokenKind.EndTag when !HtmlHeadBodyHtmlNames.Match(_token.Name):
                Error("unexpected-end-tag"); return false;
            default:
                _head = InsertElement("head");
                _mode = Mode.InHead;
                return true;
        }
    }

    private bool InHead()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment:
                { InsertComment(); return false; }
            case HtmlTokenKind.ProcessingInstruction:
                { InsertProcessingInstruction(); return false; }
            case HtmlTokenKind.Doctype:
                { Error("unexpected-doctype"); return false; }
            case HtmlTokenKind.StartTag:
                {
                    if (name == "html") { InBody(); return false; }
                    if (HtmlBaseBasefontBgsoundLinkNames.Match(name))
                    {
                        InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true; return false;
                    }
                    if (name == "title") { EnterText(HtmlTextMode.RcData, name); return false; }
                    if (HtmlNoframesStyleNames.Match(name) || name == "noscript" && _scriptingEnabled)
                    {
                        EnterText(HtmlTextMode.RawText, name!); return false;
                    }
                    if (name == "noscript")
                    {
                        InsertTokenElement();
                        _mode = Mode.InHeadNoscript;
                        return false;
                    }
                    if (name == "script") { EnterText(HtmlTextMode.ScriptData, name); return false; }
                    if (name == "template") { StartTemplate(); return false; }
                    if (name == "head") { Error("unexpected-head-start-tag"); return false; }
                }
                break;
            case HtmlTokenKind.EndTag:
                {
                    if (name == "head") { Pop(); _mode = Mode.AfterHead; return false; }
                    if (name == "template") return !EndTemplate();
                    if (!HtmlBodyHtmlBrNames.Match(name)) { Error("unexpected-end-tag"); return false; }
                }
                break;
        }
        if (!IsHtmlElement(Current, "head"))
            throw new InvalidOperationException("The in-head mode lost its head element.");
        Pop();
        _mode = Mode.AfterHead;
        return true;
    }

    private bool InHeadNoscript()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.Doctype:
                { Error("unexpected-doctype"); return false; }
            case HtmlTokenKind.StartTag when name == "html":
                { InBody(); return false; }
            case HtmlTokenKind.EndTag when name == "noscript":
                {
                    Pop(); _mode = Mode.InHead; return false;
                }
            case HtmlTokenKind.Comment or HtmlTokenKind.ProcessingInstruction:
            case HtmlTokenKind.StartTag when HtmlBasefontBgsoundLinkNames.Match(name):
                {
                    InHead(); return false;
                }
            case HtmlTokenKind.StartTag when HtmlHeadNoscriptNames.Match(name):
            case HtmlTokenKind.EndTag when name != "br":
                {
                    Error("unexpected-token-in-head-noscript"); return false;
                }
        }
        Error("unexpected-token-in-head-noscript");
        Pop(); _mode = Mode.InHead; return true;
    }

    private bool InAfterHead()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment:
                { InsertComment(); return false; }
            case HtmlTokenKind.ProcessingInstruction:
                { InsertProcessingInstruction(); return false; }
            case HtmlTokenKind.Doctype:
                { Error("unexpected-doctype"); return false; }
            case HtmlTokenKind.StartTag:
                {
                    if (name == "html") { InBody(); return false; }
                    if (name == "body")
                    {
                        InsertTokenElement(); _framesetOk = false; _mode = Mode.InBody; return false;
                    }
                    if (name == "frameset")
                    {
                        InsertTokenElement(); _mode = Mode.InFrameset; return false;
                    }
                    if (HtmlBaseBasefontBgsoundNames.Match(name))
                    {
                        Error("head-content-after-head");
                        CheckDepth(1); // The temporary head entry counts toward the open-stack bound.
                        _temporaryHeadDepth = 1;
                        _headInsertionOverride = _head ?? throw new InvalidOperationException("Missing head pointer.");
                        try { InHead(); }
                        finally { _headInsertionOverride = null; _temporaryHeadDepth = 0; }
                        return false;
                    }
                    if (name == "head") { Error("unexpected-head-start-tag"); return false; }
                }
                break;
            case HtmlTokenKind.EndTag:
                {
                    if (name == "template") return !EndTemplate();
                    if (!HtmlBodyHtmlBrNames.Match(name)) { Error("unexpected-end-tag"); return false; }
                }
                break;
        }
        InsertElement("body");
        _framesetOk = true;
        _mode = Mode.InBody;
        return true;
    }

    private bool InText()
    {
        if (_token.Kind is HtmlTokenKind.EndTag or HtmlTokenKind.EndOfFile && IsHtmlElement(Current, "script"))
        {
            var state = Current.GetHtmlState()!.Script!;
            var changes = _token.Kind == HtmlTokenKind.EndTag ? _token.SourceChanges : _tokenizer.SourceChanges;
            if (state.ParserSourceChanges != changes && state.ParserSourceLocation is { } source)
                state.ParserSourceLocation = source.AsMixed();
        }
        switch (_token.Kind)
        {
            case HtmlTokenKind.EndOfFile:
                Error("eof-in-text");
                if (IsHtmlElement(Current, "script")) Current.GetHtmlState()!.Script!.AlreadyStarted = true;
                Pop();
                _mode = _originalTextMode;
                return true;
            case HtmlTokenKind.EndTag:
                if (IsHtmlElement(Current, "script") && ScriptRequestsEnabled &&
                    _scriptingMode is not (HtmlParserScriptingMode.Inert or HtmlParserScriptingMode.Fragment))
                {
                    if (!_scriptCheckpointCompleted)
                    {
                        ScriptBoundary = Current;
                        return false;
                    }
                    ClosedScript = Current;
                    ScriptBoundary = null;
                    _scriptCheckpointCompleted = false;
                }
                Pop();
                _mode = _originalTextMode;
                return false;
            default:
                throw new InvalidOperationException("Unexpected token in text mode.");
        }
    }

    private bool InAfterBody()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment:
                { InsertComment(_open[0]); return false; }
            case HtmlTokenKind.ProcessingInstruction:
                { InsertProcessingInstruction(_open[0]); return false; }
            case HtmlTokenKind.Doctype:
                { Error("unexpected-doctype"); return false; }
            case HtmlTokenKind.StartTag when name == "html":
                { InBody(); return false; }
            case HtmlTokenKind.EndTag when name == "html":
                {
                    if (_fragmentContext is not null) Error("unexpected-html-end-tag");
                    else _mode = Mode.AfterAfterBody;
                    return false;
                }
            case HtmlTokenKind.EndOfFile:
                return false;
        }
        Error("unexpected-token-after-body");
        _mode = Mode.InBody;
        return true;
    }

    private bool InAfterAfterBody()
    {
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment:
                { InsertComment(_document); return false; }
            case HtmlTokenKind.ProcessingInstruction:
                { InsertProcessingInstruction(_document); return false; }
            case HtmlTokenKind.Doctype:
            case HtmlTokenKind.StartTag when _token.Name == "html":
                {
                    InBody(); return false;
                }
            case HtmlTokenKind.EndOfFile:
                return false;
        }
        Error("unexpected-token-after-after-body");
        _mode = Mode.InBody;
        return true;
    }

    private void ProcessCharacters()
    {
        var data = _token.DataSlice;
        var span = data.Span;
        while (_textIndex < data.Length && _remaining > 0)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (ShouldUseForeignRules(_token))
            {
                var foreignCharacter = span[_textIndex++];
                if (foreignCharacter == '\0')
                {
                    Error("unexpected-null-character");
                    InsertText(new StringSlice("\uFFFD"));
                }
                else
                {
                    InsertText(data.Slice(_textIndex - 1, 1));
                    if (!White(foreignCharacter)) _framesetOk = false;
                }
                continue;
            }
            if (_ignoreNextLf)
            {
                _ignoreNextLf = false;
                if (span[_textIndex] == '\n') { _textIndex++; Charge(1); continue; }
            }
            var c = span[_textIndex];
            switch (_mode)
            {
                case Mode.Initial:
                    if (White(c)) { _textIndex++; Charge(1); continue; }
                    if (!_context.IsSrcdoc) Error("missing-doctype");
                    if (!_context.CannotChangeMode && !_context.IsSrcdoc) _document.SetParserMode(DocumentMode.Quirks);
                    _mode = Mode.BeforeHtml;
                    continue;
                case Mode.BeforeHtml:
                    if (White(c)) { _textIndex++; Charge(1); continue; }
                    InsertElement("html", parentOverride: _document);
                    _mode = Mode.BeforeHead;
                    continue;
                case Mode.BeforeHead:
                    if (White(c)) { _textIndex++; Charge(1); continue; }
                    _head = InsertElement("head");
                    _mode = Mode.InHead;
                    continue;
                case Mode.InHead:
                    if (White(c)) { AppendCharacterRun(data, whiteOnly: true); continue; }
                    Pop(); _mode = Mode.AfterHead;
                    continue;
                case Mode.InHeadNoscript:
                    if (White(c)) { AppendCharacterRun(data, whiteOnly: true); continue; }
                    Error("unexpected-token-in-head-noscript"); Pop(); _mode = Mode.InHead;
                    continue;
                case Mode.AfterHead:
                    if (White(c)) { AppendCharacterRun(data, whiteOnly: true); continue; }
                    InsertElement("body"); _framesetOk = true; _mode = Mode.InBody;
                    continue;
                case Mode.InFrameset:
                case Mode.AfterFrameset:
                    if (White(c)) { AppendCharacterRun(data, whiteOnly: true); continue; }
                    Error("unexpected-character-in-frameset"); _textIndex++; Charge(1);
                    continue;
                case Mode.AfterAfterFrameset:
                    if (White(c))
                    {
                        if (!TryReconstructFormatting()) return;
                        AppendCharacterRun(data, whiteOnly: true);
                        continue;
                    }
                    Error("unexpected-character-after-after-frameset"); _textIndex++; Charge(1);
                    continue;
                case Mode.InBody:
                case Mode.InCaption:
                case Mode.InCell:
                case Mode.InTemplate:
                    if (c == '\0') { Error("unexpected-null-character"); _textIndex++; Charge(1); continue; }
                    if (!TryReconstructFormatting()) return;
                    if (AppendBodyCharacterRun(data)) _framesetOk = false;
                    continue;
                case Mode.InTable:
                case Mode.InTableBody:
                case Mode.InRow:
                    if (Current.NamespaceUri == Namespaces.Html &&
                        HtmlTableTbodyTemplateNames.Match(Current.LocalName))
                    {
                        EnterTableText();
                        continue;
                    }
                    // The other table character branch uses the in-body rules
                    // with foster parenting for this character token.
                    if (!_tableFosterCharacterErrorReported)
                    {
                        Error("unexpected-token-in-table");
                        _tableFosterCharacterErrorReported = true;
                    }
                    if (c == '\0') { Error("unexpected-null-character"); _textIndex++; Charge(1); continue; }
                    _fosterParenting = true;
                    if (!TryReconstructFormatting()) return;
                    if (AppendBodyCharacterRun(data)) _framesetOk = false;
                    continue;
                case Mode.InTableText:
                    BufferTableText();
                    continue;
                case Mode.InColumnGroup:
                    if (White(c)) { AppendCharacterRun(data, whiteOnly: true); continue; }
                    if (!IsHtmlElement(Current, "colgroup")) { Error("unexpected-column-group-character"); _textIndex++; Charge(1); continue; }
                    Pop(); _mode = Mode.InTable;
                    continue;
                case Mode.Text:
                    if (c == '\0') Error("unexpected-null-character");
                    AppendCharacterRun(data, whiteOnly: false, textMode: true);
                    continue;
                case Mode.AfterBody:
                case Mode.AfterAfterBody:
                    if (White(c))
                    {
                        if (!TryReconstructFormatting()) return;
                        AppendCharacterRun(data, whiteOnly: true);
                        continue;
                    }
                    Error("unexpected-token-after-body"); _mode = Mode.InBody;
                    continue;
                default:
                    throw new InvalidOperationException("Unknown insertion mode for character token.");
            }
        }
    }

    // HTML Standard §13.2.6.4.7, "any other character token" in body: whitespace and other characters
    // take the same steps, reconstructing formatting (a no-op once done for the run) and inserting
    // the character; only non-whitespace clears frameset-ok. So one run spans both and stops at NUL,
    // which keeps "a b c" one text append instead of five. Returns whether the run had non-whitespace.
    private bool AppendBodyCharacterRun(StringSlice data)
    {
        var start = _textIndex;
        var max = (int) Math.Min(data.Length, start + Math.Max(1, Math.Min(_remaining, 2048)));
        var run = data.Span[start..max];
        var length = run.IndexOf('\0');
        if (length < 0) length = run.Length;
        run = run[..length];
        _textIndex = start + length;
        InsertText(data.Slice(start, length));
        return run.IndexOfAnyExcept(BodyWhitespace) >= 0;
    }

    private static readonly SearchValues<char> BodyWhitespace = SearchValues.Create("\t\n\f\r ");

    private void AppendCharacterRun(StringSlice data, bool whiteOnly, bool textMode = false)
    {
        var span = data.Span;
        var start = _textIndex;
        var max = (int) Math.Min(data.Length, start + Math.Max(1, Math.Min(_remaining, 2048)));
        while (_textIndex < max)
        {
            var c = span[_textIndex];
            if (c == '\0' && !textMode || !textMode && White(c) != whiteOnly) break;
            _textIndex++;
        }
        InsertText(data.Slice(start, _textIndex - start));
    }
}
