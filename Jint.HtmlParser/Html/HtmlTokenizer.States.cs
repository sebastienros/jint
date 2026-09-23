using System;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTokenizer
{
    private enum State
    {
        Data, TagOpen, EndTagOpen, TagName, BeforeAttributeName, AttributeName,
        AfterAttributeName, BeforeAttributeValue, DoubleQuotedValue, SingleQuotedValue,
        UnquotedValue, AfterQuotedValue, SelfClosing, MarkupDeclaration,
        PiOpen, PiTarget, PiAfterTarget, PiData, PiQuestionable,
        BogusComment, CommentStart, CommentStartDash, Comment, CommentLessThan,
        CommentLessThanBang, CommentLessThanBangDash, CommentLessThanBangDashDash,
        CommentEndDash, CommentEnd, CommentEndBang,
        Doctype, BeforeDoctypeName, DoctypeName, AfterDoctypeName,
        AfterPublicKeyword, BeforePublicIdentifier, PublicDouble, PublicSingle,
        AfterPublicIdentifier, BetweenPublicAndSystem, AfterSystemKeyword,
        BeforeSystemIdentifier, SystemDouble, SystemSingle, AfterSystemIdentifier,
        BogusDoctype, CData, CDataBracket, CDataEnd,
        CharacterReference, NamedReference, AmbiguousAmpersand, NumericReference, HexStart, DecimalStart,
        HexReference, DecimalReference
    }

    private bool Step(char c, out HtmlToken token)
    {
        token = default;
        switch (_state)
        {
            case State.Data:
                if (c == '&') { _referenceStart = _input.Offset; Take(); _returnState = State.Data; _state = State.CharacterReference; return false; }
                if (c == '<') { _tokenStart = _input.Offset; Take(); _state = State.TagOpen; return false; }
                if (c == '\0') Error("unexpected-null-character");
                var textOffset = _input.Offset;
                Text(Take(), textOffset);
                if (_text.Length >= 4096) { FlushText(out token); return true; }
                return false;

            case State.TagOpen:
                if (c == '!') { Take(); _state = State.MarkupDeclaration; return false; }
                if (c == '/') { Take(); _state = State.EndTagOpen; return false; }
                if (AsciiAlpha(c)) { BeginTag(false); _state = State.TagName; return false; }
                if (c == '?') { Take(); _piTarget.Clear(); _piData.Clear(); _piName = null; _state = State.PiOpen; return false; }
                Error("invalid-first-character-of-tag-name");
                Text('<', _tokenStart); _tokenStart = -1; _state = State.Data;
                return false;

            case State.EndTagOpen:
                if (AsciiAlpha(c)) { BeginTag(true); _state = State.TagName; return false; }
                if (c == '>') { Error("missing-end-tag-name"); Take(); _tokenStart = -1; _state = State.Data; return false; }
                Error("invalid-first-character-of-tag-name");
                _comment.Clear(); _state = State.BogusComment;
                return false;

            case State.TagName:
                if (White(c)) { Take(); _state = State.BeforeAttributeName; return false; }
                if (c == '/') { Take(); _state = State.SelfClosing; return false; }
                if (c == '>') { Take(); return EmitTag(out token); }
                if (c == '\0') Error("unexpected-null-character");
                Append(_tagName, Lower(ReplaceNull(Take()))); return false;

            case State.BeforeAttributeName:
                if (White(c)) { Take(); return false; }
                if (c == '/' || c == '>') { _state = State.AfterAttributeName; return false; }
                _name.Clear(); _value.Clear(); _state = State.AttributeName;
                if (c == '=')
                {
                    Error("unexpected-equals-sign-before-attribute-name");
                    Append(_name, Take());
                }
                return false;

            case State.AttributeName:
                if (White(c) || c == '/' || c == '>') { _state = State.AfterAttributeName; return false; }
                if (c == '=') { Take(); _state = State.BeforeAttributeValue; return false; }
                if (c == '\0') Error("unexpected-null-character");
                if (c is '"' or '\'' or '<') Error("unexpected-character-in-attribute-name");
                Append(_name, Lower(ReplaceNull(Take()))); return false;

            case State.AfterAttributeName:
                if (White(c)) { Take(); return false; }
                if (c == '/') { FinishAttribute(); Take(); _state = State.SelfClosing; return false; }
                if (c == '=') { Take(); _state = State.BeforeAttributeValue; return false; }
                if (c == '>') { Take(); return EmitTag(out token); }
                FinishAttribute(); _name.Clear(); _value.Clear(); _state = State.AttributeName; return false;

            case State.BeforeAttributeValue:
                if (White(c)) { Take(); return false; }
                if (c == '"') { Take(); _state = State.DoubleQuotedValue; return false; }
                if (c == '\'') { Take(); _state = State.SingleQuotedValue; return false; }
                if (c == '>') { Error("missing-attribute-value"); Take(); return EmitTag(out token); }
                _state = State.UnquotedValue; return false;

            case State.DoubleQuotedValue:
                if (c == '"') { Take(); FinishAttribute(); _state = State.AfterQuotedValue; return false; }
                if (c == '&') { _referenceStart = _input.Offset; Take(); _returnState = State.DoubleQuotedValue; _state = State.CharacterReference; return false; }
                if (c == '\0') Error("unexpected-null-character");
                Append(_value, ReplaceNull(Take())); return false;

            case State.SingleQuotedValue:
                if (c == '\'') { Take(); FinishAttribute(); _state = State.AfterQuotedValue; return false; }
                if (c == '&') { _referenceStart = _input.Offset; Take(); _returnState = State.SingleQuotedValue; _state = State.CharacterReference; return false; }
                if (c == '\0') Error("unexpected-null-character");
                Append(_value, ReplaceNull(Take())); return false;

            case State.UnquotedValue:
                if (White(c)) { Take(); FinishAttribute(); _state = State.BeforeAttributeName; return false; }
                if (c == '&') { _referenceStart = _input.Offset; Take(); _returnState = State.UnquotedValue; _state = State.CharacterReference; return false; }
                if (c == '>') { Take(); return EmitTag(out token); }
                if (c == '\0') Error("unexpected-null-character");
                if (c is '"' or '\'' or '<' or '=' or '`') Error("unexpected-character-in-unquoted-attribute-value");
                Append(_value, ReplaceNull(Take())); return false;

            case State.AfterQuotedValue:
                if (White(c)) { Take(); _state = State.BeforeAttributeName; return false; }
                if (c == '/') { Take(); _state = State.SelfClosing; return false; }
                if (c == '>') { Take(); return EmitTag(out token); }
                Error("missing-whitespace-between-attributes"); _state = State.BeforeAttributeName; return false;

            case State.SelfClosing:
                if (c == '>')
                {
                    Take(); _selfClosing = true;
                    if (_endTag) _endTagHadSelfClosing = true;
                    return EmitTag(out token);
                }
                Error("unexpected-solidus-in-tag"); _state = State.BeforeAttributeName; return false;

            case State.MarkupDeclaration:
                if (Prefix("--", false, out var waitDash)) { ConsumeCount(2); _comment.Clear(); _state = State.CommentStart; return false; }
                if (Prefix("DOCTYPE", true, out var waitDoc)) { ConsumeCount(7); BeginDoctype(); _state = State.Doctype; return false; }
                if (Prefix("[CDATA[", false, out var waitCData))
                {
                    ConsumeCount(7);
                    if (_allowCData) { _state = State.CData; _tokenStart = -1; }
                    else { Error("cdata-in-html-content"); _comment.Clear(); Append(_comment, "[CDATA["); _state = State.BogusComment; }
                    return false;
                }
                if (waitDash || waitDoc || waitCData) { _needsInput = true; return false; }
                Error("incorrectly-opened-comment"); _comment.Clear(); _state = State.BogusComment; return false;

            // HTML Standard §13.2.5.72–76 (processing instruction states).
            case State.PiOpen:
                if (AsciiAlpha(c) || c == '_') { _state = State.PiTarget; return false; }
                Error("invalid-first-character-of-processing-instruction-target");
                _comment.Clear(); Append(_comment, '?'); _state = State.BogusComment; return false;
            case State.PiTarget:
                if (AsciiAlpha(c) || AsciiDigit(c) || c is '-' or '_')
                {
                    Append(_piTarget, Take()); return false;
                }
                if (White(c) || c is '?' or '>')
                {
                    var target = Materialize(_piTarget);
                    if (target.Equals("xml", StringComparison.OrdinalIgnoreCase) ||
                        target.Equals("xml-stylesheet", StringComparison.OrdinalIgnoreCase))
                    {
                        Error("disallowed-processing-instruction-target");
                        _comment.Clear(); Append(_comment, '?'); Append(_comment, target);
                        _state = State.BogusComment;
                    }
                    else { _piName = target; _state = State.PiAfterTarget; }
                    return false;
                }
                Error("invalid-processing-instruction-target");
                _comment.Clear(); Append(_comment, '?'); Append(_comment, _piTarget);
                _state = State.BogusComment;
                return false;
            case State.PiAfterTarget:
                if (White(c)) { Take(); return false; }
                _state = State.PiData; return false;
            case State.PiData:
                if (c == '?') { Take(); _state = State.PiQuestionable; return false; }
                if (c == '>') { Take(); return EmitProcessingInstruction(out token); }
                Append(_piData, Take()); return false;
            case State.PiQuestionable:
                if (c == '>') { Take(); return EmitProcessingInstruction(out token); }
                Append(_piData, '?'); _state = State.PiData; return false;

            case State.BogusComment:
                if (c == '>') { Take(); return EmitComment(out token); }
                if (c == '\0') Error("unexpected-null-character");
                Append(_comment, ReplaceNull(Take())); return false;

            case State.CommentStart:
                if (c == '-') { Take(); _state = State.CommentStartDash; return false; }
                if (c == '>') { Error("abrupt-closing-of-empty-comment"); Take(); return EmitComment(out token); }
                _state = State.Comment; return false;

            case State.CommentStartDash:
                if (c == '-') { Take(); _state = State.CommentEnd; return false; }
                if (c == '>') { Error("abrupt-closing-of-empty-comment"); Take(); return EmitComment(out token); }
                Append(_comment, '-'); _state = State.Comment; return false;

            case State.Comment:
                if (c == '<') { Take(); Append(_comment, '<'); _state = State.CommentLessThan; return false; }
                if (c == '-') { Take(); _state = State.CommentEndDash; return false; }
                if (c == '\0') Error("unexpected-null-character");
                Append(_comment, ReplaceNull(Take())); return false;

            case State.CommentLessThan:
                if (c == '!') { Take(); Append(_comment, '!'); _state = State.CommentLessThanBang; return false; }
                if (c == '<') { Take(); Append(_comment, '<'); return false; }
                _state = State.Comment; return false;

            case State.CommentLessThanBang:
                if (c == '-') { Take(); _state = State.CommentLessThanBangDash; return false; }
                _state = State.Comment; return false;

            case State.CommentLessThanBangDash:
                if (c == '-') { Take(); _state = State.CommentLessThanBangDashDash; return false; }
                Append(_comment, '-'); _state = State.Comment; return false;

            case State.CommentLessThanBangDashDash:
                if (c != '>') Error("nested-comment");
                _state = State.CommentEnd; return false;

            case State.CommentEndDash:
                if (c == '-') { Take(); _state = State.CommentEnd; return false; }
                Append(_comment, '-'); _state = State.Comment; return false;

            case State.CommentEnd:
                if (c == '>') { Take(); return EmitComment(out token); }
                if (c == '!') { Take(); _state = State.CommentEndBang; return false; }
                if (c == '-') { Take(); Append(_comment, '-'); return false; }
                Append(_comment, "--"); _state = State.Comment; return false;

            case State.CommentEndBang:
                if (c == '>') { Error("incorrectly-closed-comment"); Take(); return EmitComment(out token); }
                if (c == '-') { Append(_comment, "--!"); Take(); _state = State.CommentEndDash; return false; }
                Append(_comment, "--!"); _state = State.Comment; return false;

            case State.CData:
                if (c == ']') { Take(); _state = State.CDataBracket; return false; }
                Text(Take(), _input.Offset - 1); return false;
            case State.CDataBracket:
                if (c == ']') { Take(); _state = State.CDataEnd; return false; }
                Text(']', _input.Offset - 1); _state = State.CData; return false;
            case State.CDataEnd:
                if (c == '>') { Take(); _state = State.Data; _tokenStart = -1; return false; }
                if (c == ']') { Take(); Text(']', _input.Offset - 1); return false; }
                Text("]]", _input.Offset - 2); _state = State.CData; return false;
        }
        return StepDoctypeOrReference(c, out token);
    }
}
