using System.Text.RegularExpressions;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

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
        Source = regExpForMatchingAllCharacters;
    }

    public Regex Value { get; set; } = null!;
    public string Source { get; set; }

    public string Flags
    {
        get => _flags;
        set
        {
            _flags = value;
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
                        FullUnicode = true;
                        break;
                    case 'v':
                        UnicodeSets = true;
                        break;
                }
            }
        }
    }

    public RegExpParseResult ParseResult { get; set; }

    public bool DotAll { get; private set; }
    public bool Global { get; private set; }
    public bool Indices { get; private set; }
    public bool IgnoreCase { get; private set; }
    public bool Multiline { get; private set; }
    public bool Sticky { get; private set; }
    public bool FullUnicode { get; private set; }
    public bool UnicodeSets { get; private set; }

    internal bool HasDefaultRegExpExec => Properties == null && Prototype is RegExpPrototype { HasDefaultExec: true };

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
