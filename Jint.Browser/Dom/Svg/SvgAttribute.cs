using System.Globalization;
using Jint.HtmlParser;
using Jint.HtmlParser.Svg;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom.Svg;

/// <summary>A string-keyed parse cache and the serialization boundary for one live SVG content attribute.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#SVGDOMOverview</remarks>
internal sealed class SvgAttribute
{
    private string? _source;
    private bool _parsed;
    private readonly string _fallback;
    internal readonly string[]? Enumeration;
    private JsSvgAnimated? _animated;
    private JsSvgList? _baseList;
    private JsSvgList? _animList;
    internal readonly List<SvgValueCell> Items = [];

    internal SvgAttribute(SvgRealm owner, Element element, string name, SvgValueKind kind, string fallback,
        SvgLengthDirection direction, string? enumeration)
    {
        Owner = owner;
        Element = element;
        Name = name;
        Kind = kind;
        _fallback = fallback;
        Direction = direction;
        Enumeration = enumeration?.Split('|');
    }

    internal SvgRealm Owner { get; }
    internal Element Element { get; }
    internal string Name { get; }
    internal SvgValueKind Kind { get; }
    internal SvgLengthDirection Direction { get; }
    internal bool IsList => Kind is SvgValueKind.LengthList or SvgValueKind.NumberList or SvgValueKind.PointList
        or SvgValueKind.TransformList or SvgValueKind.StringList;
    internal SvgValueKind ItemKind => Kind switch
    {
        SvgValueKind.LengthList => SvgValueKind.Length,
        SvgValueKind.NumberList => SvgValueKind.Number,
        SvgValueKind.PointList => SvgValueKind.Point,
        SvgValueKind.TransformList => SvgValueKind.Transform,
        SvgValueKind.StringList => SvgValueKind.String,
        _ => Kind,
    };

    internal JsValue Animated => _animated ??= new JsSvgAnimated(this);
    internal JsSvgList List(bool readOnly) => readOnly
        ? _animList ??= new JsSvgList(this, true)
        : _baseList ??= new JsSvgList(this, false);

    internal string Source => Element.GetAttribute(Name) ??
        (Name == "href" ? Element.GetAttributeNS("http://www.w3.org/1999/xlink", "href") : null) ?? _fallback;

    internal void Refresh()
    {
        var source = Source;
        if (_parsed && string.Equals(source, _source, StringComparison.Ordinal)) return;
        var data = Parse(source);
        _parsed = true;
        _source = source;
        for (var i = data.Length; i < Items.Count; i++) Items[i].Attribute = null;
        if (Items.Count > data.Length) Items.RemoveRange(data.Length, Items.Count - data.Length);
        for (var i = 0; i < data.Length; i++)
        {
            if (i < Items.Count) Items[i].Data = data[i];
            else Items.Add(new SvgValueCell(Owner, ItemKind, data[i], this));
        }
    }

