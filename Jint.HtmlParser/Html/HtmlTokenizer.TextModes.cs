using System;

namespace Jint.HtmlParser.Html;

internal enum HtmlTextMode { Data, RcData, RawText, ScriptData, PlainText }

// HTML Standard §13.2.5.2–31: text contexts and script escape states.
internal sealed partial class HtmlTokenizer
{
    private bool _candidateMatches;
    private int _candidateLength;

    internal void SetTextMode(HtmlTextMode mode, string? appropriateEndTagName)
    {
        if (_terminal || _ended || !_canSetTextMode || !CanSetModeAfterToken())
            throw new InvalidOperationException("The text mode can only change between complete tokens.");
        if (mode is not (HtmlTextMode.Data or HtmlTextMode.RcData or HtmlTextMode.RawText or HtmlTextMode.ScriptData or HtmlTextMode.PlainText))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode is HtmlTextMode.RcData or HtmlTextMode.RawText or HtmlTextMode.ScriptData)
        {
            ArgumentNullException.ThrowIfNull(appropriateEndTagName);
            if (appropriateEndTagName.Length == 0) throw new ArgumentException("An appropriate end tag is required.", nameof(appropriateEndTagName));
            var normalized = appropriateEndTagName.ToCharArray();
            for (var i = 0; i < normalized.Length; i++) normalized[i] = Lower(normalized[i]);
            _appropriateEndTagName = new string(normalized);
        }
        else
        {
            if (appropriateEndTagName is not null)
                throw new ArgumentException("This mode has no appropriate end tag.", nameof(appropriateEndTagName));
            _appropriateEndTagName = null;
        }
        _textMode = mode;
        _state = ModeBaseState();
        _canSetTextMode = false;
    }

    private bool CanSetModeAfterToken() =>
        !_ended && !_hasPending && !_markupOpenerAfterText && _tokenStart < 0 && _referenceStart < 0 && _text.Length == 0 &&
        _state is State.Data or State.RcData or State.RawText or State.ScriptData or State.PlainText;

    private State ModeBaseState() => _textMode switch
    {
        HtmlTextMode.Data => State.Data,
        HtmlTextMode.RcData => State.RcData,
        HtmlTextMode.RawText => State.RawText,
        HtmlTextMode.ScriptData => State.ScriptData,
        HtmlTextMode.PlainText => State.PlainText,
        _ => throw new InvalidOperationException("Unknown text mode.")
    };

    private void StartTextLessThan(State next)
    {
        _tokenStart = _input.Offset;
        _tokenSourceChanges = _input.SourceChanges;
        Take();
        _state = next;
    }

    private void TextCurrent(char c, bool replaceNull = false)
    {
        var offset = _input.Offset;
        if (c == '\0' && replaceNull) Error("unexpected-null-character", offset);
        Text(replaceNull ? ReplaceNull(Take()) : Take(), offset);
    }

    private void BeginTextEndTag(State nameState)
    {
        BeginTag(true);
        _textEndTagBuffer.Clear();
        _candidateMatches = true;
        _candidateLength = 0;
        _state = nameState;
    }

    private void ConsumeEndTagLetter(char c)
    {
        Take();
        Append(_tagName, Lower(c));
        Append(_textEndTagBuffer, c);
        var name = _appropriateEndTagName;
        if (name is null || _candidateLength >= name.Length || Lower(c) != name[_candidateLength])
            _candidateMatches = false;
        _candidateLength++;
    }

    private bool AppropriateEndTag() =>
        _candidateMatches && _appropriateEndTagName is { } name && _candidateLength == name.Length;

    private void FallbackEndTag(State state)
    {
        Text("</", _tokenStart);
        if (_textEndTagBuffer.Length > 0) Text(Materialize(_textEndTagBuffer), _tokenStart + 2);
        _tokenStart = -1;
        _state = state;
    }

    private bool StepEndTagName(char c, State fallbackState, out HtmlToken token)
    {
        token = default;
        if (AsciiAlpha(c)) { ConsumeEndTagLetter(c); return false; }
        if (AppropriateEndTag())
        {
            if (White(c)) { Take(); _state = State.BeforeAttributeName; return false; }
            if (c == '/') { Take(); _state = State.SelfClosing; return false; }
            if (c == '>') { Take(); return EmitTag(out token); }
        }
        FallbackEndTag(fallbackState);
        return false;
    }

    private void ResetScriptWord()
    {
        _scriptWordLength = 0;
        _scriptWordMatches = true;
    }

    private void ScriptWordLetter(char c)
    {
        const string Script = "script";
        if (_scriptWordLength >= Script.Length || Lower(c) != Script[_scriptWordLength])
            _scriptWordMatches = false;
        if (_scriptWordLength < 7) _scriptWordLength++;
        TextCurrent(c);
    }

    private bool ScriptWordIsScript() => _scriptWordMatches && _scriptWordLength == 6;

    private bool StepTextMode(char c, out HtmlToken token)
    {
        token = default;
        switch (_state)
        {
            case State.RcData:
                if (c == '&') { _referenceStart = _input.Offset; Take(); _returnState = State.RcData; _state = State.CharacterReference; return false; }
                if (c == '<') { StartTextLessThan(State.RcDataLessThan); return false; }
                TextCurrent(c, replaceNull: true); return false;
            case State.RawText:
                if (c == '<') { StartTextLessThan(State.RawTextLessThan); return false; }
                TextCurrent(c, replaceNull: true); return false;
            case State.ScriptData:
                if (c == '<') { StartTextLessThan(State.ScriptLessThan); return false; }
                TextCurrent(c, replaceNull: true); return false;
            case State.PlainText:
                TextCurrent(c, replaceNull: true); return false;

            case State.RcDataLessThan:
            case State.RawTextLessThan:
            case State.ScriptLessThan:
                if (c == '/')
                {
                    Take();
                    _textEndTagBuffer.Clear();
                    _state = _state switch
                    {
                        State.RcDataLessThan => State.RcDataEndTagOpen,
                        State.RawTextLessThan => State.RawTextEndTagOpen,
                        _ => State.ScriptEndTagOpen
                    };
                    return false;
                }
                if (_state == State.ScriptLessThan && c == '!')
                {
                    Take(); Text("<!", _tokenStart); _tokenStart = -1;
                    _state = State.ScriptEscapeStart;
                    return false;
                }
                Text('<', _tokenStart); _tokenStart = -1; _state = ModeBaseState();
                return false;

            case State.RcDataEndTagOpen:
            case State.RawTextEndTagOpen:
            case State.ScriptEndTagOpen:
            case State.ScriptEscapedEndTagOpen:
                if (AsciiAlpha(c))
                {
                    var nameState = _state switch
                    {
                        State.RcDataEndTagOpen => State.RcDataEndTagName,
                        State.RawTextEndTagOpen => State.RawTextEndTagName,
                        State.ScriptEndTagOpen => State.ScriptEndTagName,
                        _ => State.ScriptEscapedEndTagName
                    };
                    BeginTextEndTag(nameState);
                    return false;
                }
                FallbackEndTag(_state == State.ScriptEscapedEndTagOpen ? State.ScriptEscaped : ModeBaseState());
                return false;

            case State.RcDataEndTagName: return StepEndTagName(c, State.RcData, out token);
            case State.RawTextEndTagName: return StepEndTagName(c, State.RawText, out token);
            case State.ScriptEndTagName: return StepEndTagName(c, State.ScriptData, out token);
            case State.ScriptEscapedEndTagName: return StepEndTagName(c, State.ScriptEscaped, out token);

            case State.ScriptEscapeStart:
                if (c == '-') { TextCurrent(c); _state = State.ScriptEscapeStartDash; }
                else _state = State.ScriptData;
                return false;
            case State.ScriptEscapeStartDash:
                if (c == '-') { TextCurrent(c); _state = State.ScriptEscapedDashDash; }
                else _state = State.ScriptData;
                return false;
            case State.ScriptEscaped:
                if (c == '-') { TextCurrent(c); _state = State.ScriptEscapedDash; return false; }
                if (c == '<') { StartTextLessThan(State.ScriptEscapedLessThan); return false; }
                TextCurrent(c, replaceNull: true); return false;
            case State.ScriptEscapedDash:
                if (c == '-') { TextCurrent(c); _state = State.ScriptEscapedDashDash; return false; }
                if (c == '<') { StartTextLessThan(State.ScriptEscapedLessThan); return false; }
                TextCurrent(c, replaceNull: true); _state = State.ScriptEscaped; return false;
            case State.ScriptEscapedDashDash:
                if (c == '-') { TextCurrent(c); return false; }
                if (c == '<') { StartTextLessThan(State.ScriptEscapedLessThan); return false; }
                if (c == '>') { TextCurrent(c); _state = State.ScriptData; return false; }
                TextCurrent(c, replaceNull: true); _state = State.ScriptEscaped; return false;
            case State.ScriptEscapedLessThan:
                if (c == '/') { Take(); _textEndTagBuffer.Clear(); _state = State.ScriptEscapedEndTagOpen; return false; }
                Text('<', _tokenStart); _tokenStart = -1;
                if (AsciiAlpha(c)) { ResetScriptWord(); _state = State.ScriptDoubleEscapeStart; }
                else _state = State.ScriptEscaped;
                return false;
            case State.ScriptDoubleEscapeStart:
                if (AsciiAlpha(c)) { ScriptWordLetter(c); return false; }
                if (White(c) || c is '/' or '>')
                {
                    _state = ScriptWordIsScript() ? State.ScriptDoubleEscaped : State.ScriptEscaped;
                    TextCurrent(c); return false;
                }
                _state = State.ScriptEscaped; return false;
            case State.ScriptDoubleEscaped:
                if (c == '-') { TextCurrent(c); _state = State.ScriptDoubleEscapedDash; return false; }
                if (c == '<') { TextCurrent(c); _state = State.ScriptDoubleEscapedLessThan; return false; }
                TextCurrent(c, replaceNull: true); return false;
            case State.ScriptDoubleEscapedDash:
                if (c == '-') { TextCurrent(c); _state = State.ScriptDoubleEscapedDashDash; return false; }
                if (c == '<') { TextCurrent(c); _state = State.ScriptDoubleEscapedLessThan; return false; }
                TextCurrent(c, replaceNull: true); _state = State.ScriptDoubleEscaped; return false;
            case State.ScriptDoubleEscapedDashDash:
                if (c == '-') { TextCurrent(c); return false; }
                if (c == '<') { TextCurrent(c); _state = State.ScriptDoubleEscapedLessThan; return false; }
                if (c == '>') { TextCurrent(c); _state = State.ScriptData; return false; }
                TextCurrent(c, replaceNull: true); _state = State.ScriptDoubleEscaped; return false;
            case State.ScriptDoubleEscapedLessThan:
                if (c == '/') { TextCurrent(c); ResetScriptWord(); _state = State.ScriptDoubleEscapeEnd; }
                else _state = State.ScriptDoubleEscaped;
                return false;
            case State.ScriptDoubleEscapeEnd:
                if (AsciiAlpha(c)) { ScriptWordLetter(c); return false; }
                if (White(c) || c is '/' or '>')
                {
                    _state = ScriptWordIsScript() ? State.ScriptEscaped : State.ScriptDoubleEscaped;
                    TextCurrent(c); return false;
                }
                _state = State.ScriptDoubleEscaped; return false;
        }
        throw new InvalidOperationException("Unknown HTML text state.");
    }

    private bool RecoverTextModeAtEof(out HtmlToken token)
    {
        token = default;
        switch (_state)
        {
            case State.RcDataLessThan:
            case State.RawTextLessThan:
            case State.ScriptLessThan:
                Text('<', _tokenStart); break;
            case State.RcDataEndTagOpen:
            case State.RawTextEndTagOpen:
            case State.ScriptEndTagOpen:
            case State.ScriptEscapedEndTagOpen:
                Text("</", _tokenStart); break;
            case State.RcDataEndTagName:
            case State.RawTextEndTagName:
            case State.ScriptEndTagName:
            case State.ScriptEscapedEndTagName:
                Text("</", _tokenStart);
                Text(Materialize(_textEndTagBuffer), _tokenStart + 2);
                break;
            case State.ScriptEscapedLessThan:
                Text('<', _tokenStart);
                Error("eof-in-script-html-comment-like-text");
                break;
            case State.ScriptEscaped:
            case State.ScriptEscapedDash:
            case State.ScriptEscapedDashDash:
            case State.ScriptDoubleEscapeStart:
            case State.ScriptDoubleEscaped:
            case State.ScriptDoubleEscapedDash:
            case State.ScriptDoubleEscapedDashDash:
            case State.ScriptDoubleEscapedLessThan:
            case State.ScriptDoubleEscapeEnd:
                Error("eof-in-script-html-comment-like-text");
                break;
        }
        if (_state is State.ScriptEscapedEndTagOpen or State.ScriptEscapedEndTagName)
            Error("eof-in-script-html-comment-like-text");
        _tokenStart = -1;
        _state = ModeBaseState();
        if (_text.Length > 0) { FlushText(out token); return true; }
        return false;
    }
}
