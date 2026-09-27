using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Jint.HtmlParser.Html;

// HTML Standard §13.2.5, Data state through the DOCTYPE and CDATA states.
// Scalar states consume one input unit; ordinary runs are bounded by quota,
// source slice and text-buffer size. Declaration probes use at most sixteen units.
internal sealed partial class HtmlTokenizer
{
    private readonly HtmlInput _input;
    private readonly ParseDiagnosticCollector? _diagnostics;
    private bool _allowCData;
    private bool _readAllowCData;
    private bool _declarationAllowCData;
    private bool _markupOpenerAfterText;
    private readonly long _maxInput;
    private readonly int _maxToken;
    private readonly StringBuilder _text = new();
    private readonly StringBuilder _tagName = new();
    private readonly StringBuilder _name = new();
    private readonly StringBuilder _value = new();
    private readonly StringBuilder _comment = new();
    private readonly StringBuilder _piTarget = new();
    private readonly StringBuilder _piData = new();
    private readonly StringBuilder _textEndTagBuffer = new();
    private string? _piName;
    private readonly List<HtmlAttribute> _attributes = new();
    private readonly HashSet<string> _attributeNames = new(StringComparer.Ordinal);
    private State _state;
    private State _returnState;
    private HtmlToken _pending;
    private bool _hasPending;
    private bool _needsInput;
    private bool _canSetTextMode = true;
    private bool _canSetCDataContext = true;
    private bool _hasAcceptedRead;
    private bool _fragmentTextModeInitialized;
    private HtmlTextMode _textMode;
    private string? _appropriateEndTagName;
    private int _scriptWordLength;
    private bool _scriptWordMatches;
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
    private long _tokenSourceChanges;
    private long _referenceStart = -1;
    private long _textStart;
    private long _work;
    private long _remainingWork;
    private CancellationToken _activeCancellationToken;
    private int _numericBase;
    private uint _numericValue;
    private bool _numericOverflow;
    private bool _numericDigits;
    private int _bestEntityLength;
    private string? _bestEntityValue;
    private int _entityState;
    private readonly StringBuilder _reference = new();

    internal HtmlTokenizer(HtmlTokenizerContext context)
    {
        _input = new HtmlInput(ChargeMarkerTraversal);
        var limits = context.Limits ?? ParseLimits.Unbounded;
        _maxInput = limits.MaxInputCharacters;
        _maxToken = limits.MaxTokenCharacters;
        _diagnostics = context.Diagnostics;
        _diagnostics?.Clear();
        _allowCData = context.AllowCData;
    }

    internal long ConsumedInput => _input.Offset;
    internal long WorkCount => _work;
    internal long SourceChanges => _input.SourceChanges;

    internal void AppendInput(string chunk, bool isFinal = false)
    {
        EnsureInsertionIdle();
        if (_terminal || _ended) throw new InvalidOperationException("The tokenizer session is terminal.");
        ArgumentNullException.ThrowIfNull(chunk);
        if (_input.IsClosed) throw new InvalidOperationException("The input is closed.");
        if (_maxInput > 0 && chunk.Length > _maxInput - _input.Appended)
        {
            _terminal = true;
            throw new ParseLimitException(ParseLimitKind.InputCharacters, _maxInput, _maxInput + 1);
        }
        _input.Append(chunk, isFinal);
    }

    internal HtmlReadStatus Read(int workQuota, CancellationToken cancellationToken, out HtmlToken token) =>
        Read(workQuota, _allowCData, cancellationToken, out token);

    // HTML Standard §13.2.5.42: a declaration uses the adjusted current node's
    // context from when its < opener begins, even across later input/work yields.
    internal HtmlReadStatus Read(int workQuota, bool allowCDataForNextDeclaration,
        CancellationToken cancellationToken, out HtmlToken token) =>
        ReadInternal(null, workQuota, allowCDataForNextDeclaration, cancellationToken, out token);