    private SvgValueData[] Parse(string source)
    {
        var check = Owner.Checkpoint;
        switch (Kind)
        {
            case SvgValueKind.Length:
                var length = SvgParser.ParseLength(source, check) ?? SvgParser.ParseLength(_fallback, check) ?? new SvgLength();
                return [new(length.Value, Unit: (ushort) length.Unit)];
            case SvgValueKind.Angle:
                return [SvgValues.ParseAngle(source) ?? default];
            case SvgValueKind.Rect:
                var box = SvgParser.ParseViewBox(source, check) ?? default;
                return [new(box.X, box.Y, box.Width, box.Height)];
            case SvgValueKind.PreserveAspectRatio:
                var aspect = SvgParser.ParsePreserveAspectRatio(source, check) ?? new SvgAspectRatio(6, 1);
                return [new(aspect.MeetOrSlice, Unit: aspect.Align)];
            case SvgValueKind.LengthList:
                return (SvgParser.ParseLengthList(source, check) ?? []).Select(static l => new SvgValueData(l.Value, Unit: (ushort) l.Unit)).ToArray();
            case SvgValueKind.NumberList:
                return (SvgParser.ParseNumberList(source, check) ?? []).Select(static n => new SvgValueData(n)).ToArray();
            case SvgValueKind.PointList:
                return (SvgParser.ParsePoints(source, check) ?? []).Select(static p => new SvgValueData(p.X, p.Y, D: 1)).ToArray();
            case SvgValueKind.TransformList:
                return SvgParser.ParseTransformList(source, check).Select(static t =>
                    new SvgValueData(t.A, t.B, t.C, t.D, t.E, t.F, (ushort) t.Kind)).ToArray();
            case SvgValueKind.StringList:
                return source.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries)
                    .Select(static s => new SvgValueData(Text: s)).ToArray();
            case SvgValueKind.String:
                return [new(Text: source)];
            case SvgValueKind.Boolean:
                return [new(source == "true" ? 1 : 0)];
            case SvgValueKind.Enumeration:
                var index = Enumeration is null ? -1 : Array.IndexOf(Enumeration, source);
                if (Name == "orient" && SvgValues.ParseAngle(source) is not null) index = 2;
                return [new(Math.Max(0, index))];
            case SvgValueKind.Integer:
                return [new(int.TryParse(source, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer) ? integer : 0)];
            default:
                var percent = Name == "offset" && source.TrimEnd().EndsWith('%');
                var number = SvgParser.ParseNumber(percent ? source.TrimEnd()[..^1] : source, check)
                    ?? SvgParser.ParseNumber(_fallback, check) ?? 0;
                return [new(percent ? number / 100 : number)];
        }
    }

    internal JsValue Read(bool readOnly)
    {
        Refresh();
        if (IsList) return List(readOnly);
        var cell = Items[0];
        return Kind switch
        {
            SvgValueKind.String => JsString.Create(cell.Data.Text ?? ""),
            SvgValueKind.Boolean => JsBoolean.Create(cell.Data.A != 0),
            SvgValueKind.Number or SvgValueKind.Integer or SvgValueKind.Enumeration => JsNumber.Create(cell.Data.A),
            _ => cell.Wrap(readOnly),
        };
    }

    internal JsValue Assign(JsValue value)
    {
        string text;
        switch (Kind)
        {
            case SvgValueKind.String:
                text = TypeConverter.ToString(value);
                break;
            case SvgValueKind.Boolean:
                text = TypeConverter.ToBoolean(value) ? "true" : "false";
                break;
            case SvgValueKind.Integer:
                text = TypeConverter.ToInt32(value).ToString(CultureInfo.InvariantCulture);
                break;
            case SvgValueKind.Enumeration:
                var index = TypeConverter.ToUint16(value);
                if (Enumeration is null || index == 0 || index >= Enumeration.Length)
                    Throw.TypeError(Owner.Realm, "Invalid SVG enumeration value.");
                text = Enumeration![index];
                break;
            default:
                text = SvgValues.Format(SvgValues.Number(Owner, value));
                break;
        }
        Write(text);
        return JsValue.Undefined;
    }

    internal void Commit()
    {
        var text = IsList
            ? string.Join(" ", Items.Select(cell => SvgValues.Serialize(ItemKind, cell.Data)))
            : SvgValues.Serialize(Kind, Items[0].Data);
        // Keep list item identities on our own write, but reparse if a native completion callback changes it.
        _source = text;
        _parsed = true;
        Write(text);
    }

    private void Write(string text)
    {
        DomFailures.PrepareMutation(Owner.Dom, Element);
        using var mutation = Owner.Dom.MutateLayout();
        Element.SetAttribute(Name, text);
        DomFailures.CompleteMutation(Owner.Dom, Element);
    }
}

/// <summary>The small, engine-free payload shared by the scalar and list tear-offs.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#SVGDOMOverview</remarks>
internal readonly record struct SvgValueData(double A = 0, double B = 0, double C = 0, double D = 0,
    double E = 0, double F = 0, ushort Unit = 0, string? Text = null);

/// <summary>An associated value, detached with its last value when its list removes it or shrinks past it.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#ListInterfaces</remarks>
internal sealed class SvgValueCell(SvgRealm owner, SvgValueKind kind, SvgValueData data, SvgAttribute? attribute = null)
{
    private JsValue? _mutable;
    private JsValue? _readOnly;
    internal SvgRealm Owner { get; } = owner;
    internal SvgValueKind Kind { get; } = kind;
    internal SvgValueData Data = data;
    internal SvgAttribute? Attribute = attribute;
    internal void SetMutableWrapper(JsValue value) => _mutable = value;

    internal SvgValueData Read()
    {
        Attribute?.Refresh();
        return Data;
    }

    internal void Write(SvgValueData value, bool readOnly)
    {
        SvgValues.Writable(Owner, readOnly);
        Attribute?.Refresh();
        Data = value;
        Attribute?.Commit();
    }

    internal JsValue Wrap(bool readOnly) => readOnly
        ? _readOnly ??= Create(true)
        : _mutable ??= Create(false);

    private JsValue Create(bool readOnly) => Kind switch
    {
        SvgValueKind.Point => new Geometry.JsDomPoint(Owner.Dom.Geometry, true, 0, 0, 0, 1)
        {
            Binding = new SvgCoordinates(this, readOnly),
        },
        SvgValueKind.Rect => new Geometry.JsDomRect(Owner.Dom.Geometry, true, 0, 0, 0, 0)
        {
            Binding = new SvgCoordinates(this, readOnly),
        },
        SvgValueKind.Transform => new JsSvgTransform(this, readOnly),
        SvgValueKind.String => JsString.Create(Read().Text ?? ""),
        _ => new JsSvgValue(this, readOnly),
    };
}
