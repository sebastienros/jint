using System.Buffers;
using System.Text.RegularExpressions;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.RegExp;

namespace Jint.Native;

public sealed class JsRegExp : ObjectInstance
{
    internal const string regExpForMatchingAllCharacters = "(?:)";
    internal static readonly JsString PropertyLastIndex = new("lastIndex");

    private string _flags = null!;

    private PropertyDescriptor _prototypeDescriptor = null!;

    // 0 = not determined yet, 1 = no hazard, 2 = hazard. Determined lazily on the first match rather
    // than at construction: a regexp literal caches its adaptation on the AST node and skips
    // RegExpConstructor entirely on every evaluation after the first, so there is no construction-time
    // hook that all of them pass through.
    private byte _caseFoldingHazard;
    private JintRegExpEngine? _caseFoldingFallbackEngine;

    // One-entry memo of the last subject probed for a trigger, so a match loop over one string
    // (`while (re.exec(s))`, @@match, @@replace) scans it once instead of once per match.
    private string? _lastProbedSubject;
    private bool _lastProbedSubjectHasTrigger;

    /// <summary>
    /// The non-ASCII code points that a case relation can connect to an ASCII one, and therefore the
    /// only subject characters over which .NET's IgnoreCase matching of an all-ASCII pattern can
    /// disagree with the specification's Canonicalize: U+0130 and U+0131 (dotted and dotless I),
    /// U+017F LATIN SMALL LETTER LONG S, and U+212A KELVIN SIGN. Which of the four any given target
    /// framework actually pairs with ASCII differs - .NET 7 and later pair U+212A with <c>k</c> and
    /// .NET Framework pairs none of them - so all four are listed and the set is a property of Unicode
    /// rather than of a framework version.
    /// </summary>
    internal static readonly SearchValues<char> CaseFoldingTriggers =
        SearchValues.Create("\u0130\u0131\u017F\u212A");

    public JsRegExp(Engine engine)
        : base(engine, ObjectClass.RegExp)
    {
        Source = regExpForMatchingAllCharacters;
    }

    public Regex Value { get; set; } = null!;
    public string Source { get; set; }

    /// <summary>
    /// Custom regex engine used when .NET Regex cannot handle the pattern.
    /// When set, this takes priority over <see cref="Value"/>.
    /// </summary>
    internal JintRegExpEngine? CustomEngine => ParseResult.ConversionResult as JintRegExpEngine;

    public string Flags
    {
        get => _flags;
        set
        {
            _flags = value;

            // Every construction and every compile() assigns Flags last, so this is the one place that
            // has to drop the lazily determined case-folding state along with the flags themselves.
            _caseFoldingHazard = 0;
            _caseFoldingFallbackEngine = null;
            _lastProbedSubject = null;

            // Reset all flags before parsing (needed for RegExp.prototype.compile re-initialization)
            DotAll = false;
            Global = false;
            Indices = false;
            IgnoreCase = false;
            Multiline = false;
            Sticky = false;
            Unicode = false;
            FullUnicode = false;
            UnicodeSets = false;
            foreach (var c in _flags)
            {
                switch (c)
                {
                    case 'd':
                        Indices = true;
                        break;
                    case 'i':
                        IgnoreCase = true;
                        break;
                    case 'm':
                        Multiline = true;
                        break;
                    case 'g':
                        Global = true;
                        break;
                    case 's':
                        DotAll = true;
                        break;
                    case 'y':
                        Sticky = true;
                        break;
                    case 'u':
                        Unicode = true;
                        FullUnicode = true;
                        break;
                    case 'v':
                        UnicodeSets = true;
                        FullUnicode = true; // v-flag implies unicode semantics
                        break;
                }
            }
        }
    }

    public RegExpParseResult ParseResult { get; set; }

    /// <summary>
    /// Whether this instance wraps a .NET <see cref="Regex"/> the host supplied directly (through
    /// <see cref="RegExp.RegExpConstructor.Construct(Regex, string, string)"/>, which the default interop
    /// converter routes every <see cref="Regex"/> through) instead of one adapted from a JavaScript
    /// pattern. Such an instance carries no <see cref="ParseResult"/>, so it always runs on the .NET
    /// engine and its capture-group metadata is read off <see cref="Value"/>.
    /// </summary>
    internal bool IsHostRegex { get; set; }

