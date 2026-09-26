using System;
using System.Collections.Generic;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6.4.4 and §13.2.6.4.16 (2026-09-22).
    // One entry belongs to each template on the open-element stack. H7b also
    // makes its fragment context part of IsParsingTemplateContents.
    private readonly List<Mode> _templateModes = [];
    private bool _templateHasFor;
    private bool _templateOrdinaryShadowFallback;

    private bool IsParsingTemplateContents => Last("template") >= 0 || _fragmentContext is { NamespaceUri: Namespaces.Html, LocalName: "template" };

    private void StartTemplate()
    {
        // The document parser's allow-declarative-shadow-roots flag is false.
        // A valid shadowrootmode therefore takes the specified ordinary return
        // branch, even if this token also spells a `for` attribute.
        if (_templateHasFor && !_templateOrdinaryShadowFallback)
        {
            Missing(HtmlMissingFeature.Templates);
            return;
        }

        PushFormattingMarker();
        _framesetOk = false;
        _mode = Mode.InTemplate;
        _templateModes.Add(Mode.InTemplate);
        Charge(1);
        InsertTokenElement();
    }

    private bool EndTemplate()
    {
        var template = Last("template");
        if (template < 0)
        {
            Error("unexpected-template-end-tag");
            return true;
        }

        if (!TryGenerateAllImpliedEndTagsThoroughly()) return false;
        if (!IsHtmlElement(Current, "template")) Error("misnested-template-end-tag");
        ScheduleTemplatePop(template, reprocess: false);
        return true;
    }

    private void ScheduleTemplatePop(int template, bool reprocess)
    {
        SchedulePopTo(template, reprocess, clearFormatting: true, resetMode: true);
        _popTemplateModeAfterPop = true;
    }

    private bool TryGenerateAllImpliedEndTagsThoroughly()
    {
        var popped = false;
        while (_open.Count > 0 && Current.NamespaceUri == Namespaces.Html &&
               Current.LocalName is "caption" or "colgroup" or "dd" or "dt" or "li" or "optgroup" or
                   "option" or "p" or "rb" or "rp" or "rt" or "rtc" or "tbody" or "td" or "tfoot" or
                   "th" or "thead" or "tr")
        {
            if (_remaining <= 0 && popped) return false;
            Pop();
            popped = true;
        }
        return true;
    }

    private bool InTemplate()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment:
            case HtmlTokenKind.ProcessingInstruction:
            case HtmlTokenKind.Doctype:
                return InBody();
            case HtmlTokenKind.StartTag when name is "base" or "basefont" or "bgsound" or "link" or
                "meta" or "noframes" or "script" or "style" or "template" or "title":
            case HtmlTokenKind.EndTag when name == "template":
                return InHead();
            case HtmlTokenKind.StartTag:
                var next = name switch
                {
                    "caption" or "colgroup" or "tbody" or "tfoot" or "thead" => Mode.InTable,
                    "col" => Mode.InColumnGroup,
                    "tr" => Mode.InTableBody,
                    "td" or "th" => Mode.InRow,
                    _ => Mode.InBody
                };
                _templateModes[^1] = next;
                _mode = next;
                Charge(1);
                return true;
            case HtmlTokenKind.EndTag:
                Error("unexpected-template-end-tag");
                return false;
            case HtmlTokenKind.EndOfFile:
                var template = Last("template");
                if (template < 0) return false;
                Error("eof-in-template");
                ScheduleTemplatePop(template, reprocess: true);
                return false;
            default:
                throw new InvalidOperationException("Unexpected token kind in template mode.");
        }
    }
}
