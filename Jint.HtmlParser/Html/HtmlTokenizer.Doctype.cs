using System;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTokenizer
{
    private bool StepDoctypeOrReference(char c, out HtmlToken token)
    {
        token = default;
        switch (_state)
        {
            case State.Doctype:
                if (White(c)) { Take(); _state = State.BeforeDoctypeName; return false; }
                if (c == '>') { Error("missing-doctype-name"); _forceQuirks = true; Take(); return EmitDoctype(out token); }
                Error("missing-whitespace-before-doctype-name"); _state = State.BeforeDoctypeName; return false;
            case State.BeforeDoctypeName:
                if (White(c)) { Take(); return false; }
                if (c == '>') { Error("missing-doctype-name"); _forceQuirks = true; Take(); return EmitDoctype(out token); }
                _name.Clear(); _state = State.DoctypeName; return false;
            case State.DoctypeName:
                if (White(c)) { _doctypeName = _name.ToString(); Take(); _state = State.AfterDoctypeName; return false; }
                if (c == '>') { _doctypeName = _name.ToString(); Take(); return EmitDoctype(out token); }
                if (c == '\0') Error("unexpected-null-character");
                _name.Append(Lower(ReplaceNull(Take()))); return false;
            case State.AfterDoctypeName:
                if (White(c)) { Take(); return false; }
                if (c == '>') { Take(); return EmitDoctype(out token); }
                if (Prefix("PUBLIC", true, out var waitPublic)) { ConsumeCount(6); _state = State.AfterPublicKeyword; return false; }
                if (Prefix("SYSTEM", true, out var waitSystem)) { ConsumeCount(6); _state = State.AfterSystemKeyword; return false; }
                if (waitPublic || waitSystem) { _needsInput = true; return false; }
                Error("invalid-character-sequence-after-doctype-name"); _forceQuirks = true; _state = State.BogusDoctype; return false;
            case State.AfterPublicKeyword:
                if (White(c)) { Take(); _state = State.BeforePublicIdentifier; return false; }
                if (c is '"' or '\'') { Error("missing-whitespace-after-doctype-public-keyword"); _state = State.BeforePublicIdentifier; return false; }
                if (c == '>') { Error("missing-doctype-public-identifier"); _forceQuirks = true; Take(); return EmitDoctype(out token); }
                Error("missing-quote-before-doctype-public-identifier"); _forceQuirks = true; _state = State.BogusDoctype; return false;
            case State.BeforePublicIdentifier:
                if (White(c)) { Take(); return false; }
                if (c == '"') { Take(); _value.Clear(); _state = State.PublicDouble; return false; }
                if (c == '\'') { Take(); _value.Clear(); _state = State.PublicSingle; return false; }
                if (c == '>') { Error("missing-doctype-public-identifier"); _forceQuirks = true; Take(); return EmitDoctype(out token); }
                Error("missing-quote-before-doctype-public-identifier"); _forceQuirks = true; _state = State.BogusDoctype; return false;
            case State.PublicDouble:
            case State.PublicSingle:
                if (c == (_state == State.PublicDouble ? '"' : '\'')) { Take(); _publicIdentifier = _value.ToString(); _state = State.AfterPublicIdentifier; return false; }
                if (c == '>') { Error("abrupt-doctype-public-identifier"); _publicIdentifier = _value.ToString(); _forceQuirks = true; Take(); return EmitDoctype(out token); }
                if (c == '\0') Error("unexpected-null-character");
                _value.Append(ReplaceNull(Take())); return false;
            case State.AfterPublicIdentifier:
                if (White(c)) { Take(); _state = State.BetweenPublicAndSystem; return false; }
                if (c == '>') { Take(); return EmitDoctype(out token); }
                if (c is '"' or '\'') { Error("missing-whitespace-between-doctype-public-and-system-identifiers"); _state = State.BeforeSystemIdentifier; return false; }
                Error("missing-quote-before-doctype-system-identifier"); _forceQuirks = true; _state = State.BogusDoctype; return false;
            case State.BetweenPublicAndSystem:
                if (White(c)) { Take(); return false; }
                if (c == '>') { Take(); return EmitDoctype(out token); }
                if (c is '"' or '\'') { _state = State.BeforeSystemIdentifier; return false; }
                Error("missing-quote-before-doctype-system-identifier"); _forceQuirks = true; _state = State.BogusDoctype; return false;
            case State.AfterSystemKeyword:
                if (White(c)) { Take(); _state = State.BeforeSystemIdentifier; return false; }
                if (c is '"' or '\'') { Error("missing-whitespace-after-doctype-system-keyword"); _state = State.BeforeSystemIdentifier; return false; }
                if (c == '>') { Error("missing-doctype-system-identifier"); _forceQuirks = true; Take(); return EmitDoctype(out token); }
                Error("missing-quote-before-doctype-system-identifier"); _forceQuirks = true; _state = State.BogusDoctype; return false;
            case State.BeforeSystemIdentifier:
                if (White(c)) { Take(); return false; }
                if (c == '"') { Take(); _value.Clear(); _state = State.SystemDouble; return false; }
                if (c == '\'') { Take(); _value.Clear(); _state = State.SystemSingle; return false; }
                if (c == '>') { Error("missing-doctype-system-identifier"); _forceQuirks = true; Take(); return EmitDoctype(out token); }
                Error("missing-quote-before-doctype-system-identifier"); _forceQuirks = true; _state = State.BogusDoctype; return false;
            case State.SystemDouble:
            case State.SystemSingle:
                if (c == (_state == State.SystemDouble ? '"' : '\'')) { Take(); _systemIdentifier = _value.ToString(); _state = State.AfterSystemIdentifier; return false; }
                if (c == '>') { Error("abrupt-doctype-system-identifier"); _systemIdentifier = _value.ToString(); _forceQuirks = true; Take(); return EmitDoctype(out token); }
                if (c == '\0') Error("unexpected-null-character");
                _value.Append(ReplaceNull(Take())); return false;
            case State.AfterSystemIdentifier:
                if (White(c)) { Take(); return false; }
                if (c == '>') { Take(); return EmitDoctype(out token); }
                Error("unexpected-character-after-doctype-system-identifier"); _state = State.BogusDoctype; return false;
            case State.BogusDoctype:
                if (c == '>') { Take(); return EmitDoctype(out token); }
                if (c == '\0') Error("unexpected-null-character");
                Take(); return false;
        }
        return StepReference(c);
    }

    private bool RecoverAtEof(out HtmlToken token)
    {
        token = default;
        switch (_state)
        {
            case State.TagOpen:
                Error("eof-before-tag-name"); Text('<', _tokenStart); _tokenStart = -1; break;
            case State.PiOpen:
            case State.PiTarget:
            case State.PiAfterTarget:
            case State.PiData:
            case State.PiQuestionable:
                Error("eof-in-processing-instruction"); _tokenStart = -1; break;
            case State.EndTagOpen:
                Error("eof-before-tag-name"); Text("</", _tokenStart); _tokenStart = -1; break;
            case State.TagName:
            case State.BeforeAttributeName:
            case State.AttributeName:
            case State.AfterAttributeName:
            case State.BeforeAttributeValue:
            case State.DoubleQuotedValue:
            case State.SingleQuotedValue:
            case State.UnquotedValue:
            case State.AfterQuotedValue:
            case State.SelfClosing:
                Error("eof-in-tag"); _tokenStart = -1; break;
            case State.MarkupDeclaration:
                Error("incorrectly-opened-comment"); _comment.Clear(); return EmitComment(out token);
            case State.BogusComment:
                return EmitComment(out token);
            case State.CommentStart:
            case State.CommentStartDash:
            case State.Comment:
            case State.CommentLessThan:
            case State.CommentLessThanBang:
            case State.CommentLessThanBangDash:
            case State.CommentLessThanBangDashDash:
            case State.CommentEndDash:
            case State.CommentEnd:
            case State.CommentEndBang:
                Error("eof-in-comment");
                return EmitComment(out token);
            case State.Doctype:
            case State.BeforeDoctypeName:
            case State.DoctypeName:
            case State.AfterDoctypeName:
            case State.AfterPublicKeyword:
            case State.BeforePublicIdentifier:
            case State.PublicDouble:
            case State.PublicSingle:
            case State.AfterPublicIdentifier:
            case State.BetweenPublicAndSystem:
            case State.AfterSystemKeyword:
            case State.BeforeSystemIdentifier:
            case State.SystemDouble:
            case State.SystemSingle:
            case State.AfterSystemIdentifier:
                Error("eof-in-doctype");
                if (_state == State.DoctypeName) _doctypeName = _name.ToString();
                if (_state is State.PublicDouble or State.PublicSingle) _publicIdentifier = _value.ToString();
                if (_state is State.SystemDouble or State.SystemSingle) _systemIdentifier = _value.ToString();
                if (_state != State.BogusDoctype) _forceQuirks = true;
                return EmitDoctype(out token);
            case State.BogusDoctype:
                return EmitDoctype(out token);
            case State.CData:
                Error("eof-in-cdata"); break;
            case State.CDataBracket:
                Error("eof-in-cdata"); Text(']', _input.Offset - 1); break;
            case State.CDataEnd:
                Error("eof-in-cdata"); Text("]]", _input.Offset - 2); break;
            case State.CharacterReference:
            case State.NamedReference:
            case State.AmbiguousAmpersand:
            case State.NumericReference:
            case State.HexStart:
            case State.DecimalStart:
            case State.HexReference:
            case State.DecimalReference:
                FinishReferenceAtEof(); return RecoverAtEof(out token);
        }
        _state = State.Data;
        if (_text.Length > 0) { FlushText(out token); return true; }
        return false;
    }
}