    public bool DotAll { get; private set; }
    public bool Global { get; private set; }
    public bool Indices { get; private set; }
    public bool IgnoreCase { get; private set; }
    public bool Multiline { get; private set; }
    public bool Sticky { get; private set; }
    /// <summary>Whether the 'u' flag was explicitly set (for the unicode accessor).</summary>
    public bool Unicode { get; private set; }
    /// <summary>Whether unicode semantics apply (true for both 'u' and 'v' flags).</summary>
    public bool FullUnicode { get; private set; }
    public bool UnicodeSets { get; private set; }

    internal bool HasDefaultRegExpExec => Properties == null && Prototype is RegExpPrototype { HasDefaultExec: true };

    /// <summary>
    /// Whether this match has to leave the .NET engine for the custom one because the pattern is
    /// case-insensitive in a way .NET resolves differently from the specification's Canonicalize, and
    /// this particular subject carries one of the <see cref="CaseFoldingTriggers"/> that makes the
    /// difference observable. Everything else stays on .NET: the scan is the only cost a pattern with
    /// the hazard pays on a subject without a trigger, and it is memoized per subject instance so a
    /// match loop over one string pays it once.
    /// <para>
    /// A host-supplied <see cref="Regex"/> is exempt - it is the host's own .NET pattern, not one
    /// adapted from JavaScript, so its casing rules are the host's to decide. So is a Unicode-mode
    /// pattern, which never reaches the .NET engine at all.
    /// </para>
    /// </summary>
    internal bool NeedsCaseFoldingFallback(string subject)
    {
        if (_caseFoldingHazard == 0)
        {
            _caseFoldingHazard = (byte) (IgnoreCase && !IsHostRegex && !FullUnicode
                && RegExp.RegExpConstructor.HasSubjectDependentCaseFoldingHazard(Source, _flags)
                ? 2
                : 1);
        }

        if (_caseFoldingHazard == 1)
        {
            return false;
        }

        if (!ReferenceEquals(subject, _lastProbedSubject))
        {
            _lastProbedSubject = subject;
            _lastProbedSubjectHasTrigger = subject.AsSpan().ContainsAny(CaseFoldingTriggers);
        }

        return _lastProbedSubjectHasTrigger;
    }

    /// <summary>
    /// The custom-engine compilation of this pattern, built on the first subject that needs it and
    /// reused afterwards. Never reached unless <see cref="NeedsCaseFoldingFallback"/> said so, so a
    /// pattern whose subjects never carry a trigger never pays the compilation.
    /// </summary>
    internal JintRegExpEngine GetCaseFoldingFallbackEngine(TimeSpan timeout)
        => _caseFoldingFallbackEngine ??=
            RegExp.RegExpConstructor.TryCompileWithCustomEngine(Engine.Realm, Source, _flags, timeout);

    /// <summary>Whether this regex uses the .NET Regex engine (not the custom engine).</summary>
    internal bool UsesDotNetEngine => IsHostRegex || ParseResult.ConversionResult is Regex;

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (PropertyLastIndex.Equals(property))
        {
            return _prototypeDescriptor ?? PropertyDescriptor.Undefined;
        }

        return base.GetOwnProperty(property);
    }

    protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (PropertyLastIndex.Equals(property))
        {
            _prototypeDescriptor = desc;
            return;
        }

        base.SetOwnProperty(property, desc);
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        if (_prototypeDescriptor != null)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(PropertyLastIndex, _prototypeDescriptor);
        }

        foreach (var entry in base.GetOwnProperties())
        {
            yield return entry;
        }
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = new List<JsValue>();
        if (_prototypeDescriptor != null)
        {
            keys.Add(PropertyLastIndex);
        }

        keys.AddRange(base.GetOwnPropertyKeys(types));
        return keys;
    }
}
