using System;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6.4.7 (2026-09-22).
    private bool InBody()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment: InsertComment(); return false;
            case HtmlTokenKind.ProcessingInstruction: InsertProcessingInstruction(); return false;
            case HtmlTokenKind.Doctype: Error("unexpected-doctype"); return false;
            case HtmlTokenKind.EndOfFile:
                if (_unexpectedOpenCount != 0) Error("eof-with-open-elements");
                return false;
            case HtmlTokenKind.StartTag: return BodyStart(name!);
            case HtmlTokenKind.EndTag: return BodyEnd(name!);
            default: throw new InvalidOperationException("Unexpected token kind in body.");
        }
    }

    private bool BodyStart(string name)
    {
        if (name == "html")
        {
            Error("unexpected-html-start-tag");
            MergeAttributes(_open[0]);
            return false;
        }
        if (name is "base" or "basefont" or "bgsound" or "link" or "meta" or "noframes" or
            "script" or "style" or "template" or "title")
        {
            InHead();
            return false;
        }
        if (name == "body")
        {
            Error("unexpected-body-start-tag");
            if (_open.Count > 1 && _open[1].LocalName == "body")
            {
                _framesetOk = false;
                MergeAttributes(_open[1]);
            }
            return false;
        }
        if (name == "frameset")
        {
            Error("unexpected-frameset-start-tag");
            if (_framesetOk && _open.Count > 1 && _open[1].LocalName == "body")
                Missing(HtmlMissingFeature.Framesets);
            return false;
        }
        if (IsBlockStart(name))
        {
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            InsertTokenElement();
            return false;
        }
        if (IsHeading(name))
        {
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            if (IsHeading(Current.LocalName)) { Error("nested-heading"); Pop(); }
            InsertTokenElement();
            return false;
        }
        if (name is "pre" or "listing")
        {
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            InsertTokenElement();
            _ignoreNextLf = true;
            _framesetOk = false;
            return false;
        }
        if (name == "form")
        {
            if (_form is not null) { Error("nested-form"); return false; }
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            _form = InsertTokenElement();
            return false;
        }
        if (name == "li")
        {
            _framesetOk = false;
            var li = Last("li");
            if (li >= LastLiStop && li >= 0)
            {
                if (!TryGenerateImpliedEndTags("li")) return true;
                if (Current.LocalName != "li") Error("misnested-li-start-tag");
                SchedulePopTo(li, reprocess: true);
                return true;
            }
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            InsertTokenElement();
            return false;
        }
        if (name is "dd" or "dt")
        {
            _framesetOk = false;
            var dd = Last("dd");
            var dt = Last("dt");
            var target = Math.Max(dd, dt);
            if (target >= LastDdDtStop && target >= 0)
            {
                if (!TryGenerateImpliedEndTags(_open[target].LocalName)) return true;
                if (!ReferenceEquals(Current, _open[target])) Error("misnested-description-start-tag");
                SchedulePopTo(target, reprocess: true);
                return true;
            }
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            InsertTokenElement();
            return false;
        }
        if (name == "plaintext")
        {
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            InsertTokenElement();
            _tokenizer.SetTextMode(HtmlTextMode.PlainText, null);
            return false;
        }
        if (name == "button")
        {
            if (InScope("button"))
            {
                if (!TryGenerateImpliedEndTags()) return true;
                Error("nested-button");
                SchedulePopTo(Last("button"), reprocess: true);
                return true;
            }
            InsertTokenElement();
            _framesetOk = false;
            return false;
        }
        if (name == "table")
        {
            if (_document.Mode != DocumentMode.Quirks && InButtonScope("p")) { CloseP(reprocess: true); return true; }
            InsertTokenElement(); _framesetOk = false; _mode = Mode.InTable;
            return false;
        }
        if (IsFormatting(name) || name is "applet" or "marquee" or "object")
        {
            Missing(HtmlMissingFeature.Formatting); return false;
        }
        if (name is "area" or "br" or "embed" or "img" or "keygen" or "wbr")
        {
            InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true; _framesetOk = false;
            return false;
        }
        if (name == "input")
        {
            var inspected = false;
            while (_inputAttributeIndex < _token.Attributes.Count && !_inputTypeFound)
            {
                if (_remaining <= 0 && inspected) break;
                var attribute = _token.Attributes[_inputAttributeIndex++];
                Charge(1);
                inspected = true;
                if (attribute.Name != "type") continue;
                _inputTypeFound = true;
                _inputTypeHidden = string.Equals(attribute.Value, "hidden", StringComparison.OrdinalIgnoreCase);
            }
            if (_inputAttributeIndex < _token.Attributes.Count && !_inputTypeFound) return true;
            if (inspected && _remaining <= 0) return true;
            InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true;
            if (!_inputTypeHidden) _framesetOk = false;
            return false;
        }
        if (name is "param" or "source" or "track")
        {
            InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true; return false;
        }
        if (name == "hr")
        {
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true; _framesetOk = false;
            return false;
        }
        if (name == "image")
        {
            Error("image-rewritten-to-img");
            _token = new HtmlToken(HtmlTokenKind.StartTag, name: "img", attributes: _token.Attributes,
                selfClosing: _token.SelfClosing, offset: _token.Offset);
            return true;
        }
        if (name == "textarea")
        {
            EnterText(HtmlTextMode.RcData, name);
            _ignoreNextLf = true;
            _framesetOk = false;
            return false;
        }
        if (name == "xmp")
        {
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            _framesetOk = false;
            EnterText(HtmlTextMode.RawText, name);
            return false;
        }
        if (name == "iframe")
        {
            _framesetOk = false;
            EnterText(HtmlTextMode.RawText, name);
            return false;
        }
        if (name == "noembed" || name == "noscript" && _scriptingEnabled)
        {
            EnterText(HtmlTextMode.RawText, name);
            return false;
        }
        if (name is "select" or "option" or "optgroup")
        {
            Missing(HtmlMissingFeature.Select); return false;
        }
        if (name is "rb" or "rtc" or "rp" or "rt")
        {
            if (InScope("ruby"))
            {
                if (!TryGenerateImpliedEndTags(name is "rp" or "rt" ? "rtc" : null)) return true;
                if (name is "rb" or "rtc" ? Current.LocalName != "ruby" : Current.LocalName is not ("ruby" or "rtc"))
                    Error("misnested-ruby-start-tag");
            }
            InsertTokenElement();
            return false;
        }
        if (name is "math" or "svg") { Missing(HtmlMissingFeature.ForeignContent); return false; }
        if (name is "caption" or "col" or "colgroup" or "frame" or "head" or "tbody" or "td" or "tfoot" or "th" or "thead" or "tr")
        {
            Error("unexpected-start-tag"); return false;
        }
        InsertTokenElement();
        return false;
    }

    private bool BodyEnd(string name)
    {
        if (name == "template") { Missing(HtmlMissingFeature.Templates); return false; }
        if (name == "table")
        {
            if (!InTableScope("table")) { Error("unexpected-table-end-tag"); return false; }
            SchedulePopTo(Last("table"), reprocess: false, resetMode: true);
            return false;
        }
        if (name is "body" or "html")
        {
            if (!InScope("body")) { Error("unexpected-end-tag"); return false; }
            if (_unexpectedOpenCount != 0) Error("misnested-body-end-tag");
            _mode = Mode.AfterBody;
            return name == "html";
        }
        if (IsBlockEnd(name) || name == "button")
        {
            if (!InScope(name)) { Error("unexpected-end-tag"); return false; }
            if (!TryGenerateImpliedEndTags()) return true;
            if (Current.LocalName != name) Error("misnested-end-tag");
            SchedulePopTo(Last(name), reprocess: false);
            return false;
        }
        if (name == "form")
        {
            var form = _form;
            if (form is null || !InScope("form")) { _form = null; Error("unexpected-form-end-tag"); return false; }
            if (!TryGenerateImpliedEndTags()) return true;
            _form = null;
            if (!ReferenceEquals(Current, form)) Error("misnested-form-end-tag");
            RemoveOpenAt(Last("form"));
            return false;
        }
        if (name == "p")
        {
            if (!InButtonScope("p")) { Error("unexpected-p-end-tag"); InsertElement("p"); }
            if (!CloseP(reprocess: false)) return true;
            return false;
        }
        if (name == "li")
        {
            if (!InListItemScope("li")) { Error("unexpected-li-end-tag"); return false; }
            if (!TryGenerateImpliedEndTags("li")) return true;
            if (Current.LocalName != "li") Error("misnested-li-end-tag");
            SchedulePopTo(Last("li"), reprocess: false);
            return false;
        }
        if (name is "dd" or "dt")
        {
            if (!InScope(name)) { Error("unexpected-end-tag"); return false; }
            if (!TryGenerateImpliedEndTags(name)) return true;
            if (Current.LocalName != name) Error("misnested-end-tag");
            SchedulePopTo(Last(name), reprocess: false);
            return false;
        }
        if (IsHeading(name))
        {
            var index = -1;
            for (var i = 1; i <= 6; i++) index = Math.Max(index, Last("h" + i));
            if (index < 0 || index < LastScopeStop) { Error("unexpected-heading-end-tag"); return false; }
            if (!TryGenerateImpliedEndTags()) return true;
            if (Current.LocalName != name) Error("misnested-heading-end-tag");
            SchedulePopTo(index, reprocess: false);
            return false;
        }
        if (IsFormatting(name))
        {
            Missing(HtmlMissingFeature.Formatting); return false;
        }
        if (name is "applet" or "marquee" or "object")
        {
            Error("unexpected-end-tag"); return false;
        }
        if (name == "br")
        {
            Error("unexpected-br-end-tag");
            InsertElement("br"); Pop(); _framesetOk = false;
            return false;
        }
        var target = Last(name);
        if (target < 0 || target < LastSpecial)
        {
            Error("unexpected-end-tag");
            return false;
        }
        if (!TryGenerateImpliedEndTags(name)) return true;
        if (Current.LocalName != name) Error("misnested-end-tag");
        SchedulePopTo(target, reprocess: false);
        return false;
    }

    private void RemoveOpenAt(int index)
    {
        if (index == _open.Count - 1)
        {
            Pop();
            return;
        }
        var removed = _open[index];
        if (!AllowedOpenAtEof(removed.LocalName)) _unexpectedOpenCount--;
        RemoveIndexes(removed, index);
        Charge(1);
        _pendingShiftIndex = index;
    }

    private static bool IsFormatting(string name) => name is "a" or "b" or "big" or "code" or "em" or
        "font" or "i" or "nobr" or "s" or "small" or "strike" or "strong" or "tt" or "u";

    private static bool IsBlockStart(string name) => name is "address" or "article" or "aside" or
        "blockquote" or "center" or "details" or "dialog" or "dir" or "div" or "dl" or "fieldset" or
        "figcaption" or "figure" or "footer" or "header" or "hgroup" or "main" or "menu" or "nav" or
        "ol" or "p" or "search" or "section" or "summary" or "ul";

    private static bool IsBlockEnd(string name) => name is "address" or "article" or "aside" or
        "blockquote" or "center" or "details" or "dialog" or "dir" or "div" or "dl" or "fieldset" or
        "figcaption" or "figure" or "footer" or "header" or "hgroup" or "listing" or "main" or "menu" or
        "nav" or "ol" or "pre" or "search" or "section" or "summary" or "ul";
}
