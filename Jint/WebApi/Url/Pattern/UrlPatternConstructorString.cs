#if NET8_0_OR_GREATER
using Jint.Runtime;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// The constructor string parser of https://urlpattern.spec.whatwg.org/#constructor-string-parsing — splits a
/// URL-shaped pattern such as "<c>https://*.example.com/:page?</c>" into per-component pattern strings.
/// </summary>
/// <remarks>
/// <para>
/// It resembles the basic URL parser and deliberately is not one. It runs over <see cref="UrlPatternToken"/>s
/// produced with the lenient policy rather than over code points, so a "<c>:hmm</c>" name cannot be mistaken for
/// a port; it applies no canonicalization, because which code points are fixed text is not known until each
/// component is compiled; and it handles no backslash escaping or file-URL host quirks, since a pattern that
/// needs those is better written as a <c>URLPatternInit</c>.
/// </para>
/// <para>
/// Two shorthands fall out of it. Components after the last one written are wildcards — "<c>https://example.com</c>"
/// matches any path, search and hash on that origin — while a hostname written without a port pins the port to
/// the scheme's default, so matching any port takes an explicit "<c>:*</c>".
/// </para>
/// </remarks>
internal static class UrlPatternConstructorString
{
    /// <summary>https://urlpattern.spec.whatwg.org/#constructor-string-parser</summary>
    private enum ParserState
    {
        Init,
        Protocol,
        Authority,
        Username,
        Password,
        Hostname,
        Port,
        Pathname,
        Search,
        Hash,
        Done,
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#parse-a-constructor-string
    /// </summary>
    internal static UrlPatternInit Parse(Engine engine, string input)
    {
        var parser = new ConstructorStringParser(engine, input);
        parser.Run();
        return parser.Result;
    }

    private sealed class ConstructorStringParser(Engine engine, string input)
    {
        private readonly Engine _engine = engine;
        private readonly string _input = input;
        private readonly List<UrlPatternToken> _tokenList = UrlPatternTokenizer.Tokenize(engine.Realm, input, strict: false);
        private ParserState _state = ParserState.Init;
        private int _componentStart;
        private int _tokenIndex;
        private int _tokenIncrement = 1;
        private int _groupDepth;
        private int _hostnameIpv6BracketDepth;
        private bool _protocolMatchesSpecialScheme;

        internal UrlPatternInit Result { get; } = new();

        internal void Run()
        {
            while (_tokenIndex < _tokenList.Count)
            {
                _tokenIncrement = 1;

                if (_tokenList[_tokenIndex].Type == UrlPatternTokenType.End)
                {
                    if (_state == ParserState.Init)
                    {
                        // No protocol terminator was found, so this is a relative pattern; where it begins is
                        // decided by whether it starts with a hash or a search prefix.
                        Rewind();

                        if (IsHashPrefix())
                        {
                            ChangeState(ParserState.Hash, 1);
                        }
                        else if (IsSearchPrefix())
                        {
                            ChangeState(ParserState.Search, 1);
                        }
                        else
                        {
                            ChangeState(ParserState.Pathname, 0);
                        }

                        _tokenIndex += _tokenIncrement;
                        continue;
                    }

                    if (_state == ParserState.Authority)
                    {
                        // No "@" was found, so there is no username or password after all.
                        RewindAndSetState(ParserState.Hostname);
                        _tokenIndex += _tokenIncrement;
                        continue;
                    }

                    ChangeState(ParserState.Done, 0);
                    break;
                }

                if (_tokenList[_tokenIndex].Type == UrlPatternTokenType.Open)
                {
                    // Everything inside "{ ... }" is one grouping, so a component boundary cannot lie within it.
                    _groupDepth++;
                    _tokenIndex += _tokenIncrement;
                    continue;
                }

                if (_groupDepth > 0)
                {
                    if (_tokenList[_tokenIndex].Type == UrlPatternTokenType.Close)
                    {
                        _groupDepth--;
                    }
                    else
                    {
                        _tokenIndex += _tokenIncrement;
                        continue;
                    }
                }

                Step();
                _tokenIndex += _tokenIncrement;
            }

            if (Result.Hostname is not null && Result.Port is null)
            {
                // An author who named a host and no port meant the default port; "any port" is written ":*".
                Result.Port = string.Empty;
            }
        }

