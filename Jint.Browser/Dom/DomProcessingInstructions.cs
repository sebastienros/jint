using AngleSharp.Text;

namespace Jint.Browser.Dom;

/// <summary>https://dom.spec.whatwg.org/#concept-pi-initialize.</summary>
internal static class DomProcessingInstructions
{
    // The native factory validates XML scalar values, including supplementary Names.
    // https://dom.spec.whatwg.org/#dom-document-createprocessinginstruction
    internal static Jint.HtmlParser.ProcessingInstruction Create(Jint.HtmlParser.Document document, string target, string data)
        => document.CreateProcessingInstruction(target, data);

    // XML 1.0 Fifth Edition Name, including supplementary scalar values.
    internal static bool IsXmlName(string target, out bool astral)
    {
        astral = false;
        for (var i = 0; i < target.Length; i++)
        {
            var c = target[i];
            if (char.IsHighSurrogate(c) && i + 1 < target.Length && char.IsLowSurrogate(target[i + 1]))
            {
                if (char.ConvertToUtf32(c, target[++i]) > 0xEFFFF)
                {
                    return false;
                }
                astral = true;
            }
            else if (!(i == 0 ? c.IsXmlNameStart() : c.IsXmlName()))
            {
                return false;
            }
        }

        return target.Length != 0;
    }

}
