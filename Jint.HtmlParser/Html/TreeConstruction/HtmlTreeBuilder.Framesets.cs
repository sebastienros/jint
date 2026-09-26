using System;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6.4.7, .18, .19 and .21 (2026-09-25).
    // The body is removed through the native DOM operation. Its subtree walk is
    // retained across quotas so removal of a large, otherwise frameset-ok body
    // does not hide unbounded work behind one tree-builder dispatch.
    private bool AdvanceFramesetReplacement()
    {
        if (_framesetReplacementStage == 1)
        {
            var body = _open[1];
            while (_framesetScanNode is { } node && _remaining > 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (_framesetScanUnwinding)
                {
                    if (ReferenceEquals(node, body)) _framesetScanNode = null;
                    else if (node.NextSibling is { } sibling)
                    {
                        _framesetScanNode = sibling;
                        _framesetScanUnwinding = false;
                    }
                    else _framesetScanNode = node.ParentNode;
                }
                else if (node.FirstChild is { } child) _framesetScanNode = child;
                else if (!ReferenceEquals(node, body) && node.NextSibling is { } sibling)
                    _framesetScanNode = sibling;
                else if (!ReferenceEquals(node, body))
                {
                    _framesetScanNode = node.ParentNode;
                    _framesetScanUnwinding = true;
                }
                else _framesetScanNode = null;
                Charge(1);
            }
            if (_framesetScanNode is not null || _remaining <= 0) return false;
            _framesetReplacementStage = 2;
        }

        if (_framesetReplacementStage == 2)
        {
            if (_remaining <= 0) return false;
            var body = _open[1];
            body.ParentNode?.RemoveChild(body);
            Charge(1);
            _framesetReplacementStage = 3;
        }

        while (_open.Count > 1 && _remaining > 0) Pop();
        if (_open.Count > 1 || _remaining <= 0) return false;
        InsertTokenElement();
        _mode = Mode.InFrameset;
        _framesetReplacementStage = 0;
        return true;
    }

    private bool InFrameset()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment: InsertComment(); return false;
            case HtmlTokenKind.ProcessingInstruction: InsertProcessingInstruction(); return false;
            case HtmlTokenKind.Doctype: Error("unexpected-doctype"); return false;
            case HtmlTokenKind.StartTag when name == "html": InBody(); return false;
            case HtmlTokenKind.StartTag when name == "frameset": InsertTokenElement(); return false;
            case HtmlTokenKind.EndTag when name == "frameset":
                if (IsHtmlElement(Current, "html"))
                {
                    Error("unexpected-frameset-end-tag");
                    return false;
                }
                Pop();
                if (!IsHtmlElement(Current, "frameset")) _mode = Mode.AfterFrameset;
                return false;
            case HtmlTokenKind.StartTag when name == "frame":
                InsertTokenElement(); Pop(); _acknowledgedSelfClosing = true; return false;
            case HtmlTokenKind.StartTag when name == "noframes": InHead(); return false;
            case HtmlTokenKind.EndOfFile:
                if (!IsHtmlElement(Current, "html")) Error("eof-in-frameset");
                return false;
            default:
                Error("unexpected-token-in-frameset"); return false;
        }
    }

    private bool InAfterFrameset()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment: InsertComment(); return false;
            case HtmlTokenKind.ProcessingInstruction: InsertProcessingInstruction(); return false;
            case HtmlTokenKind.Doctype: Error("unexpected-doctype"); return false;
            case HtmlTokenKind.StartTag when name == "html": InBody(); return false;
            case HtmlTokenKind.EndTag when name == "html": _mode = Mode.AfterAfterFrameset; return false;
            case HtmlTokenKind.StartTag when name == "noframes": InHead(); return false;
            case HtmlTokenKind.EndOfFile: return false;
            default:
                Error("unexpected-token-after-frameset"); return false;
        }
    }

    private bool InAfterAfterFrameset()
    {
        var name = _token.Name;
        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment: InsertComment(_document); return false;
            case HtmlTokenKind.ProcessingInstruction: InsertProcessingInstruction(_document); return false;
            case HtmlTokenKind.Doctype: Error("unexpected-doctype"); return false;
            case HtmlTokenKind.StartTag when name == "html": InBody(); return false;
            case HtmlTokenKind.StartTag when name == "noframes": InHead(); return false;
            case HtmlTokenKind.EndOfFile: return false;
            default:
                Error("unexpected-token-after-after-frameset"); return false;
        }
    }
}
