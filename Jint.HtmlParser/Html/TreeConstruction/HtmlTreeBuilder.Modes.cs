using System;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6.4.1–8, .17, .20 (2026-09-22).
    private bool InInitial()
    {
        if (_token.Kind == HtmlTokenKind.Comment) { InsertComment(_document); return false; }
        if (_token.Kind == HtmlTokenKind.ProcessingInstruction) { InsertProcessingInstruction(_document); return false; }
        if (_token.Kind == HtmlTokenKind.Doctype)
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
            case HtmlTokenKind.EndTag when _token.Name is not ("head" or "body" or "html" or "br"):
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
            case HtmlTokenKind.EndTag when _token.Name is not ("head" or "body" or "html" or "br"):
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
        if (_token.Kind == HtmlTokenKind.Comment) { InsertComment(); return false; }
        if (_token.Kind == HtmlTokenKind.ProcessingInstruction) { InsertProcessingInstruction(); return false; }
        if (_token.Kind == HtmlTokenKind.Doctype) { Error("unexpected-doctype"); return false; }
        if (_token.Kind == HtmlTokenKind.StartTag)
        {
            if (name == "html") { InBody(); return false; }
            if (name is "base" or "basefont" or "bgsound" or "link" or "meta")
            {
                InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true; return false;
            }
            if (name == "title") { EnterText(HtmlTextMode.RcData, name); return false; }
            if (name is "noframes" or "style" || name == "noscript" && _scriptingEnabled)
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
            if (name == "template") { Missing(HtmlMissingFeature.Templates); return false; }
            if (name == "head") { Error("unexpected-head-start-tag"); return false; }
        }
        if (_token.Kind == HtmlTokenKind.EndTag)
        {
            if (name == "head") { Pop(); _mode = Mode.AfterHead; return false; }
            if (name == "template") { Missing(HtmlMissingFeature.Templates); return false; }
            if (name is not ("body" or "html" or "br")) { Error("unexpected-end-tag"); return false; }
        }
        if (Current.LocalName != "head")
            throw new InvalidOperationException("The in-head mode lost its head element.");
        Pop();
        _mode = Mode.AfterHead;
        return true;
    }

    private bool InHeadNoscript()
    {
        var name = _token.Name;
        if (_token.Kind == HtmlTokenKind.Doctype) { Error("unexpected-doctype"); return false; }
        if (_token.Kind == HtmlTokenKind.StartTag && name == "html") { InBody(); return false; }
        if (_token.Kind == HtmlTokenKind.EndTag && name == "noscript")
        {
            Pop(); _mode = Mode.InHead; return false;
        }
        if (_token.Kind is HtmlTokenKind.Comment or HtmlTokenKind.ProcessingInstruction ||
            _token.Kind == HtmlTokenKind.StartTag && name is "basefont" or "bgsound" or "link" or "meta" or "noframes" or "style")
        {
            InHead(); return false;
        }
        if (_token.Kind == HtmlTokenKind.StartTag && name is "head" or "noscript" ||
            _token.Kind == HtmlTokenKind.EndTag && name != "br")
        {
            Error("unexpected-token-in-head-noscript"); return false;
        }
        Error("unexpected-token-in-head-noscript");
        Pop(); _mode = Mode.InHead; return true;
    }

    private bool InAfterHead()
    {
        var name = _token.Name;
        if (_token.Kind == HtmlTokenKind.Comment) { InsertComment(); return false; }
        if (_token.Kind == HtmlTokenKind.ProcessingInstruction) { InsertProcessingInstruction(); return false; }
        if (_token.Kind == HtmlTokenKind.Doctype) { Error("unexpected-doctype"); return false; }
        if (_token.Kind == HtmlTokenKind.StartTag)
        {
            if (name == "html") { InBody(); return false; }
            if (name == "body")
            {
                InsertTokenElement(); _framesetOk = false; _mode = Mode.InBody; return false;
            }
            if (name == "frameset") { Missing(HtmlMissingFeature.Framesets); return false; }
            if (name is "base" or "basefont" or "bgsound" or "link" or "meta" or "noframes" or "script" or "style" or "template" or "title")
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
        if (_token.Kind == HtmlTokenKind.EndTag)
        {
            if (name == "template") { Missing(HtmlMissingFeature.Templates); return false; }
            if (name is not ("body" or "html" or "br")) { Error("unexpected-end-tag"); return false; }
        }
        InsertElement("body");
        _framesetOk = true;
        _mode = Mode.InBody;
        return true;
    }

    private bool InText()
    {
        if (_token.Kind == HtmlTokenKind.EndOfFile)
        {
            Error("eof-in-text");
            Pop();
            _mode = _originalTextMode;
            return true;
        }
        if (_token.Kind == HtmlTokenKind.EndTag)
        {
            Pop();
            _mode = _originalTextMode;
            return false;
        }
        throw new InvalidOperationException("Unexpected token in text mode.");
    }

    private bool InAfterBody()
    {
        var name = _token.Name;
        if (_token.Kind == HtmlTokenKind.Comment) { InsertComment(_open[0]); return false; }
        if (_token.Kind == HtmlTokenKind.ProcessingInstruction) { InsertProcessingInstruction(_open[0]); return false; }
        if (_token.Kind == HtmlTokenKind.Doctype) { Error("unexpected-doctype"); return false; }
        if (_token.Kind == HtmlTokenKind.StartTag && name == "html") { InBody(); return false; }
        if (_token.Kind == HtmlTokenKind.EndTag && name == "html") { _mode = Mode.AfterAfterBody; return false; }
        if (_token.Kind == HtmlTokenKind.EndOfFile) return false;
        Error("unexpected-token-after-body");
        _mode = Mode.InBody;
        return true;
    }

    private bool InAfterAfterBody()
    {
        if (_token.Kind == HtmlTokenKind.Comment) { InsertComment(_document); return false; }
        if (_token.Kind == HtmlTokenKind.ProcessingInstruction) { InsertProcessingInstruction(_document); return false; }
        if (_token.Kind == HtmlTokenKind.Doctype || _token.Kind == HtmlTokenKind.StartTag && _token.Name == "html")
        {
            InBody(); return false;
        }
        if (_token.Kind == HtmlTokenKind.EndOfFile) return false;
        Error("unexpected-token-after-after-body");
        _mode = Mode.InBody;
        return true;
    }

    private void ProcessCharacters()
    {
        var data = _token.Data;
        while (_textIndex < data.Length && _remaining > 0)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_ignoreNextLf)
            {
                _ignoreNextLf = false;
                if (data[_textIndex] == '\n') { _textIndex++; Charge(1); continue; }
            }
            var c = data[_textIndex];
            switch (_mode)
            {
                case Mode.Initial:
                case Mode.BeforeHtml:
                case Mode.BeforeHead:
                    if (White(c)) { _textIndex++; Charge(1); continue; }
                    if (_mode == Mode.Initial)
                    {
                        if (!_context.IsSrcdoc) Error("missing-doctype");
                        if (!_context.CannotChangeMode && !_context.IsSrcdoc) _document.SetParserMode(DocumentMode.Quirks);
                        _mode = Mode.BeforeHtml;
                    }
                    else if (_mode == Mode.BeforeHtml)
                    {
                        InsertElement("html", parentOverride: _document);
                        _mode = Mode.BeforeHead;
                    }
                    else
                    {
                        _head = InsertElement("head");
                        _mode = Mode.InHead;
                    }
                    continue;
                case Mode.InHead:
                case Mode.InHeadNoscript:
                    if (White(c)) { AppendCharacterRun(data, whiteOnly: true); continue; }
                    if (_mode == Mode.InHeadNoscript) { Error("unexpected-token-in-head-noscript"); Pop(); _mode = Mode.InHead; }
                    else { Pop(); _mode = Mode.AfterHead; }
                    continue;
                case Mode.AfterHead:
                    if (White(c)) { AppendCharacterRun(data, whiteOnly: true); continue; }
                    InsertElement("body"); _framesetOk = true; _mode = Mode.InBody;
                    continue;
                case Mode.InBody:
                case Mode.InCaption:
                case Mode.InCell:
                    if (c == '\0') { Error("unexpected-null-character"); _textIndex++; Charge(1); continue; }
                    AppendCharacterRun(data, whiteOnly: White(c));
                    if (!White(c)) _framesetOk = false;
                    continue;
                case Mode.InTable:
                case Mode.InTableBody:
                case Mode.InRow:
                    if (Current.NamespaceUri == Namespaces.Html &&
                        Current.LocalName is "table" or "tbody" or "template" or "tfoot" or "thead" or "tr")
                    {
                        EnterTableText();
                        continue;
                    }
                    // The other table character branch uses the in-body rules
                    // with foster parenting for this character token.
                    if (c == '\0') { Error("unexpected-null-character"); _textIndex++; Charge(1); continue; }
                    _fosterParenting = true;
                    AppendCharacterRun(data, whiteOnly: White(c));
                    if (!White(c)) _framesetOk = false;
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
                    if (White(c)) { AppendCharacterRun(data, whiteOnly: true); continue; }
                    Error("unexpected-token-after-body"); _mode = Mode.InBody;
                    continue;
                default:
                    throw new InvalidOperationException("Unknown insertion mode for character token.");
            }
        }
    }

    private void AppendCharacterRun(string data, bool whiteOnly, bool textMode = false)
    {
        var start = _textIndex;
        var max = (int) Math.Min(data.Length, start + Math.Max(1, Math.Min(_remaining, 2048)));
        while (_textIndex < max)
        {
            var c = data[_textIndex];
            if (c == '\0' && !textMode || !textMode && White(c) != whiteOnly) break;
            _textIndex++;
        }
        InsertText(data.AsSpan(start, _textIndex - start));
    }
}
