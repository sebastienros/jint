using System.Text.RegularExpressions;
using Jint.Native.Object;
using System.Text;
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

    public JsRegExp(Engine engine)
        : base(engine, ObjectClass.RegExp)
    {
        // The empty pattern, matching what its Source says; Intrinsics.RegExp.Construct builds any other.
        Source = regExpForMatchingAllCharacters;
        Matcher = RegExpParseCache.GetOrCompile(engine.Realm, "", "", System.Threading.Timeout.InfiniteTimeSpan);
    }

    /// <summary>The pattern text, escaped as <c>RegExp.prototype.source</c> reports it.</summary>
    public string Source { get; set; }

    /// <summary>
    /// The compiled ECMAScript matcher, shared with every other instance of the same pattern and flags.
    /// Null only for a host-supplied <see cref="HostRegex"/>.
    /// </summary>
    internal JintRegExpEngine? Matcher { get; set; }

    /// <summary>
    /// The conversion options the pattern was compiled under, which carry the match timeout every later
    /// match of it runs under; null leaves that on the engine's <see cref="Options.ConstraintOptions.RegexTimeout"/>.
    /// </summary>
    internal Engine.RegexConversionOptions? ConversionOptions { get; set; }

    /// <summary>
    /// A .NET <see cref="Regex"/> the host supplied directly, kept on .NET semantics (see
    /// <see cref="Options.InteropOptions.RegexConversion"/>). Null for every pattern written in script.
    /// </summary>
    internal Regex? HostRegex { get; set; }

    public string Flags
    {
        get => _flags;
        set
        {
            _flags = value;
            _canonicalFlags = null;

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

    private string? _canonicalFlags;

    /// <summary>
    /// The string <c>RegExp.prototype.flags</c> answers for this instance while the flag accessors are the
    /// built-in ones: the flags in the specification's fixed order ("dgimsuvy"), whatever order the
    /// pattern was written with. Built once per <see cref="Flags"/> assignment.
    /// https://tc39.es/ecma262/#sec-get-regexp.prototype.flags
    /// </summary>
    internal string CanonicalFlags => _canonicalFlags ??= BuildCanonicalFlags();

    private string BuildCanonicalFlags()
    {
        var builder = new ValueStringBuilder(stackalloc char[8]);
        if (Indices) builder.Append('d');
        if (Global) builder.Append('g');
        if (IgnoreCase) builder.Append('i');
        if (Multiline) builder.Append('m');
        if (DotAll) builder.Append('s');
        if (Unicode) builder.Append('u');
        if (UnicodeSets) builder.Append('v');
        if (Sticky) builder.Append('y');
        return builder.ToString();
    }

    /// <summary>Whether this instance wraps a <see cref="HostRegex"/> rather than a pattern written in script.</summary>
    internal bool IsHostRegex => HostRegex is not null;

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

    /// <summary>Whether this regex uses the .NET Regex engine (not the custom engine).</summary>
    internal bool UsesDotNetEngine => HostRegex is not null;

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
