using Jint.Browser.Geometry;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Dom.Svg;

/// <summary>A live SVG list; attribute replacement preserves item identities by index and detaches excess items.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#ListInterfaces</remarks>
internal sealed class JsSvgList : ArrayLikeObject
{
    private readonly ObjectInstance? _lengthGetter;
    internal JsSvgList(SvgAttribute attribute, bool readOnly) : base(attribute.Owner.Engine)
    {
        Attribute = attribute;
        ReadOnly = readOnly;
        Prototype = attribute.Owner.Prototype("SVG" + attribute.Kind);
        _lengthGetter = (Prototype.GetOwnProperty("length") as GetSetPropertyDescriptor)?.Get as ObjectInstance;
    }

    internal SvgAttribute Attribute { get; }
    internal bool ReadOnly { get; }
    public override uint Length { get { Attribute.Refresh(); return (uint) Attribute.Items.Count; } }
    protected override bool OwnsLength => false;
    protected override ObjectInstance? PristineLengthGetter => _lengthGetter;
    public override bool TryGetIndex(uint index, out JsValue value)
    {
        Attribute.Refresh();
        value = index < Attribute.Items.Count ? Attribute.Items[(int) index].Wrap(ReadOnly) : JsValue.Undefined;
        return index < Attribute.Items.Count;
    }

    private int Index(JsValue value, bool clamp)
    {
        var index = TypeConverter.ToUint32(value);
        Attribute.Refresh();
        if (clamp) return (int) Math.Min(index, (uint) Attribute.Items.Count);
        if (index >= Attribute.Items.Count) DomFailures.Refuse(Attribute.Owner.Dom, "SVGList", "IndexSizeError", "The SVG list index is out of range.");
        return (int) index;
    }

    internal JsValue Invoke(string method, JsValue[] args)
    {
        var owner = Attribute.Owner;
        SvgValues.Require(owner, args, method switch
        {
            "clear" or "consolidate" => 0,
            "insertItemBefore" or "replaceItem" => 2,
            _ => 1,
        });
        if (method == "getItem") return Attribute.Items[Index(args[0], false)].Wrap(ReadOnly);
        if (method == "createSVGTransformFromMatrix")
            return new SvgValueCell(owner, SvgValueKind.Transform, SvgTransforms.MatrixArgument(owner, args[0])).Wrap(false);
        SvgValues.Writable(owner, ReadOnly);
        Attribute.Refresh();
        if (method == "consolidate")
        {
            if (Attribute.Items.Count == 0) return JsValue.Null;
            var matrix = GeometryMatrix.Identity();
            foreach (var cell in Attribute.Items)
            {
                owner.Checkpoint();
                GeometryMatrix.PostMultiply(matrix, SvgTransforms.Matrix(cell.Data));
            }
            Clear();
            var consolidated = new SvgValueCell(owner, SvgValueKind.Transform, SvgTransforms.FromMatrix(matrix), Attribute);
            Attribute.Items.Add(consolidated);
            Attribute.Commit();
            return consolidated.Wrap(false);
        }
        if (method == "clear")
        {
            Clear();
            Attribute.Commit();
            return JsValue.Undefined;
        }
        if (method == "removeItem")
        {
            var index = Index(args[0], false);
            var cell = Attribute.Items[index];
            Attribute.Items.RemoveAt(index);
            cell.Attribute = null;
            Attribute.Commit();
            return cell.Wrap(false);
        }

        var item = Item(args[0]);
        var target = method is "insertItemBefore" or "replaceItem" ? Index(args[1], method == "insertItemBefore") : 0;
        item.Read();
        Attribute.Refresh();
        if (method == "initialize") Clear();
        // SVG 2 copies associated or read-only items, so a source list is never mutated here.
        if (item.Attribute is not null || IsReadOnly(args[0]))
            item = new SvgValueCell(owner, Attribute.ItemKind, item.Read());
        if (method == "replaceItem")
        {
            Attribute.Items[target].Attribute = null;
            Attribute.Items[target] = item;
        }
        else if (method == "insertItemBefore") Attribute.Items.Insert(Math.Min(target, Attribute.Items.Count), item);
        else Attribute.Items.Add(item);
        item.Attribute = Attribute;
        Attribute.Commit();
        return item.Wrap(false);
    }

    private void Clear()
    {
        foreach (var item in Attribute.Items) item.Attribute = null;
        Attribute.Items.Clear();
    }

    private SvgValueCell Item(JsValue value)
    {
        if (Attribute.ItemKind == SvgValueKind.String)
            return new SvgValueCell(Attribute.Owner, SvgValueKind.String, new SvgValueData(Text: TypeConverter.ToString(value)));
        var cell = value switch
        {
            JsSvgValue v => v.Cell,
            JsSvgTransform t => t.Cell,
            JsDomPoint { Binding: SvgCoordinates b } => b.Cell,
            JsDomPoint { Mutable: true } p when Attribute.ItemKind == SvgValueKind.Point => Point(p),
            _ => null,
        };
        if (cell is null || cell.Kind != Attribute.ItemKind) Throw.TypeError(Attribute.Owner.Realm, "The item has the wrong SVG type.");
        return cell!;
    }

    private SvgValueCell Point(JsDomPoint point)
    {
        var cell = new SvgValueCell(Attribute.Owner, SvgValueKind.Point, new SvgValueData(point.X, point.Y, point.Z, point.W));
        point.Binding = new SvgCoordinates(cell, false);
        cell.SetMutableWrapper(point);
        return cell;
    }

    private static bool IsReadOnly(JsValue value) => value switch
    {
        JsSvgValue v => v.ReadOnly,
        JsSvgTransform t => t.ReadOnly,
        JsDomPoint { Binding: SvgCoordinates b } => b.ReadOnly,
        _ => false,
    };
}
