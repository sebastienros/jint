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
    private bool _templateHasValidShadowMode;
    private string? _templateForValue;
    private bool _templateDelegatesFocus;
    private bool _templateSerializable;
    private bool _templateClonable;
    private bool _templateManualSlotAssignment;
    private bool _templateKeepRegistryNull;

    private bool IsParsingTemplateContents => Last("template") >= 0 || _fragmentContext is { NamespaceUri: Namespaces.Html, LocalName: "template" };

    private void StartTemplate()
    {
        PushFormattingMarker();
        _framesetOk = false;
        _mode = Mode.InTemplate;
        _templateModes.Add(Mode.InTemplate);
        Charge(1);
        if (_templateHasValidShadowMode)
        {
            if (!_context.AllowDeclarativeShadowRoots || ReferenceEquals(AdjustedCurrent, _open[0]))
            { InsertTokenElement(); return; }
            StartDeclarativeShadowTemplate();
            return;
        }
        if (!_templateHasFor) { InsertTokenElement(); return; }
        var location = FindAdjustedInsertionLocation(_headInsertionOverride);
        var fallbackTarget = _headInsertionOverride ?? CurrentParent;
        var scope = location.Parent;
        if (scope is Element { TemplateContent: { } contents }) scope = contents;
        var template = InsertElement("template", _preparedAttributes, attributeWork: _preparedAttributeWork,
            isValue: _preparedIsValue, onlyAddToStack: true);
        _templateOperation = new TemplateOperation(template, scope, _templateForValue!, fallbackTarget);
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
        if (_open[template].TemplatePatchState is { } patch)
        {
            _templateOperation = new TemplateOperation(_open[template], patch, template);
            return true;
        }
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
               HtmlCaptionColgroupDdNames.Match(Current.LocalName))
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
            case HtmlTokenKind.StartTag when HtmlBaseBasefontBgsoundNames.Match(name):
            case HtmlTokenKind.EndTag when name == "template":
                return InHead();
            case HtmlTokenKind.StartTag:
                var next = HtmlTemplateModeLookup.Match(name) switch
                {
                    HtmlTemplateModeKind.Caption or HtmlTemplateModeKind.Colgroup or HtmlTemplateModeKind.Tbody or HtmlTemplateModeKind.Tfoot or HtmlTemplateModeKind.Thead => Mode.InTable,
                    HtmlTemplateModeKind.Col => Mode.InColumnGroup,
                    HtmlTemplateModeKind.Tr => Mode.InTableBody,
                    HtmlTemplateModeKind.Td or HtmlTemplateModeKind.Th => Mode.InRow,
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
