namespace Jint.HtmlParser;

/// <summary>HTML §4.10.5: the three input checkedness flags, independent of value state.</summary>
internal sealed class HtmlInputCheckedState
{
    internal HtmlInputCheckedState(Element element, ref HtmlCheckedWork work)
    {
        Element = element;
        RefreshMetadata(ref work);
        // With no published sidecar there has been no explicit flag write or
        // radio exclusion. Materialize the logical default without a mutation.
        Checked = DefaultChecked;
    }
    internal void RefreshMetadata(ref HtmlCheckedWork work)
    {
        Attr? checkedAttribute = null;
        string? type = null;
        string? name = null;
        var required = false;
        for (uint i = 0; Element.GetAttributeAt(i) is { } attribute; i++)
        {
            work.Step();
            if (attribute.NamespaceUri is not null) continue;
            switch (attribute.LocalName)
            {
                case "type": type = attribute.Value; break;
                case "name": name = attribute.Value; break;
                case "checked": checkedAttribute = attribute; break;
                case "required": required = true; break;
            }
        }
        work.Check();
        Type = HtmlInputTypes.Parse(type);
        Name = name;
        CheckedAttribute = checkedAttribute;
        RegisteredRequired = required;
    }
    internal Element Element { get; }
    internal HtmlInputType Type { get; set; }
    internal string? Name { get; set; }
    internal Attr? CheckedAttribute { get; set; }
    internal bool Checked { get; private set; }
    internal bool DirtyCheckedness { get; private set; }
    internal bool Indeterminate { get; private set; }
    internal bool DefaultChecked => CheckedAttribute is not null;
    internal HtmlRadioGroupIndex? Index;
    internal HtmlRadioGroupIndex.Bucket? Group;
    internal bool RegisteredRequired;
    internal int MemberPosition = -1;
    internal int CheckedPosition = -1;

    internal void SetChecked(bool value, CancellationToken cancellationToken)
        => HtmlCheckednessAlgorithms.SetCore(this, value, dirty: true, cancellationToken);
    internal void SetDefaultChecked(bool value)
    {
        if (value) Element.SetAttribute("checked", "");
        else Element.RemoveAttributeNS(null, "checked");
    }
    internal void SetIndeterminate(bool value)
    {
        if (Indeterminate == value) return;
        Indeterminate = value;
        Element.OwnerDocument!.MarkMutation();
    }
    internal void Write(bool value, bool dirty)
    {
        if (Checked == value && DirtyCheckedness == dirty) return;
        Checked = value;
        DirtyCheckedness = dirty;
        HtmlRadioGroupIndex.CheckedChanged(this);
        Element.OwnerDocument!.MarkMutation();
    }
    internal void CopyFrom(HtmlInputCheckedState source)
    {
        Checked = source.Checked;
        DirtyCheckedness = source.DirtyCheckedness;
        Indeterminate = source.Indeterminate;
    }
}
