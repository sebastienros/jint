namespace Jint.HtmlParser;

internal enum HtmlRequiredState
{
    Inapplicable,
    Optional,
    Required
}

/// <summary>HTML Living Standard §4.15, required and optional form controls.</summary>
internal static class HtmlRequiredness
{
    internal static HtmlRequiredState GetState(Element element, ref HtmlDisabledWork work)
    {
        if (element.NamespaceUri != Namespaces.Html ||
            element.LocalName is not ("input" or "select" or "textarea"))
        {
            return HtmlRequiredState.Inapplicable;
        }

        string? type = null;
        var required = false;
        foreach (var attribute in element.Attributes)
        {
            work.Step();
            if (attribute.NamespaceUri is not null)
            {
                continue;
            }

            if (attribute.LocalName == "required")
            {
                required = true;
            }
            else if (element.LocalName == "input" && attribute.LocalName == "type")
            {
                type = attribute.Value;
            }
        }

        if (element.LocalName == "input" && !HtmlInputTypes.Info(HtmlInputTypes.Parse(type)).RequiredApplies)
        {
            return HtmlRequiredState.Inapplicable;
        }

        return required ? HtmlRequiredState.Required : HtmlRequiredState.Optional;
    }
}
