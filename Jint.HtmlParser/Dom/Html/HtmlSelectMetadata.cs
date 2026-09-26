namespace Jint.HtmlParser;

internal readonly record struct HtmlOptionMetadata(bool DefaultSelected, string? Value, string? Label,
    string? Id, string? Name)
{
    internal static HtmlOptionMetadata Read(IEnumerable<Attr> attributes, ref HtmlSelectWork work)
    {
        bool selected = false;
        string? value = null, label = null, id = null, name = null;
        foreach (var attribute in attributes)
        {
            work.Step();
            if (attribute.NamespaceUri is not null) continue;
            switch (attribute.LocalName)
            {
                case "selected": selected = true; break;
                case "value": value = attribute.Value; break;
                case "label": label = attribute.Value; break;
                case "id": id = attribute.Value; break;
                case "name": name = attribute.Value; break;
            }
        }
        work.Check();
        return new HtmlOptionMetadata(selected, value, label, id, name);
    }
}
internal readonly record struct HtmlSelectMetadata(bool Multiple, uint? Size)
{
    internal static HtmlSelectMetadata Read(IEnumerable<Attr> attributes, ref HtmlSelectWork work)
    {
        var multiple = false;
        string? size = null;
        foreach (var attribute in attributes)
        {
            work.Step();
            if (attribute.NamespaceUri is not null) continue;
            if (attribute.LocalName == "multiple") multiple = true;
            else if (attribute.LocalName == "size") size = attribute.Value;
        }
        var parsed = HtmlSelectState.ParseSize(size, ref work);
        work.Check();
        return new HtmlSelectMetadata(multiple, parsed);
    }
}
internal readonly record struct HtmlSelectInitialization(HtmlOptionMetadata? Option, HtmlSelectMetadata? Select);
