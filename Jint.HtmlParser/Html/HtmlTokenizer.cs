using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Jint.HtmlParser.Html;

// HTML Standard §13.2.5, Data state through the DOCTYPE and CDATA states.
// One loop iteration consumes at most one input unit, apart from a seven-unit
// declaration marker. Declaration matching probes at most sixteen units.
internal sealed partial class HtmlTokenizer
{
    private readonly HtmlInput _input = new();
    private readonly ParseDiagnosticCollector? _diagnostics;
    private readonly bool _allowCData;
    private readonly long _maxInput;
    private readonly int _maxToken;
    private readonly StringBuilder _text = new();
    private readonly StringBuilder _tagName = new();
    private readonly StringBuilder _name = new();
    private readonly StringBuilder _value = new();
    private readonly StringBuilder _comment = new();
    private readonly StringBuilder _piTarget = new();
    private readonly StringBuilder _piData = new();
    private readonly List<HtmlAttribute> _attributes = new();
    private readonly HashSet<string> _attributeNames = new(StringComparer.Ordinal);
    private State _state;
    private State _returnState;
    private HtmlToken _pending;
    private bool _hasPending;
    private bool _needsInput;
    private bool _ended;
    private bool _terminal;
    private bool _skipLf;
    private bool _endTag;
    private bool _selfClosing;
    private bool _endTagHadAttributes;
    private bool _endTagHadSelfClosing;
    private bool _forceQuirks;
    private string? _doctypeName;
    private string? _publicIdentifier;
    private string? _systemIdentifier;
    private long _tokenStart = -1;
    private long _referenceStart = -1;
    private long _textStart;
    private long _work;
    private int _numericBase;
    private uint _numericValue;
    private bool _numericOverflow;
    private bool _numericDigits;
    private int _bestEntityLength;
    private string? _bestEntityValue;
    private readonly StringBuilder _reference = new();

    internal HtmlTokenizer(HtmlTokenizerContext context)
    {
        var limits = context.Limits ?? ParseLimits.Unbounded;
        _maxInput = limits.MaxInputCharacters;
        _maxToken = limits.MaxTokenCharacters;
        _diagnostics = context.Diagnostics;
        _diagnostics?.Clear();
        _allowCData = context.AllowCData;
    }

    internal long ConsumedInput => _input.Offset;
    internal long WorkCount => _work;

    internal void AppendInput(string chunk, bool isFinal = false)
    {
        if (_terminal) throw new InvalidOperationException("The tokenizer session is terminal.");
        ArgumentNullException.ThrowIfNull(chunk);
        if (_input.IsFinal) throw new InvalidOperationException("The input is closed.");
        if (_maxInput > 0 && chunk.Length > _maxInput - _input.Appended)
        {
            _terminal = true;
            throw new ParseLimitException(ParseLimitKind.InputCharacters, _maxInput, _maxInput + 1);
        }
        _input.Append(chunk, isFinal);
    }

    internal HtmlReadStatus Read(int workQuota, CancellationToken cancellationToken, out HtmlToken token)
    {
        token = default;
        if (_terminal) throw new InvalidOperationException("The tokenizer session is terminal.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workQuota);
        if (cancellationToken.IsCancellationRequested)
        {
            _terminal = true;
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (_hasPending)
        {
            _hasPending = false;
            token = _pending;
            return HtmlReadStatus.Token;
        }
        if (_ended) return HtmlReadStatus.Complete;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var spent = 0; spent < workQuota; spent++)
            {
                _work++;
                cancellationToken.ThrowIfCancellationRequested();
                if (_skipLf)
                {
                    if (!_input.Peek(0, out var following))
                    {
                        if (!_input.IsFinal) return ReturnNeedInput(out token);
                        _skipLf = false;
                    }
                    else
                    {
                        _skipLf = false;
                        if (following == '\n')
                        {
                            _input.Consume();
                            if (_tokenStart >= 0) CheckTokenLength(_tokenStart);
                            if (_referenceStart >= 0 && _tokenStart < 0) CheckTokenLength(_referenceStart);
                            continue;
                        }
                    }
                }
                if (!_input.Peek(0, out var current))
                {
                    if (!_input.IsFinal) return ReturnNeedInput(out token);
                    if (RecoverAtEof(out token)) return HtmlReadStatus.Token;
                    _ended = true;
                    token = new HtmlToken(HtmlTokenKind.EndOfFile, offset: _input.Offset);
                    return HtmlReadStatus.Token;
                }
                _needsInput = false;
                if (Step(current, out token)) return HtmlReadStatus.Token;
                if (_needsInput) return ReturnNeedInput(out token);
                if (_text.Length >= 4096) return FlushText(out token);
                if (_hasPending)
                {
                    _hasPending = false;
                    token = _pending;
                    return HtmlReadStatus.Token;
                }
            }
            if (_text.Length > 0 && _state == State.Data) return FlushText(out token);
            return HtmlReadStatus.Yielded;
        }
        catch (OperationCanceledException)
        {
            _terminal = true;
            throw;
        }
        catch (ParseLimitException)
        {
            _terminal = true;
            throw;
        }
    }

    private HtmlReadStatus ReturnNeedInput(out HtmlToken token)
    {
        if (_text.Length > 0) return FlushText(out token);
        token = default;
        return HtmlReadStatus.NeedInput;
    }

