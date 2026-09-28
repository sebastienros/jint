using System;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6.4.9–15 (2026-09-22).
    private bool InTable()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment: InsertComment(); return false;
            case HtmlTokenKind.ProcessingInstruction: InsertProcessingInstruction(); return false;
            case HtmlTokenKind.Doctype: Error("unexpected-doctype"); return false;
            case HtmlTokenKind.EndOfFile: return InBody();
            case HtmlTokenKind.StartTag:
                switch (HtmlTableStartLookup.Match(name))
                {
                    case HtmlTableStartKind.Caption:
                        if (!TryClearToTableContext()) return true;
                        PushFormattingMarker();
                        InsertTokenElement();
                        _mode = Mode.InCaption;
                        return false;
                    case HtmlTableStartKind.Colgroup:
                        if (!TryClearToTableContext()) return true;
                        InsertTokenElement();
                        _mode = Mode.InColumnGroup;
                        return false;
                    case HtmlTableStartKind.Col:
                        if (!TryClearToTableContext()) return true;
                        InsertElement("colgroup");
                        _mode = Mode.InColumnGroup;
                        return true;
                    case HtmlTableStartKind.Tbody or HtmlTableStartKind.Tfoot or HtmlTableStartKind.Thead:
                        if (!TryClearToTableContext()) return true;
                        InsertTokenElement();
                        _mode = Mode.InTableBody;
                        return false;
                    case HtmlTableStartKind.Td or HtmlTableStartKind.Th or HtmlTableStartKind.Tr:
                        if (!TryClearToTableContext()) return true;
                        InsertElement("tbody");
                        _mode = Mode.InTableBody;
                        return true;
                    case HtmlTableStartKind.Table:
                        Error("nested-table-start-tag");
                        if (InTableScope("table")) SchedulePopTo(Last("table"), reprocess: true, resetMode: true);
                        return false;
                    case HtmlTableStartKind.Style or HtmlTableStartKind.Script or HtmlTableStartKind.Template: return InHead();
                    case HtmlTableStartKind.Input:
                        if (!InspectInputType()) return true;
                        if (!_inputTypeHidden) break;
                        Error("hidden-input-in-table");
                        InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true;
                        return false;
                    case HtmlTableStartKind.Form:
                        Error("form-in-table");
                        if (_form is not null && !IsParsingTemplateContents) return false;
                        var tableForm = InsertTokenElement();
                        if (!IsParsingTemplateContents) _form = tableForm;
                        Pop();
                        return false;
                }
                break;
            case HtmlTokenKind.EndTag:
                switch (HtmlTableEndLookup.Match(name))
                {
                    case HtmlTableEndKind.Table:
                        if (!InTableScope("table")) { Error("unexpected-table-end-tag"); return false; }
                        SchedulePopTo(Last("table"), reprocess: false, resetMode: true);
                        return false;
                    case HtmlTableEndKind.Body or HtmlTableEndKind.Caption or HtmlTableEndKind.Col or HtmlTableEndKind.Colgroup or HtmlTableEndKind.Html or HtmlTableEndKind.Tbody or HtmlTableEndKind.Td or
                        HtmlTableEndKind.Tfoot or HtmlTableEndKind.Th or HtmlTableEndKind.Thead or HtmlTableEndKind.Tr:
                        Error("unexpected-end-tag"); return false;
                    case HtmlTableEndKind.Template: return InHead();
                }
                break;
        }
        Error("unexpected-token-in-table");
        _fosterParenting = true;
        _delegateToBody = true;
        _delegatedFromMode = _mode;
        return InBody();
    }

    private bool InCaption()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.EndTag when name == "caption":
            case HtmlTokenKind.StartTag when HtmlCaptionColColgroupTbodyNames.Match(name):
            case HtmlTokenKind.EndTag when name == "table":
                {
                    if (!InTableScope("caption")) { Error("unexpected-caption-end-tag"); return false; }
                    if (!TryGenerateImpliedEndTags()) return true;
                    if (!IsHtmlElement(Current, "caption")) Error("misnested-caption-end-tag");
                    var reprocess = _token.Kind != HtmlTokenKind.EndTag || name != "caption";
                    SchedulePopTo(Last("caption"), reprocess, Mode.InTable, clearFormatting: true);
                    return false;
                }
            case HtmlTokenKind.EndTag when HtmlBodyColColgroupNames.Match(name):
                {
                    Error("unexpected-end-tag"); return false;
                }
        }
        return InBody();
    }

    private bool InColumnGroup()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment: InsertComment(); return false;
            case HtmlTokenKind.ProcessingInstruction: InsertProcessingInstruction(); return false;
            case HtmlTokenKind.Doctype: Error("unexpected-doctype"); return false;
            case HtmlTokenKind.StartTag when name == "html": return InBody();
            case HtmlTokenKind.StartTag when name == "col":
                InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true; return false;
            case HtmlTokenKind.EndTag when name == "colgroup":
                if (!IsHtmlElement(Current, "colgroup")) { Error("unexpected-colgroup-end-tag"); return false; }
                Pop(); _mode = Mode.InTable; return false;
            case HtmlTokenKind.EndTag when name == "col": Error("unexpected-end-tag"); return false;
            case HtmlTokenKind.StartTag when name == "template": return InHead();
            case HtmlTokenKind.EndTag when name == "template": return InHead();
            case HtmlTokenKind.EndOfFile: return InBody();
        }
        if (!IsHtmlElement(Current, "colgroup")) { Error("unexpected-column-group-token"); return false; }
        Pop(); _mode = Mode.InTable;
        return true;
    }

    private bool InTableBody()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.StartTag when name == "tr":
                {
                    if (!TryClearToTableBodyContext()) return true;
                    InsertTokenElement(); _mode = Mode.InRow; return false;
                }
            case HtmlTokenKind.StartTag when HtmlTdThNames.Match(name):
                {
                    if (!TryClearToTableBodyContext()) return true;
                    Error("cell-without-row");
                    InsertElement("tr"); _mode = Mode.InRow; return true;
                }
            case HtmlTokenKind.EndTag when HtmlTbodyTfootTheadNames.Match(name):
                {
                    if (!InTableScope(name!)) { Error("unexpected-table-body-end-tag"); return false; }
                    if (!TryClearToTableBodyContext()) return true;
                    Pop(); _mode = Mode.InTable; return false;
                }
            case HtmlTokenKind.StartTag when HtmlCaptionColColgroupTbodyTfootNames.Match(name):
            case HtmlTokenKind.EndTag when name == "table":
                {
                    var target = LastTableBodyInScope();
                    if (target < 0) { Error("unexpected-table-body-token"); return false; }
                    if (!TryClearToTableBodyContext()) return true;
                    Pop(); _mode = Mode.InTable; return true;
                }
            case HtmlTokenKind.EndTag when HtmlBodyCaptionColNames.Match(name):
                {
                    Error("unexpected-end-tag"); return false;
                }
        }
        return InTable();
    }

    private bool InRow()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.StartTag when HtmlTdThNames.Match(name):
                {
                    if (!TryClearToRowContext()) return true;
                    InsertTokenElement(); _mode = Mode.InCell; PushFormattingMarker(); return false;
                }
            case HtmlTokenKind.EndTag when name == "tr":
                {
                    if (!InTableScope("tr")) { Error("unexpected-row-end-tag"); return false; }
                    if (!TryClearToRowContext()) return true;
                    Pop(); _mode = Mode.InTableBody; return false;
                }
            case HtmlTokenKind.StartTag when HtmlCaptionColColgroupTbodyTfootTheadNames.Match(name):
            case HtmlTokenKind.EndTag when name == "table":
                {
                    if (!InTableScope("tr")) { Error("unexpected-row-token"); return false; }
                    if (!TryClearToRowContext()) return true;
                    Pop(); _mode = Mode.InTableBody; return true;
                }
            case HtmlTokenKind.EndTag when HtmlTbodyTfootTheadNames.Match(name):
                {
                    if (!InTableScope(name!)) { Error("unexpected-table-body-end-tag"); return false; }
                    if (!InTableScope("tr")) return false;
                    if (!TryClearToRowContext()) return true;
                    Pop(); _mode = Mode.InTableBody; return true;
                }
            case HtmlTokenKind.EndTag when HtmlBodyCaptionColColgroupNames.Match(name):
                {
                    Error("unexpected-end-tag"); return false;
                }
        }
        return InTable();
    }

    private bool InCell()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.EndTag when HtmlTdThNames.Match(name):
                {
                    if (!InTableScope(name!)) { Error("unexpected-cell-end-tag"); return false; }
                    return CloseCell(name!, reprocess: false);
                }
            case HtmlTokenKind.StartTag when HtmlCaptionColColgroupTbodyNames.Match(name):
            case HtmlTokenKind.EndTag when HtmlTableTbodyTfootNames.Match(name):
                {
                    if (_token.Kind == HtmlTokenKind.EndTag && !InTableScope(name!))
                    {
                        Error("unexpected-table-end-tag"); return false;
                    }
                    var cell = LastCellInTableScope();
                    if (cell < 0) throw new InvalidOperationException("In-cell mode lost its cell.");
                    return CloseCell(_open[cell].LocalName, reprocess: true);
                }
            case HtmlTokenKind.EndTag when HtmlBodyCaptionColColgroupHtmlNames.Match(name):
                {
                    Error("unexpected-end-tag"); return false;
                }
        }
        return InBody();
    }

    private bool CloseCell(string name, bool reprocess)
    {
        if (!TryGenerateImpliedEndTags()) return true;
        if (!IsHtmlElement(Current, name)) Error("misnested-cell-end-tag");
        SchedulePopTo(Last(name), reprocess, Mode.InRow, clearFormatting: true);
        return false;
    }

    private bool TryClearToTableContext() => TryClearContext(Mode.InTable);
    private bool TryClearToTableBodyContext() => TryClearContext(Mode.InTableBody);
    private bool TryClearToRowContext() => TryClearContext(Mode.InRow);

    private bool TryClearContext(Mode context)
    {
        var popped = false;
        while (!IsHtmlElement(Current, "template") && !IsHtmlElement(Current, "html") &&
            !(context == Mode.InTable && IsHtmlElement(Current, "table")) &&
            !(context == Mode.InTableBody &&
                (IsHtmlElement(Current, "tbody") || IsHtmlElement(Current, "tfoot") || IsHtmlElement(Current, "thead"))) &&
            !(context == Mode.InRow && IsHtmlElement(Current, "tr")))
        {
            if (_remaining <= 0 && popped) return false;
            Pop();
            popped = true;
        }
        return true;
    }

    private bool InspectInputType()
    {
        var inspected = false;
        while (_inputAttributeIndex < _token.Attributes.Count && !_inputTypeFound)
        {
            if (_remaining <= 0 && inspected) return false;
            var attribute = _token.Attributes[_inputAttributeIndex++];
            Charge(1);
            inspected = true;
            if (attribute.Name != "type") continue;
            _inputTypeFound = true;
            _inputTypeHidden = attribute.ValueSlice.Span.Equals("hidden", StringComparison.OrdinalIgnoreCase);
        }
        return !inspected || _remaining > 0;
    }

    private int LastTableBodyInScope()
    {
        var index = Math.Max(Last("tbody"), Math.Max(Last("thead"), Last("tfoot")));
        return index >= Math.Max(Last("html"), Math.Max(Last("table"), Last("template"))) ? index : -1;
    }

    private int LastCellInTableScope()
    {
        var index = Math.Max(Last("td"), Last("th"));
        return index >= Math.Max(Last("html"), Math.Max(Last("table"), Last("template"))) ? index : -1;
    }

    private void ResetInsertionMode()
    {
        if (_resetModeIndexes.Count == 0)
            throw new InvalidOperationException("HTML insertion-mode reset lost its root.");
        // The nearest applicable HTML element is maintained with the open stack.
        // Repeated table closures must not scan the same unchanged ancestors.
        var rootCase = _resetModeIndexes[^1] == 0 && _fragmentContext is not null;
        var element = rootCase ? _fragmentContext! : _open[_resetModeIndexes[^1]];
        if (rootCase && (element.NamespaceUri != Namespaces.Html || HtmlTdThHeadNames.Match(element.LocalName)))
        {
            _mode = Mode.InBody;
            Charge(1);
            return;
        }
        Charge(1);
        switch (HtmlResetModeLookup.Match(element.LocalName))
        {
            case HtmlResetModeKind.Td or HtmlResetModeKind.Th: _mode = Mode.InCell; break;
            case HtmlResetModeKind.Tr: _mode = Mode.InRow; break;
            case HtmlResetModeKind.Tbody or HtmlResetModeKind.Thead or HtmlResetModeKind.Tfoot: _mode = Mode.InTableBody; break;
            case HtmlResetModeKind.Caption: _mode = Mode.InCaption; break;
            case HtmlResetModeKind.Colgroup: _mode = Mode.InColumnGroup; break;
            case HtmlResetModeKind.Table: _mode = Mode.InTable; break;
            case HtmlResetModeKind.Template:
                _mode = _templateModes.Count > 0 ? _templateModes[^1] :
                    throw new InvalidOperationException("Template insertion mode stack is empty.");
                break;
            case HtmlResetModeKind.Head: _mode = Mode.InHead; break;
            case HtmlResetModeKind.Body: _mode = Mode.InBody; break;
            case HtmlResetModeKind.Frameset: _mode = Mode.InFrameset; break;
            case HtmlResetModeKind.Html: _mode = _head is null ? Mode.BeforeHead : Mode.AfterHead; break;
            default:
                if (!rootCase) throw new InvalidOperationException("Unknown HTML insertion-mode reset element.");
                _mode = Mode.InBody;
                break;
        }
    }
}
