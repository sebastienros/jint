#if NET8_0_OR_GREATER
using System.Text;

namespace Jint.WebApi.Url.Parsing;

/// <summary>
/// The states of the basic URL parser, https://url.spec.whatwg.org/#url-parsing. The seven the <c>URL</c>
/// attribute setters pass as a state override are <see cref="SchemeStart"/>, <see cref="Host"/>,
/// <see cref="Hostname"/>, <see cref="Port"/>, <see cref="PathStart"/>, <see cref="Query"/> and
/// <see cref="Fragment"/>.
/// </summary>
internal enum UrlParserState
{
    SchemeStart,
    Scheme,
    NoScheme,
    SpecialRelativeOrAuthority,
    PathOrAuthority,
    Relative,
    RelativeSlash,
    SpecialAuthoritySlashes,
    SpecialAuthorityIgnoreSlashes,
    Authority,
    Host,
    Hostname,
    Port,
    File,
    FileSlash,
    FileHost,
    PathStart,
    Path,
    OpaquePath,
    Query,
    Fragment,
}

/// <summary>
/// The basic URL parser, https://url.spec.whatwg.org/#concept-basic-url-parser — the spec's state machine,
/// transcribed.
/// </summary>
/// <remarks>
/// <para>
/// The transcription is deliberately literal: one <c>switch</c> over <see cref="UrlParserState"/> inside one
/// loop, EOF modelled as the pointer having reached the end rather than as a sentinel character, and the spec's
/// "decrease pointer by 1" spelled as exactly that. Where the spec says "return" in a state-override run it
/// means terminate successfully; where it says "return failure" the caller's URL may already have been
/// mutated, and that is observable and intended — the Web Platform Tests pin it for <c>url.host =
/// "example.com:65536"</c>, whose comment reads "Port numbers are 16 bit integers, overflowing is an error.
/// Hostname is still set, though."
/// </para>
/// <para>
/// One deliberate departure from the letter of the algorithm: the spec's pointer walks <i>code points</i> while
/// this one walks UTF-16 code units. The two agree everywhere it matters, because the input has already been
/// converted to a scalar value string (so a surrogate is always half of a pair), every code point the state
/// machine compares against is ASCII, and every place that percent-encodes the current code point consumes the
/// whole pair. The one arithmetic use — the authority state's "decrease pointer by buffer's code point length
/// + 1" — works out because the buffer holds exactly the code units walked past.
/// </para>
/// <para>
/// This is a <c>ref struct</c> so the spec's <c>buffer</c> can be a pooled <see cref="ValueStringBuilder"/>
/// rather than a per-parse allocation.
/// </para>
/// </remarks>
internal ref struct UrlParser : IDisposable
{
    private const int Eof = -1;

    private readonly string _input;
    private readonly UrlRecord? _base;
    private readonly UrlRecord _url;
    private readonly UrlParserState? _stateOverride;

    /// <summary>The spec's <c>buffer</c>.</summary>
    private ValueStringBuilder _buffer;

    /// <summary>
    /// The opaque path state and the fragment state are the two that append to a URL component code point by
    /// code point rather than through <c>buffer</c>. Accumulating into one more builder and committing it once
    /// keeps those two states linear rather than quadratic; the two never overlap in time, because a fragment
    /// only ever begins after an opaque path has ended.
    /// </summary>
    private ValueStringBuilder _tail;

    private TailTarget _tailTarget;
    private UrlParserState _state;
    private int _pointer;
    private bool _atSignSeen;
    private bool _insideBrackets;
    private bool _passwordTokenSeen;

    private enum TailTarget
    {
        None,
        OpaquePath,
        Fragment,
    }

    private enum StepResult
    {
        /// <summary>Keep running the state machine.</summary>
        Continue,

        /// <summary>The spec's bare "return" — a state-override run that has done its work.</summary>
        Return,

        /// <summary>The spec's "return failure".</summary>
        Failure,
    }

    private UrlParser(string input, UrlRecord? baseUrl, UrlRecord url, UrlParserState? stateOverride)
    {
        _input = Sanitize(input, trim: stateOverride is null);
        _base = baseUrl;
        _url = url;
        _stateOverride = stateOverride;
        _state = stateOverride ?? UrlParserState.SchemeStart;
        // Pooled rather than stack-allocated: a stackalloc here would live in the constructor's frame, which
        // is gone by the time the state machine runs.
        _buffer = new ValueStringBuilder(64);
        _tail = default;
        _tailTarget = TailTarget.None;
        _pointer = 0;
        _atSignSeen = false;
        _insideBrackets = false;
        _passwordTokenSeen = false;
    }

    /// <summary>
    /// The URL parser, https://url.spec.whatwg.org/#concept-url-parser, minus the "blob" step: this
    /// implementation has no blob URL store, so a blob URL's blob URL entry is always null.
    /// </summary>
    /// <returns>The parsed record, or <see langword="null"/> for the spec's failure value.</returns>
    internal static UrlRecord? Parse(string input, UrlRecord? baseUrl = null)
    {
        var url = new UrlRecord();
        return Run(input, baseUrl, url, stateOverride: null) ? url : null;
    }

    /// <summary>
    /// The API URL parser, https://url.spec.whatwg.org/#api-url-parser — what the <c>URL</c> constructor,
    /// <c>URL.parse</c> and <c>URL.canParse</c> all run. A base that does not parse is failure for the whole
    /// call, before the input is looked at.
    /// </summary>
    internal static UrlRecord? ParseApi(string input, string? baseHref)
    {
        UrlRecord? parsedBase = null;
        if (baseHref is not null)
        {
            parsedBase = Parse(baseHref);
            if (parsedBase is null)
            {
                return null;
            }
        }

        return Parse(input, parsedBase);
    }

    /// <summary>
    /// The basic URL parser run with an existing URL and a state override — what every <c>URL</c> attribute
    /// setter is defined in terms of. <paramref name="url"/> is modified in place, including when this returns
    /// <see langword="false"/>.
    /// </summary>
    internal static bool ParseInto(string input, UrlRecord url, UrlParserState stateOverride)
        => Run(input, baseUrl: null, url, stateOverride);

    private static bool Run(string input, UrlRecord? baseUrl, UrlRecord url, UrlParserState? stateOverride)
    {
        // Deliberately not a `using` declaration: that would make the local read-only, and invoking a mutating
        // instance method on a read-only local of a non-readonly struct runs it on a defensive copy — so the
        // copy would grow (and return to the pool) a buffer the original still holds and would return again.
        var parser = new UrlParser(input, baseUrl, url, stateOverride);
        try
        {
            return parser.Execute();
        }
        finally
        {
            parser.Dispose();
        }
    }

    /// <summary>Returns both pooled buffers.</summary>
    public void Dispose()
    {
        _buffer.Dispose();
        _tail.Dispose();
    }

    private bool Execute()
    {
        while (true)
        {
            var result = Step();
            if (result == StepResult.Failure)
            {
                return false;
            }

            if (result == StepResult.Return)
            {
                break;
            }

            // "If after a run pointer points to the EOF code point, go to the next step. Otherwise, increase
            // pointer by 1 and continue with the state machine."
            if (_pointer >= _input.Length)
            {
                break;
            }

            _pointer++;
        }

        FlushTail();
        return true;
    }

    /// <summary>The code point at the pointer, or <see cref="Eof"/>.</summary>
    private readonly int C => (uint) _pointer < (uint) _input.Length ? _input[_pointer] : Eof;

    /// <summary>The spec's <c>remaining</c>: the code points after the pointer.</summary>
    private readonly ReadOnlySpan<char> Remaining
        => (uint) (_pointer + 1) < (uint) _input.Length ? _input.AsSpan(_pointer + 1) : default;

    private readonly bool RemainingStartsWith(char c) => Remaining.Length > 0 && Remaining[0] == c;

    private StepResult Step()
    {
        var c = C;
        switch (_state)
        {
            case UrlParserState.SchemeStart:
                return SchemeStartState(c);
            case UrlParserState.Scheme:
                return SchemeState(c);
            case UrlParserState.NoScheme:
                return NoSchemeState(c);
            case UrlParserState.SpecialRelativeOrAuthority:
                return SpecialRelativeOrAuthorityState(c);
            case UrlParserState.PathOrAuthority:
                return PathOrAuthorityState(c);
            case UrlParserState.Relative:
                return RelativeState(c);
            case UrlParserState.RelativeSlash:
                return RelativeSlashState(c);
            case UrlParserState.SpecialAuthoritySlashes:
                return SpecialAuthoritySlashesState(c);
            case UrlParserState.SpecialAuthorityIgnoreSlashes:
                return SpecialAuthorityIgnoreSlashesState(c);
            case UrlParserState.Authority:
                return AuthorityState(c);
            case UrlParserState.Host:
            case UrlParserState.Hostname:
                return HostState(c);
            case UrlParserState.Port:
                return PortState(c);
            case UrlParserState.File:
                return FileState(c);
            case UrlParserState.FileSlash:
                return FileSlashState(c);
            case UrlParserState.FileHost:
                return FileHostState(c);
            case UrlParserState.PathStart:
                return PathStartState(c);
            case UrlParserState.Path:
                return PathState(c);
            case UrlParserState.OpaquePath:
                return OpaquePathState(c);
            case UrlParserState.Query:
                return QueryState(c);
            default:
                return FragmentState(c);
        }
    }

    /// <summary>https://url.spec.whatwg.org/#scheme-start-state</summary>
    private StepResult SchemeStartState(int c)
    {
        if (c != Eof && UrlCharacters.IsAsciiAlpha((char) c))
        {
            _buffer.Append(AsciiLower((char) c));
            _state = UrlParserState.Scheme;
            return StepResult.Continue;
        }

        if (_stateOverride is null)
        {
            _state = UrlParserState.NoScheme;
            _pointer--;
            return StepResult.Continue;
        }

        return StepResult.Failure;
    }

    /// <summary>https://url.spec.whatwg.org/#scheme-state</summary>
    private StepResult SchemeState(int c)
    {
        if (c != Eof && (UrlCharacters.IsAsciiAlphanumeric((char) c) || c == '+' || c == '-' || c == '.'))
        {
            _buffer.Append(AsciiLower((char) c));
            return StepResult.Continue;
        }

        if (c == ':')
        {
            var buffer = _buffer.AsSpan().ToString();

            if (_stateOverride is not null)
            {
                var urlIsSpecial = _url.IsSpecial;
                var bufferIsSpecial = UrlRecord.IsSpecialScheme(buffer);
                if (urlIsSpecial != bufferIsSpecial)
                {
                    return StepResult.Return;
                }

                if ((_url.IncludesCredentials || _url.Port is not null) && string.Equals(buffer, "file", StringComparison.Ordinal))
                {
                    return StepResult.Return;
                }

                if (_url.Scheme is "file" && _url.Host is { Kind: UrlHostKind.Empty })
                {
                    return StepResult.Return;
                }
            }

            _url.Scheme = buffer;

            if (_stateOverride is not null)
            {
                if (_url.Port == UrlRecord.DefaultPort(_url.Scheme))
                {
                    _url.Port = null;
                }

                return StepResult.Return;
            }

            _buffer.Length = 0;

            if (_url.Scheme is "file")
            {
                _state = UrlParserState.File;
            }
            else if (_url.IsSpecial && _base is not null && string.Equals(_base.Scheme, _url.Scheme, StringComparison.Ordinal))
            {
                _state = UrlParserState.SpecialRelativeOrAuthority;
            }
            else if (_url.IsSpecial)
            {
                _state = UrlParserState.SpecialAuthoritySlashes;
            }
            else if (RemainingStartsWith('/'))
            {
                _state = UrlParserState.PathOrAuthority;
                _pointer++;
            }
            else
            {
                _url.OpaquePath = string.Empty;
                _state = UrlParserState.OpaquePath;
            }

            return StepResult.Continue;
        }

        if (_stateOverride is null)
        {
            _buffer.Length = 0;
            _state = UrlParserState.NoScheme;

            // "Start over (from the first code point in input)": the loop's own increment brings the pointer
            // back to zero before the no scheme state runs.
            _pointer = -1;
            return StepResult.Continue;
        }

        return StepResult.Failure;
    }

    /// <summary>https://url.spec.whatwg.org/#no-scheme-state</summary>
    private StepResult NoSchemeState(int c)
    {
        if (_base is null || (_base.HasOpaquePath && c != '#'))
        {
            return StepResult.Failure;
        }

        if (_base.HasOpaquePath)
        {
            _url.Scheme = _base.Scheme;
            _url.OpaquePath = _base.OpaquePath;
            _url.Query = _base.Query;
            _url.Fragment = string.Empty;
            _state = UrlParserState.Fragment;
            return StepResult.Continue;
        }

        _state = _base.Scheme is "file" ? UrlParserState.File : UrlParserState.Relative;
        _pointer--;
        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#special-relative-or-authority-state</summary>
    private StepResult SpecialRelativeOrAuthorityState(int c)
    {
        if (c == '/' && RemainingStartsWith('/'))
        {
            _state = UrlParserState.SpecialAuthorityIgnoreSlashes;
            _pointer++;
            return StepResult.Continue;
        }

        _state = UrlParserState.Relative;
        _pointer--;
        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#path-or-authority-state</summary>
    private StepResult PathOrAuthorityState(int c)
    {
        if (c == '/')
        {
            _state = UrlParserState.Authority;
            return StepResult.Continue;
        }

        _state = UrlParserState.Path;
        _pointer--;
        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#relative-state</summary>
    private StepResult RelativeState(int c)
    {
        // The state is only ever reached with a non-null base whose scheme is not "file".
        var baseUrl = _base!;
        _url.Scheme = baseUrl.Scheme;

        if (c == '/' || (_url.IsSpecial && c == '\\'))
        {
            _state = UrlParserState.RelativeSlash;
            return StepResult.Continue;
        }

        _url.Username = baseUrl.Username;
        _url.Password = baseUrl.Password;
        _url.Host = baseUrl.Host;
        _url.Port = baseUrl.Port;
        _url.Path = [.. baseUrl.Path];
        _url.OpaquePath = baseUrl.OpaquePath;
        _url.Query = baseUrl.Query;

        if (c == '?')
        {
            _url.Query = string.Empty;
            _state = UrlParserState.Query;
        }
        else if (c == '#')
        {
            _url.Fragment = string.Empty;
            _state = UrlParserState.Fragment;
        }
        else if (c != Eof)
        {
            _url.Query = null;
            _url.ShortenPath();
            _state = UrlParserState.Path;
            _pointer--;
        }

        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#relative-slash-state</summary>
    private StepResult RelativeSlashState(int c)
    {
        if (_url.IsSpecial && (c == '/' || c == '\\'))
        {
            _state = UrlParserState.SpecialAuthorityIgnoreSlashes;
            return StepResult.Continue;
        }

        if (c == '/')
        {
            _state = UrlParserState.Authority;
            return StepResult.Continue;
        }

        var baseUrl = _base!;
        _url.Username = baseUrl.Username;
        _url.Password = baseUrl.Password;
        _url.Host = baseUrl.Host;
        _url.Port = baseUrl.Port;
        _state = UrlParserState.Path;
        _pointer--;
        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#special-authority-slashes-state</summary>
    private StepResult SpecialAuthoritySlashesState(int c)
    {
        _state = UrlParserState.SpecialAuthorityIgnoreSlashes;

        if (c == '/' && RemainingStartsWith('/'))
        {
            _pointer++;
        }
        else
        {
            _pointer--;
        }

        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#special-authority-ignore-slashes-state</summary>
    private StepResult SpecialAuthorityIgnoreSlashesState(int c)
    {
        if (c != '/' && c != '\\')
        {
            _state = UrlParserState.Authority;
            _pointer--;
        }

        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#authority-state</summary>
    private StepResult AuthorityState(int c)
    {
        if (c == '@')
        {
            if (_atSignSeen)
            {
                _buffer.Insert(0, "%40");
            }

            _atSignSeen = true;

            // The spec walks the buffer code point by code point, splitting at the first U+003A (:) that is
            // not already inside the password. Splitting once and encoding each half is the same thing: the
            // userinfo percent-encode set contains U+003A, so every later colon still encodes as "%3A".
            var span = _buffer.AsSpan();
            if (!_passwordTokenSeen)
            {
                var colon = span.IndexOf(':');
                if (colon >= 0)
                {
                    _url.Username += PercentEncoding.Encode(span.Slice(0, colon), PercentEncodeSet.Userinfo);
                    _url.Password += PercentEncoding.Encode(span.Slice(colon + 1), PercentEncodeSet.Userinfo);
                    _passwordTokenSeen = true;
                }
                else
                {
                    _url.Username += PercentEncoding.Encode(span, PercentEncodeSet.Userinfo);
                }
            }
            else
            {
                _url.Password += PercentEncoding.Encode(span, PercentEncodeSet.Userinfo);
            }

            _buffer.Length = 0;
            return StepResult.Continue;
        }

        if (c is Eof or '/' or '?' or '#' || (_url.IsSpecial && c == '\\'))
        {
            if (_atSignSeen && _buffer.Length == 0)
            {
                return StepResult.Failure;
            }

            _pointer -= _buffer.Length + 1;
            _buffer.Length = 0;
            _state = UrlParserState.Host;
            return StepResult.Continue;
        }

        _buffer.Append((char) c);
        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#host-state and https://url.spec.whatwg.org/#hostname-state</summary>
    private StepResult HostState(int c)
    {
        if (_stateOverride is not null && _url.Scheme is "file")
        {
            _pointer--;
            _state = UrlParserState.FileHost;
            return StepResult.Continue;
        }

        if (c == ':' && !_insideBrackets)
        {
            if (_buffer.Length == 0 || _stateOverride == UrlParserState.Hostname)
            {
                return StepResult.Failure;
            }

            if (!HostParser.TryParse(_buffer.AsSpan().ToString(), !_url.IsSpecial, out var parsed))
            {
                return StepResult.Failure;
            }

            _url.Host = parsed;
            _buffer.Length = 0;
            _state = UrlParserState.Port;
            return StepResult.Continue;
        }

        if (c is Eof or '/' or '?' or '#' || (_url.IsSpecial && c == '\\'))
        {
            _pointer--;

            if (_url.IsSpecial && _buffer.Length == 0)
            {
                return StepResult.Failure;
            }

            if (_stateOverride is not null && _buffer.Length == 0 && (_url.IncludesCredentials || _url.Port is not null))
            {
                return StepResult.Failure;
            }

            if (!HostParser.TryParse(_buffer.AsSpan().ToString(), !_url.IsSpecial, out var parsed))
            {
                return StepResult.Failure;
            }

            _url.Host = parsed;
            _buffer.Length = 0;
            _state = UrlParserState.PathStart;

            return _stateOverride is not null ? StepResult.Return : StepResult.Continue;
        }

        if (c == '[')
        {
            _insideBrackets = true;
        }
        else if (c == ']')
        {
            _insideBrackets = false;
        }

        _buffer.Append((char) c);
        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#port-state</summary>
    private StepResult PortState(int c)
    {
        if (c != Eof && UrlCharacters.IsAsciiDigit((char) c))
        {
            _buffer.Append((char) c);
            return StepResult.Continue;
        }

        if (c is Eof or '/' or '?' or '#' || (_url.IsSpecial && c == '\\') || _stateOverride is not null)
        {
            if (_buffer.Length != 0)
            {
                var port = 0;
                foreach (var digit in _buffer.AsSpan())
                {
                    port = port * 10 + (digit - '0');
                    if (port > ushort.MaxValue)
                    {
                        return StepResult.Failure;
                    }
                }

                _url.Port = port == UrlRecord.DefaultPort(_url.Scheme) ? null : port;
                _buffer.Length = 0;

                if (_stateOverride is not null)
                {
                    return StepResult.Return;
                }
            }
            else if (_stateOverride is not null)
            {
                // A state-override run that reached here with nothing buffered was handed something that is not
                // a port at all — "abc", or the empty string. The spec's "return failure" is inside this branch
                // and the success "return" is inside the one above, which the URL port setter cannot tell apart
                // because it discards the result either way; URLPattern's canonicalize a port does not.
                return StepResult.Failure;
            }

            _state = UrlParserState.PathStart;
            _pointer--;
            return StepResult.Continue;
        }

        return StepResult.Failure;
    }

    /// <summary>https://url.spec.whatwg.org/#file-state</summary>
    private StepResult FileState(int c)
    {
        _url.Scheme = "file";
        _url.Host = UrlHost.Empty;

        if (c == '/' || c == '\\')
        {
            _state = UrlParserState.FileSlash;
            return StepResult.Continue;
        }

        if (_base is null || _base.Scheme is not "file")
        {
            _state = UrlParserState.Path;
            _pointer--;
            return StepResult.Continue;
        }

        _url.Host = _base.Host;
        _url.Path = [.. _base.Path];
        _url.Query = _base.Query;

        if (c == '?')
        {
            _url.Query = string.Empty;
            _state = UrlParserState.Query;
        }
        else if (c == '#')
        {
            _url.Fragment = string.Empty;
            _state = UrlParserState.Fragment;
        }
        else if (c != Eof)
        {
            _url.Query = null;

            if (!UrlCharacters.StartsWithWindowsDriveLetter(_input.AsSpan(_pointer)))
            {
                _url.ShortenPath();
            }
            else
            {
                // A (platform-independent) Windows drive letter quirk.
                _url.Path.Clear();
            }

            _state = UrlParserState.Path;
            _pointer--;
        }

        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#file-slash-state</summary>
    private StepResult FileSlashState(int c)
    {
        if (c == '/' || c == '\\')
        {
            _state = UrlParserState.FileHost;
            return StepResult.Continue;
        }

        if (_base is not null && _base.Scheme is "file")
        {
            _url.Host = _base.Host;

            if (!UrlCharacters.StartsWithWindowsDriveLetter(_input.AsSpan(_pointer))
                && _base.Path.Count > 0
                && UrlCharacters.IsNormalizedWindowsDriveLetter(_base.Path[0].AsSpan()))
            {
                // A (platform-independent) Windows drive letter quirk.
                _url.Path.Add(_base.Path[0]);
            }
        }

        _state = UrlParserState.Path;
        _pointer--;
        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#file-host-state</summary>
    private StepResult FileHostState(int c)
    {
        if (c is not (Eof or '/' or '\\' or '?' or '#'))
        {
            _buffer.Append((char) c);
            return StepResult.Continue;
        }

        _pointer--;

        if (_stateOverride is null && UrlCharacters.IsWindowsDriveLetter(_buffer.AsSpan()))
        {
            // A (platform-independent) Windows drive letter quirk: buffer is deliberately not reset here, and
            // is used by the path state instead.
            _state = UrlParserState.Path;
            return StepResult.Continue;
        }

        if (_buffer.Length == 0)
        {
            _url.Host = UrlHost.Empty;

            if (_stateOverride is not null)
            {
                return StepResult.Return;
            }

            _state = UrlParserState.PathStart;
            return StepResult.Continue;
        }

        if (!HostParser.TryParse(_buffer.AsSpan().ToString(), !_url.IsSpecial, out var parsed))
        {
            return StepResult.Failure;
        }

        _url.Host = parsed.Serialized is "localhost" ? UrlHost.Empty : parsed;

        if (_stateOverride is not null)
        {
            return StepResult.Return;
        }

        _buffer.Length = 0;
        _state = UrlParserState.PathStart;
        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#path-start-state</summary>
    private StepResult PathStartState(int c)
    {
        if (_url.IsSpecial)
        {
            _state = UrlParserState.Path;
            if (c != '/' && c != '\\')
            {
                _pointer--;
            }

            return StepResult.Continue;
        }

        if (_stateOverride is null && c == '?')
        {
            _url.Query = string.Empty;
            _state = UrlParserState.Query;
            return StepResult.Continue;
        }

        if (_stateOverride is null && c == '#')
        {
            _url.Fragment = string.Empty;
            _state = UrlParserState.Fragment;
            return StepResult.Continue;
        }

        if (c != Eof)
        {
            _state = UrlParserState.Path;
            if (c != '/')
            {
                _pointer--;
            }

            return StepResult.Continue;
        }

        if (_stateOverride is not null && _url.Host is null)
        {
            _url.Path.Add(string.Empty);
        }

        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#path-state</summary>
    private StepResult PathState(int c)
    {
        var segmentEnds = c is Eof or '/'
            || (_url.IsSpecial && c == '\\')
            || (_stateOverride is null && (c == '?' || c == '#'));

        if (!segmentEnds)
        {
            AppendEncodedCodePoint(ref _buffer, PercentEncodeSet.Path);
            return StepResult.Continue;
        }

        var slashLike = c == '/' || (_url.IsSpecial && c == '\\');
        var segment = _buffer.AsSpan();

        if (UrlCharacters.IsDoubleDotSegment(segment))
        {
            _url.ShortenPath();
            if (!slashLike)
            {
                // "This means that for input /usr/.. the result is / and not a lack of a path."
                _url.Path.Add(string.Empty);
            }
        }
        else if (UrlCharacters.IsSingleDotSegment(segment))
        {
            if (!slashLike)
            {
                _url.Path.Add(string.Empty);
            }
        }
        else
        {
            if (_url.Scheme is "file" && _url.Path.Count == 0 && UrlCharacters.IsWindowsDriveLetter(segment))
            {
                // A (platform-independent) Windows drive letter quirk.
                _buffer[1] = ':';
            }

            _url.Path.Add(_buffer.AsSpan().ToString());
        }

        _buffer.Length = 0;

        if (c == '?')
        {
            _url.Query = string.Empty;
            _state = UrlParserState.Query;
        }
        else if (c == '#')
        {
            _url.Fragment = string.Empty;
            _state = UrlParserState.Fragment;
        }

        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#cannot-be-a-base-url-path-state</summary>
    private StepResult OpaquePathState(int c)
    {
        BeginTail(TailTarget.OpaquePath);

        if (c == '?')
        {
            _url.Query = string.Empty;
            _state = UrlParserState.Query;
            return StepResult.Continue;
        }

        if (c == '#')
        {
            _url.Fragment = string.Empty;
            _state = UrlParserState.Fragment;
            return StepResult.Continue;
        }

        if (c == ' ')
        {
            // A space is only percent-encoded when it is the last thing before the query or the fragment,
            // which is what keeps a trailing space out of an opaque path without a separate stripping step.
            if (RemainingStartsWith('?') || RemainingStartsWith('#'))
            {
                _tail.Append("%20");
            }
            else
            {
                _tail.Append(' ');
            }

            return StepResult.Continue;
        }

        if (c != Eof)
        {
            AppendEncodedCodePoint(ref _tail, PercentEncodeSet.C0Control);
        }

        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#query-state</summary>
    private StepResult QueryState(int c)
    {
        // The encoding is always UTF-8 here, so the state's first step — which switches a legacy encoding back
        // to UTF-8 for a non-special URL or a websocket one — has nothing to do.
        if (c == Eof || (_stateOverride is null && c == '#'))
        {
            var set = _url.IsSpecial ? PercentEncodeSet.SpecialQuery : PercentEncodeSet.Query;
            _url.Query = (_url.Query ?? string.Empty) + PercentEncoding.Encode(_buffer.AsSpan(), set);
            _buffer.Length = 0;

            if (c == '#')
            {
                _url.Fragment = string.Empty;
                _state = UrlParserState.Fragment;
            }

            return StepResult.Continue;
        }

        _buffer.Append((char) c);
        return StepResult.Continue;
    }

    /// <summary>https://url.spec.whatwg.org/#fragment-state</summary>
    private StepResult FragmentState(int c)
    {
        BeginTail(TailTarget.Fragment);

        if (c != Eof)
        {
            AppendEncodedCodePoint(ref _tail, PercentEncodeSet.Fragment);
        }

        return StepResult.Continue;
    }

    /// <summary>
    /// UTF-8 percent-encodes the code point at the pointer, consuming both halves of a surrogate pair so the
    /// scalar value is encoded once rather than as two lone halves.
    /// </summary>
    private void AppendEncodedCodePoint(ref ValueStringBuilder builder, PercentEncodeSet set)
    {
        var length = 1;
        if (char.IsHighSurrogate(_input[_pointer])
            && _pointer + 1 < _input.Length
            && char.IsLowSurrogate(_input[_pointer + 1]))
        {
            length = 2;
        }

        PercentEncoding.Append(ref builder, _input.AsSpan(_pointer, length), set);
        _pointer += length - 1;
    }

    private void BeginTail(TailTarget target)
    {
        if (_tailTarget == target)
        {
            return;
        }

        FlushTail();
        _tailTarget = target;
        if (_tail.Capacity == 0)
        {
            _tail = new ValueStringBuilder(64);
        }
    }

    private void FlushTail()
    {
        if (_tailTarget == TailTarget.None)
        {
            return;
        }

        var text = _tail.AsSpan().ToString();
        if (_tailTarget == TailTarget.OpaquePath)
        {
            _url.OpaquePath = (_url.OpaquePath ?? string.Empty) + text;
        }
        else
        {
            _url.Fragment = (_url.Fragment ?? string.Empty) + text;
        }

        _tail.Length = 0;
        _tailTarget = TailTarget.None;
    }

    private static char AsciiLower(char c) => (uint) (c - 'A') <= 'Z' - 'A' ? (char) (c | 0x20) : c;

    /// <summary>
    /// Brings the input into the shape the state machine expects: a scalar value string (WebIDL's USVString
    /// conversion, which replaces every unpaired surrogate with U+FFFD), with leading and trailing C0 controls
    /// or spaces removed when the parser is building a new URL, and every ASCII tab or newline removed always.
    /// </summary>
    private static string Sanitize(string input, bool trim)
    {
        var start = 0;
        var end = input.Length;

        if (trim)
        {
            while (start < end && UrlCharacters.IsC0ControlOrSpace(input[start]))
            {
                start++;
            }

            while (end > start && UrlCharacters.IsC0ControlOrSpace(input[end - 1]))
            {
                end--;
            }
        }

        var rewrite = false;
        for (var i = start; i < end; i++)
        {
            var c = input[i];
            if (UrlCharacters.IsTabOrNewline(c))
            {
                rewrite = true;
                break;
            }

            if (char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(input[i + 1]))
            {
                i++;
                continue;
            }

            if (char.IsSurrogate(c))
            {
                rewrite = true;
                break;
            }
        }

        if (!rewrite)
        {
            return start == 0 && end == input.Length ? input : input.Substring(start, end - start);
        }

        var builder = new ValueStringBuilder(end - start);
        try
        {
            for (var i = start; i < end; i++)
            {
                var c = input[i];
                if (UrlCharacters.IsTabOrNewline(c))
                {
                    continue;
                }

                if (char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(input[i + 1]))
                {
                    builder.Append(c);
                    builder.Append(input[i + 1]);
                    i++;
                    continue;
                }

                builder.Append(char.IsSurrogate(c) ? '\uFFFD' : c);
            }

            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }
}
#endif