    private HtmlReadStatus FlushText(out HtmlToken token)
    {
        token = new HtmlToken(HtmlTokenKind.Text, data: _text.ToString(), offset: _textStart);
        _text.Clear();
        return HtmlReadStatus.Token;
    }

    private bool Emit(HtmlToken produced, out HtmlToken token)
    {
        if (_text.Length != 0)
        {
            _pending = produced;
            _hasPending = true;
            FlushText(out token);
            return true;
        }
        token = produced;
        return true;
    }

    private void Text(char c, long offset)
    {
        if (_text.Length == 0) _textStart = offset;
        _text.Append(c);
    }

    private void Text(string s, long offset)
    {
        if (_text.Length == 0) _textStart = offset;
        _text.Append(s);
    }

    private char Take()
    {
        var c = _input.Consume();
        if (c == '\r')
        {
            _skipLf = true;
            c = '\n';
        }
        if (_tokenStart >= 0) CheckTokenLength(_tokenStart);
        if (_referenceStart >= 0 && _tokenStart < 0) CheckTokenLength(_referenceStart);
        return c;
    }

    private void CheckTokenLength(long start)
    {
        if (_maxToken > 0 && _input.Offset - start > _maxToken)
            throw new ParseLimitException(ParseLimitKind.TokenCharacters, _maxToken, _maxToken + 1L);
    }

    private void Error(string code, long? offset = null) =>
        _diagnostics?.Add("html/" + code, offset ?? _input.Offset);

    private static bool White(char c) => c is '\t' or '\n' or '\f' or ' ';
    private static bool AsciiAlpha(char c) => (uint) (c - 'A') <= 25 || (uint) (c - 'a') <= 25;
    private static bool AsciiDigit(char c) => (uint) (c - '0') <= 9;
    private static char Lower(char c) => (uint) (c - 'A') <= 25 ? (char) (c + 32) : c;
    private static char ReplaceNull(char c) => c == '\0' ? '\uFFFD' : c;

    private void BeginTag(bool endTag)
    {
        _endTag = endTag;
        _selfClosing = false;
        _endTagHadAttributes = false;
        _endTagHadSelfClosing = false;
        _tagName.Clear();
        _name.Clear();
        _value.Clear();
        _attributes.Clear();
        _attributeNames.Clear();
    }

    private void FinishAttribute()
    {
        if (_name.Length == 0) return;
        var name = _name.ToString();
        if (!_attributeNames.Add(name)) Error("duplicate-attribute");
        else _attributes.Add(new HtmlAttribute(name, _value.ToString()));
        if (_endTag) _endTagHadAttributes = true;
        _name.Clear();
        _value.Clear();
    }

    private bool EmitTag(out HtmlToken token)
    {
        FinishAttribute();
        if (_endTag && _endTagHadAttributes) Error("end-tag-with-attributes");
        if (_endTag && _endTagHadSelfClosing) Error("end-tag-with-trailing-solidus");
        var attributes = _attributes.Count == 0 ? Array.Empty<HtmlAttribute>() : _attributes.ToArray();
        var produced = new HtmlToken(_endTag ? HtmlTokenKind.EndTag : HtmlTokenKind.StartTag,
            name: _tagName.ToString(), attributes: Array.AsReadOnly(attributes),
            selfClosing: _selfClosing, offset: _tokenStart,
            endTagHadAttributes: _endTagHadAttributes,
            endTagHadSelfClosing: _endTagHadSelfClosing);
        // Tag name has a separate buffer from the current attribute.
        _tokenStart = -1;
        _state = State.Data;
        return Emit(produced, out token);
    }

    private bool EmitComment(out HtmlToken token)
    {
        var produced = new HtmlToken(HtmlTokenKind.Comment, data: _comment.ToString(), offset: _tokenStart);
        _comment.Clear();
        _tokenStart = -1;
        _state = State.Data;
        return Emit(produced, out token);
    }

    private bool EmitDoctype(out HtmlToken token)
    {
        var produced = new HtmlToken(HtmlTokenKind.Doctype, name: _doctypeName,
            publicIdentifier: _publicIdentifier, systemIdentifier: _systemIdentifier,
            forceQuirks: _forceQuirks, offset: _tokenStart);
        _tokenStart = -1;
        _state = State.Data;
        return Emit(produced, out token);
    }

    private bool EmitProcessingInstruction(out HtmlToken token)
    {
        var produced = new HtmlToken(HtmlTokenKind.ProcessingInstruction,
            data: _piData.ToString(), name: _piTarget.ToString(), offset: _tokenStart);
        _piTarget.Clear();
        _piData.Clear();
        _tokenStart = -1;
        _state = State.Data;
        return Emit(produced, out token);
    }

    private void BeginDoctype()
    {
        _name.Clear();
        _value.Clear();
        _doctypeName = null;
        _publicIdentifier = null;
        _systemIdentifier = null;
        _forceQuirks = false;
    }

    private bool Prefix(string expected, bool ignoreCase, out bool needInput)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            if (!_input.Peek(i, out var c))
            {
                needInput = !_input.IsFinal;
                return false;
            }
            if (ignoreCase ? Lower(c) != Lower(expected[i]) : c != expected[i])
            {
                needInput = false;
                return false;
            }
        }
        needInput = false;
        return true;
    }

    private void ConsumeCount(int count)
    {
        for (var i = 0; i < count; i++) Take();
    }
}