        private void Step()
        {
            switch (_state)
            {
                case ParserState.Init:
                    if (IsProtocolSuffix())
                    {
                        RewindAndSetState(ParserState.Protocol);
                    }

                    break;

                case ParserState.Protocol:
                    if (IsProtocolSuffix())
                    {
                        // The protocol has to be compiled here and now: whether it matches a special scheme
                        // decides both the default pathname and whether an authority is looked for at all.
                        ComputeProtocolMatchesSpecialScheme();

                        var nextState = ParserState.Pathname;
                        var skip = 1;

                        if (NextIsAuthoritySlashes())
                        {
                            nextState = ParserState.Authority;
                            skip = 3;
                        }
                        else if (_protocolMatchesSpecialScheme)
                        {
                            nextState = ParserState.Authority;
                        }

                        ChangeState(nextState, skip);
                    }

                    break;

                case ParserState.Authority:
                    if (IsIdentityTerminator())
                    {
                        RewindAndSetState(ParserState.Username);
                    }
                    else if (IsPathnameStart() || IsSearchPrefix() || IsHashPrefix())
                    {
                        RewindAndSetState(ParserState.Hostname);
                    }

                    break;

                case ParserState.Username:
                    if (IsPasswordPrefix())
                    {
                        ChangeState(ParserState.Password, 1);
                    }
                    else if (IsIdentityTerminator())
                    {
                        ChangeState(ParserState.Hostname, 1);
                    }

                    break;

                case ParserState.Password:
                    if (IsIdentityTerminator())
                    {
                        ChangeState(ParserState.Hostname, 1);
                    }

                    break;

                case ParserState.Hostname:
                    if (IsIpv6Open())
                    {
                        _hostnameIpv6BracketDepth++;
                    }
                    else if (IsIpv6Close())
                    {
                        _hostnameIpv6BracketDepth--;
                    }
                    else if (IsPortPrefix() && _hostnameIpv6BracketDepth == 0)
                    {
                        ChangeState(ParserState.Port, 1);
                    }
                    else if (IsPathnameStart())
                    {
                        ChangeState(ParserState.Pathname, 0);
                    }
                    else if (IsSearchPrefix())
                    {
                        ChangeState(ParserState.Search, 1);
                    }
                    else if (IsHashPrefix())
                    {
                        ChangeState(ParserState.Hash, 1);
                    }

                    break;

                case ParserState.Port:
                    if (IsPathnameStart())
                    {
                        ChangeState(ParserState.Pathname, 0);
                    }
                    else if (IsSearchPrefix())
                    {
                        ChangeState(ParserState.Search, 1);
                    }
                    else if (IsHashPrefix())
                    {
                        ChangeState(ParserState.Hash, 1);
                    }

                    break;

                case ParserState.Pathname:
                    if (IsSearchPrefix())
                    {
                        ChangeState(ParserState.Search, 1);
                    }
                    else if (IsHashPrefix())
                    {
                        ChangeState(ParserState.Hash, 1);
                    }

                    break;

                case ParserState.Search:
                    if (IsHashPrefix())
                    {
                        ChangeState(ParserState.Hash, 1);
                    }

                    break;

                default:
                    // The hash state consumes everything that is left, and the done state is never stepped.
                    break;
            }
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#change-state</summary>
        private void ChangeState(ParserState newState, int skip)
        {
            if (_state is not (ParserState.Init or ParserState.Authority or ParserState.Done))
            {
                SetResult(_state, MakeComponentString());
            }

            if (_state != ParserState.Init && newState != ParserState.Done)
            {
                // Leaving the authority behind without having written a hostname means there was none; the same
                // reasoning gives an empty pathname and search to a pattern that stops before reaching them, which
                // is what makes "https://example.com/foo" match any search and any hash but only that path.
                if (_state is ParserState.Protocol or ParserState.Authority or ParserState.Username or ParserState.Password
                    && newState is ParserState.Port or ParserState.Pathname or ParserState.Search or ParserState.Hash
                    && Result.Hostname is null)
                {
                    Result.Hostname = string.Empty;
                }

                if (_state is ParserState.Protocol or ParserState.Authority or ParserState.Username or ParserState.Password
                        or ParserState.Hostname or ParserState.Port
                    && newState is ParserState.Search or ParserState.Hash
                    && Result.Pathname is null)
                {
                    Result.Pathname = _protocolMatchesSpecialScheme ? "/" : string.Empty;
                }

                if (_state is ParserState.Protocol or ParserState.Authority or ParserState.Username or ParserState.Password
                        or ParserState.Hostname or ParserState.Port or ParserState.Pathname
                    && newState == ParserState.Hash
                    && Result.Search is null)
                {
                    Result.Search = string.Empty;
                }
            }

            _state = newState;
            _tokenIndex += skip;
            _componentStart = _tokenIndex;
            _tokenIncrement = 0;
        }

        private void SetResult(ParserState state, string value)
        {
            switch (state)
            {
                case ParserState.Protocol:
                    Result.Protocol = value;
                    break;
                case ParserState.Username:
                    Result.Username = value;
                    break;
                case ParserState.Password:
                    Result.Password = value;
                    break;
                case ParserState.Hostname:
                    Result.Hostname = value;
                    break;
                case ParserState.Port:
                    Result.Port = value;
                    break;
                case ParserState.Pathname:
                    Result.Pathname = value;
                    break;
                case ParserState.Search:
                    Result.Search = value;
                    break;
                default:
                    Result.Hash = value;
                    break;
            }
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#rewind</summary>
        private void Rewind()
        {
            _tokenIndex = _componentStart;
            _tokenIncrement = 0;
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#rewind-and-set-state</summary>
        private void RewindAndSetState(ParserState state)
        {
            Rewind();
            _state = state;
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#get-a-safe-token</summary>
        private UrlPatternToken GetSafeToken(int index)
            => index < _tokenList.Count ? _tokenList[index] : _tokenList[_tokenList.Count - 1];

        /// <summary>https://urlpattern.spec.whatwg.org/#is-a-non-special-pattern-char</summary>
        private bool IsNonSpecialPatternChar(int index, char value)
        {
            var token = GetSafeToken(index);
            if (token.Value.Length != 1 || token.Value[0] != value)
            {
                return false;
            }

            return token.Type is UrlPatternTokenType.Char or UrlPatternTokenType.EscapedChar or UrlPatternTokenType.InvalidChar;
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#is-a-protocol-suffix</summary>
        private bool IsProtocolSuffix() => IsNonSpecialPatternChar(_tokenIndex, ':');

        /// <summary>https://urlpattern.spec.whatwg.org/#next-is-authority-slashes</summary>
        private bool NextIsAuthoritySlashes()
            => IsNonSpecialPatternChar(_tokenIndex + 1, '/') && IsNonSpecialPatternChar(_tokenIndex + 2, '/');

        /// <summary>https://urlpattern.spec.whatwg.org/#is-an-identity-terminator</summary>
        private bool IsIdentityTerminator() => IsNonSpecialPatternChar(_tokenIndex, '@');

        /// <summary>https://urlpattern.spec.whatwg.org/#is-a-password-prefix</summary>
        private bool IsPasswordPrefix() => IsNonSpecialPatternChar(_tokenIndex, ':');

        /// <summary>https://urlpattern.spec.whatwg.org/#is-a-port-prefix</summary>
        private bool IsPortPrefix() => IsNonSpecialPatternChar(_tokenIndex, ':');

        /// <summary>https://urlpattern.spec.whatwg.org/#is-a-pathname-start</summary>
        private bool IsPathnameStart() => IsNonSpecialPatternChar(_tokenIndex, '/');

        /// <summary>https://urlpattern.spec.whatwg.org/#is-a-hash-prefix</summary>
        private bool IsHashPrefix() => IsNonSpecialPatternChar(_tokenIndex, '#');

        /// <summary>https://urlpattern.spec.whatwg.org/#is-an-ipv6-open</summary>
        private bool IsIpv6Open() => IsNonSpecialPatternChar(_tokenIndex, '[');

        /// <summary>https://urlpattern.spec.whatwg.org/#is-an-ipv6-close</summary>
        private bool IsIpv6Close() => IsNonSpecialPatternChar(_tokenIndex, ']');

        /// <summary>
        /// https://urlpattern.spec.whatwg.org/#is-a-search-prefix — a "<c>?</c>" only starts the search when it is
        /// not the modifier of the group in front of it.
        /// </summary>
        private bool IsSearchPrefix()
        {
            if (IsNonSpecialPatternChar(_tokenIndex, '?'))
            {
                return true;
            }

            var token = _tokenList[_tokenIndex];
            if (token.Value.Length != 1 || token.Value[0] != '?')
            {
                return false;
            }

            var previousIndex = _tokenIndex - 1;
            if (previousIndex < 0)
            {
                return true;
            }

            return GetSafeToken(previousIndex).Type
                is not (UrlPatternTokenType.Name or UrlPatternTokenType.Regexp
                    or UrlPatternTokenType.Close or UrlPatternTokenType.Asterisk);
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#make-a-component-string</summary>
        private string MakeComponentString()
        {
            var token = _tokenList[_tokenIndex];
            var componentStartInputIndex = GetSafeToken(_componentStart).Index;
            return _input.Substring(componentStartInputIndex, token.Index - componentStartInputIndex);
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#compute-protocol-matches-a-special-scheme-flag</summary>
        private void ComputeProtocolMatchesSpecialScheme()
        {
            var protocolComponent = UrlPatternComponent.Compile(
                _engine,
                MakeComponentString(),
                UrlPatternCanonicalization.CanonicalizeProtocol,
                UrlPatternCompileOptions.Default());

            if (protocolComponent.MatchesSpecialScheme())
            {
                _protocolMatchesSpecialScheme = true;
            }
        }
    }
}
#endif
