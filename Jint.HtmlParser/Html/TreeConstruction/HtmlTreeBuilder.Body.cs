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
                if (IsParsingTemplateContents) return InTemplate();
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
            if (!IsParsingTemplateContents) MergeAttributes(_open[0]);
            return false;
        }
        if (HtmlBaseBasefontBgsoundNames.Match(name))
        {
            InHead();
            return false;
        }
        if (name == "body")
        {
            Error("unexpected-body-start-tag");
            if (!IsParsingTemplateContents && _open.Count > 1 && IsHtmlElement(_open[1], "body"))
            {
                _framesetOk = false;
                MergeAttributes(_open[1]);
            }
            return false;
        }
        if (name == "frameset")
        {
            Error("unexpected-frameset-start-tag");
            if (_framesetOk && _open.Count > 1 && IsHtmlElement(_open[1], "body"))
            {
                _framesetReplacementStage = 1;
                _framesetScanNode = _open[1];
                return true;
            }
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
            if (Current.NamespaceUri == Namespaces.Html && IsHeading(Current.LocalName)) { Error("nested-heading"); Pop(); }
            InsertTokenElement();
            return false;
        }
        if (HtmlPreListingNames.Match(name))
        {
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            InsertTokenElement();
            _ignoreNextLf = true;
            _framesetOk = false;
            return false;
        }
        if (name == "form")
        {
            if (_form is not null && !IsParsingTemplateContents) { Error("nested-form"); return false; }
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            var form = InsertTokenElement();
            if (!IsParsingTemplateContents) _form = form;
            return false;
        }
        if (name == "li")
        {
            _framesetOk = false;
            var li = Last("li");
            if (li >= LastLiStop && li >= 0)
            {
                if (!TryGenerateImpliedEndTags("li")) return true;
                if (!IsHtmlElement(Current, "li")) Error("misnested-li-start-tag");
                SchedulePopTo(li, reprocess: true);
                return true;
            }
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            InsertTokenElement();
            return false;
        }
        if (HtmlDdDtNames.Match(name))
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
            if (!TryReconstructFormatting()) return true;
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
        if (IsFormatting(name))
        {
            if (HtmlANobrNames.Match(name)) return !TrySpecialFormattingStart(name);
            if (_pendingFormattingElement is null)
            {
                if (!TryReconstructFormatting()) return true;
                _pendingFormattingElement = InsertTokenElement();
            }
            if (!TryAddFormattingElement()) return true;
            return false;
        }
        if (HtmlAppletMarqueeObjectNames.Match(name))
        {
            if (!TryReconstructFormatting()) return true;
            InsertTokenElement();
            PushFormattingMarker();
            _framesetOk = false;
            return false;
        }
        if (HtmlAreaBrEmbedNames.Match(name))
        {
            if (!TryReconstructFormatting()) return true;
            InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true; _framesetOk = false;
            return false;
        }
        if (name == "input")
        {
            // HTML Standard §13.2.6.4.7: the select fragment context is not
            // on the stack, so this guard must precede the open-select check.
            if (_fragmentContext is { NamespaceUri: Namespaces.Html, LocalName: "select" })
            {
                Error("input-in-select-fragment");
                return false;
            }
            // HTML Standard §13.2.6.4.7: an input closes an open select
            // before formatting is reconstructed and the input is inserted.
            if (InScope("select"))
            {
                Error("input-in-select");
                SchedulePopTo(Last("select"), reprocess: true);
                return true;
            }
            var inspected = false;
            while (_inputAttributeIndex < _token.Attributes.Length && !_inputTypeFound)
            {
                if (_remaining <= 0 && inspected) break;
                var attribute = _token.Attributes[_inputAttributeIndex++];
                Charge(1);
                inspected = true;
                if (attribute.Name != "type") continue;
                _inputTypeFound = true;
                _inputTypeHidden = attribute.ValueSlice.Span.Equals("hidden", StringComparison.OrdinalIgnoreCase);
            }
            if (_inputAttributeIndex < _token.Attributes.Length && !_inputTypeFound) return true;
            if (inspected && _remaining <= 0) return true;
            if (!TryReconstructFormatting()) return true;
            InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true;
            if (!_inputTypeHidden) _framesetOk = false;
            return false;
        }
        if (HtmlParamSourceTrackNames.Match(name))
        {
            InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true; return false;
        }
        if (name == "hr")
        {
            if (InButtonScope("p")) { CloseP(reprocess: true); return true; }
            if (InScope("select"))
            {
                if (!TryGenerateImpliedEndTags()) return true;
                if (InScope("option") || InScope("optgroup")) Error("hr-in-select-option");
            }
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
            if (!TryReconstructFormatting()) return true;
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
        if (HtmlSelectOptionOptgroupNames.Match(name)) return SelectStart(name);
        if (HtmlRbRtcRpNames.Match(name))
        {
            if (InScope("ruby"))
            {
                if (!TryGenerateImpliedEndTags(HtmlRpRtNames.Match(name) ? "rtc" : null)) return true;
                if (HtmlRbRtcNames.Match(name) ? !IsHtmlElement(Current, "ruby") : !IsHtmlElement(Current, "ruby") && !IsHtmlElement(Current, "rtc"))
                    Error("misnested-ruby-start-tag");
            }
            InsertTokenElement();
            return false;
        }
        if (HtmlMathSvgNames.Match(name))
        {
            if (!TryReconstructFormatting()) return true;
            if (!TryInsertForeignTokenElement(name == "math" ? Namespaces.MathMl : Namespaces.Svg)) return true;
            if (_token.SelfClosing) { Pop(); _acknowledgedSelfClosing = true; }
            return false;
        }
        if (HtmlCaptionColColgroupNames.Match(name))
        {
            Error("unexpected-start-tag"); return false;
        }
        if (!TryReconstructFormatting()) return true;
        InsertTokenElement();
        return false;
    }

    private bool BodyEnd(string name)
    {
        if (name == "template") return !EndTemplate();
        if (name == "table")
        {
            if (!InTableScope("table")) { Error("unexpected-table-end-tag"); return false; }
            SchedulePopTo(Last("table"), reprocess: false, resetMode: true);
            return false;
        }
        if (HtmlBodyHtmlNames.Match(name))
        {
            if (!InScope("body")) { Error("unexpected-end-tag"); return false; }
            if (_unexpectedOpenCount != 0) Error("misnested-body-end-tag");
            _mode = Mode.AfterBody;
            return name == "html";
        }
        if (IsBlockEnd(name) || HtmlButtonSelectNames.Match(name))
        {
            if (!InScope(name)) { Error("unexpected-end-tag"); return false; }
            if (!TryGenerateImpliedEndTags()) return true;
            if (!IsHtmlElement(Current, name)) Error("misnested-end-tag");
            SchedulePopTo(Last(name), reprocess: false);
            return false;
        }
        if (name == "form")
        {
            if (IsParsingTemplateContents)
            {
                if (!InScope("form")) { Error("unexpected-form-end-tag"); return false; }
                if (!TryGenerateImpliedEndTags()) return true;
                if (!IsHtmlElement(Current, "form")) Error("misnested-form-end-tag");
                SchedulePopTo(Last("form"), reprocess: false);
                return false;
            }
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
            if (!IsHtmlElement(Current, "li")) Error("misnested-li-end-tag");
            SchedulePopTo(Last("li"), reprocess: false);
            return false;
        }
        if (HtmlDdDtNames.Match(name))
        {
            if (!InScope(name)) { Error("unexpected-end-tag"); return false; }
            if (!TryGenerateImpliedEndTags(name)) return true;
            if (!IsHtmlElement(Current, name)) Error("misnested-end-tag");
            SchedulePopTo(Last(name), reprocess: false);
            return false;
        }
        if (IsHeading(name))
        {
            var index = -1;
            for (var i = 1; i <= 6; i++) index = Math.Max(index, Last("h" + i));
            if (index < 0 || index < LastScopeStop) { Error("unexpected-heading-end-tag"); return false; }
            if (!TryGenerateImpliedEndTags()) return true;
            if (!IsHtmlElement(Current, name)) Error("misnested-heading-end-tag");
            SchedulePopTo(index, reprocess: false);
            return false;
        }
        if (IsFormatting(name))
        {
            return !TryAdoptionAgency(name);
        }
        if (HtmlAppletMarqueeObjectNames.Match(name))
        {
            if (!InScope(name)) { Error("unexpected-end-tag"); return false; }
            if (!TryGenerateImpliedEndTags()) return true;
            if (!IsHtmlElement(Current, name)) Error("misnested-end-tag");
            SchedulePopTo(Last(name), reprocess: false, clearFormatting: true);
            return false;
        }
        if (name == "br")
        {
            if (!TryReconstructFormatting()) return true;
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
        if (!IsHtmlElement(Current, name)) Error("misnested-end-tag");
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
        _openIdentity.Remove(removed);
        if (!AllowedOpenAtEof(removed)) _unexpectedOpenCount--;
        RemoveIndexes(removed, index);
        Charge(1);
        _pendingShiftIndex = index;
    }

    private static bool IsFormatting(string name) => HtmlFormattingElementLookup.Match(name);

    private static bool IsBlockStart(string name) => HtmlBlockStartLookup.Match(name);

    private static bool IsBlockEnd(string name) => HtmlBlockEndLookup.Match(name);
}