    private HtmlReadStatus ReadCore(int workQuota, bool allowCDataForNextDeclaration,
        CancellationToken cancellationToken, out HtmlToken token)
    {
        token = default;
        if (_terminal) throw new InvalidOperationException("The tokenizer session is terminal.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workQuota);
        if (cancellationToken.IsCancellationRequested)
        {
            _terminal = true;
            cancellationToken.ThrowIfCancellationRequested();
        }
        _hasAcceptedRead = true;
        _readAllowCData = allowCDataForNextDeclaration;
        _canSetTextMode = false;
        _canSetCDataContext = false;
        if (_hasPending)
        {
            _hasPending = false;
            token = _pending;
            _canSetCDataContext = _canSetTextMode = CanSetModeAfterToken();
            return HtmlReadStatus.Token;
        }
        if (_ended) return HtmlReadStatus.Complete;
        try
        {
            _activeCancellationToken = cancellationToken;
            _remainingWork = workQuota;
            cancellationToken.ThrowIfCancellationRequested();
            while (_remainingWork > 0)
            {
                _remainingWork--;
                if (_work < long.MaxValue) _work++;
                cancellationToken.ThrowIfCancellationRequested();
                if (_input.SkipMarker()) continue;
                if (_skipLf)
                {
                    if (!_input.PeekCurrent(out var following))
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
                if (!_input.PeekCurrent(out var current))
                {
                    if (!_input.IsFinal) return ReturnNeedInput(out token);
                    if (RecoverAtEof(out token)) return HtmlReadStatus.Token;
                    _ended = true;
                    token = new HtmlToken(HtmlTokenKind.EndOfFile, offset: _input.Offset);
                    return HtmlReadStatus.Token;
                }
                if (TryConsumeRun(current))
                {
                    if (TextLength >= 4096) return FlushText(out token);
                    continue;
                }
                // HTML input preprocessing happens before state dispatch. Take() also
                // normalizes the consumed unit and skips a following LF, including
                // when that LF arrives in a later input segment.
                if (current == '\r') current = '\n';
                _needsInput = false;
                if (Step(current, out token)) return HtmlReadStatus.Token;
                if (_needsInput) return ReturnNeedInput(out token);
                if (TextLength >= 4096) return FlushText(out token);
                if (_hasPending)
                {
                    _hasPending = false;
                    token = _pending;
                    return HtmlReadStatus.Token;
                }
            }
            if (TextLength > 0 && _state == State.Data) return FlushText(out token);
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
        if (TextLength > 0) return FlushText(out token);
        token = default;
        if (_input.WorkExhausted) return HtmlReadStatus.Yielded;
        return _input.BoundaryReached ? HtmlReadStatus.InsertionBoundary : HtmlReadStatus.NeedInput;
    }

    private HtmlReadStatus FlushText(out HtmlToken token)
    {
        token = new HtmlToken(HtmlTokenKind.Text, dataSlice: TakeValue(_text), offset: _textStart);
        _canSetCDataContext = _canSetTextMode = CanSetModeAfterToken();
        return HtmlReadStatus.Token;
    }

    private bool Emit(HtmlToken produced, out HtmlToken token)
    {
        if (TextLength != 0)
        {
            _pending = produced;
            _hasPending = true;
            FlushText(out token);
            return true;
        }
        token = produced;
        _canSetCDataContext = _canSetTextMode = CanSetModeAfterToken();
        return true;
    }

    private void Text(char c, long offset)
    {
        if (TextLength == 0) _textStart = offset;
        Append(_text, c);
    }

    private void Text(string s, long offset)
    {
        if (TextLength == 0) _textStart = offset;
        Append(_text, s);
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
        Poll();
        ChargeCopy((long) _attributes.Count + _attributeNames.Count);
        _attributes.Clear();
        _attributeNames.Clear();
        Poll();
    }

    private void FinishAttribute()
    {
        if (_name.Length == 0) return;
        var name = MaterializeName(_name);
        EnsureAttributeNameCapacity();
        Poll();
        var unique = _attributeNames.Add(name);
        ChargeCopy(name.Length); // Hashing the candidate name scans its UTF-16 units.
        Poll();
        if (!unique) Error("duplicate-attribute");
        else AddAttribute(new HtmlAttribute(name, TakeValue(_value)));
        if (_endTag) _endTagHadAttributes = true;
        _name.Clear();
        _value.Clear();
        _valueSource = default;
    }

    private bool EmitTag(out HtmlToken token)
    {
        FinishAttribute();
        if (_endTag && _endTagHadAttributes) Error("end-tag-with-attributes");
        if (_endTag && _endTagHadSelfClosing) Error("end-tag-with-trailing-solidus");
        var attributes = CopyAttributes();
        var name = MaterializeName(_tagName);
        HtmlSourceLocation? source = null;
        if (!_endTag && name == "script")
        {
            source = _input.SourceLocation;
            if (_tokenSourceChanges != _input.SourceChanges) source = source.Value.AsMixed();
        }
        var produced = new HtmlToken(_endTag ? HtmlTokenKind.EndTag : HtmlTokenKind.StartTag,
            name: name, attributes: Array.AsReadOnly(attributes),
            selfClosing: _selfClosing, offset: _tokenStart,
            endTagHadAttributes: _endTagHadAttributes,
            endTagHadSelfClosing: _endTagHadSelfClosing,
            scriptSourceLocation: source,
            sourceChanges: _endTag ? _tokenSourceChanges : _input.SourceChanges);
        // Tag name has a separate buffer from the current attribute.
        _tokenStart = -1;
        _state = State.Data;
        _textMode = HtmlTextMode.Data;
        _appropriateEndTagName = null;
        return Emit(produced, out token);
    }

    private bool EmitComment(out HtmlToken token)
    {
        var produced = new HtmlToken(HtmlTokenKind.Comment, data: Materialize(_comment), offset: _tokenStart);
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
            data: Materialize(_piData), name: _piName, offset: _tokenStart);
        _piTarget.Clear();
        _piData.Clear();
        _piName = null;
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
        _input.BeginKeywordConsumption();
        try
        {
            for (var i = 0; i < count; i++)
            {
                _input.ConsumeKeywordCharacter(i);
                if (_tokenStart >= 0) CheckTokenLength(_tokenStart);
            }
        }
        finally { _input.EndKeywordConsumption(); }
    }

    // Quota is cooperative: CLR string/array allocation and copying cannot yield
    // midway. Poll around each such call, charge copied units, then yield at the
    // next safe state boundary. The scanner's authored loops yield by quota.
    private void Poll() => _activeCancellationToken.ThrowIfCancellationRequested();

    private void ChargeCopy(long units)
    {
        _work = _work > long.MaxValue - units ? long.MaxValue : _work + units;
        _remainingWork -= units;
    }

    private void EnsureAppendCapacity(StringBuilder buffer, int extra)
    {
        if (extra <= buffer.Capacity - buffer.Length) return;
        Poll();
        var existing = buffer.Length;
        var required = checked(existing + extra);
        var doubled = Math.Min(buffer.MaxCapacity, (long) buffer.Capacity * 2);
        buffer.EnsureCapacity((int) Math.Max(required, doubled));
        ChargeCopy(existing);
        Poll();
    }

    private void Append(StringBuilder buffer, char value)
    {
        CopySourceToBuffer(buffer);
        EnsureAppendCapacity(buffer, 1);
        buffer.Append(value);
    }

    private void Append(StringBuilder buffer, string value)
    {
        CopySourceToBuffer(buffer);
        EnsureAppendCapacity(buffer, value.Length);
        Poll();
        buffer.Append(value);
        ChargeCopy(value.Length);
        Poll();
    }

    private void Append(StringBuilder buffer, StringBuilder value) => Append(buffer, Materialize(value));

    private string Materialize(StringBuilder buffer)
    {
        Poll();
        var result = buffer.ToString();
        ChargeCopy(buffer.Length);
        Poll();
        return result;
    }

    private HtmlAttribute[] CopyAttributes()
    {
        if (_attributes.Count == 0) return Array.Empty<HtmlAttribute>();
        Poll();
        var result = _attributes.ToArray();
        ChargeCopy(_attributes.Count);
        Poll();
        return result;
    }

    private void EnsureAttributeNameCapacity()
    {
        var capacity = _attributeNames.EnsureCapacity(0);
        if (_attributeNames.Count < capacity) return;
        Poll();
        var target = capacity == 0 ? 4 : Math.Min(int.MaxValue, (long) capacity * 2);
        _attributeNames.EnsureCapacity((int) target);
        ChargeCopy(_attributeNames.Count);
        Poll();
    }

    private void AddAttribute(HtmlAttribute attribute)
    {
        if (_attributes.Count == _attributes.Capacity)
        {
            Poll();
            var target = _attributes.Capacity == 0 ? 4 : Math.Min(int.MaxValue, (long) _attributes.Capacity * 2);
            _attributes.Capacity = (int) target;
            ChargeCopy(_attributes.Count);
            Poll();
        }
        _attributes.Add(attribute);
    }
}
