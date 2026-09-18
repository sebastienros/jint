using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Html;
using AngleSharp.Html.Construction;
using AngleSharp.Text;

namespace Jint.Browser.Dom;

/// <summary>https://dom.spec.whatwg.org/#concept-pi-initialize.</summary>
internal static class DomProcessingInstructions
{
    internal static IProcessingInstruction Create(IDocument document, string target, string data)
    {
        if (!IsXmlName(target, out var astral))
        {
            throw new DomException(DomError.InvalidCharacter);
        }

        if (target.Length == 0 || data.Contains("?>", StringComparison.Ordinal))
        {
            throw new DomException(DomError.InvalidCharacter);
        }

        if (!astral)
        {
            return document.CreateProcessingInstruction(target, data);
        }

        // The native factory tests UTF-16 code units and rejects valid astral XML Names. Use its
        // public parser construction seam to build the same native node in an inert scratch document.
        // The tokenizer retains '?' in PI data, so replace the token's public payload with the
        // validated target. Neither target nor data enters markup.
        using var source = new TextSource("<??>");
        using var tokenizer = new HtmlTokenizer(source, HtmlEntityProvider.Resolver)
        {
            IsSupportingProcessingInstructions = true,
        };
        ref var token = ref tokenizer.GetStructToken();
        token.Name = target + " ";
        using var scratch = new HtmlParser().ParseDocument(string.Empty);
        ((IConstructableDocument) scratch).AddComment(ref token);
        var instruction = (IProcessingInstruction) scratch.LastChild!;
        document.AdoptNode(instruction);
        instruction.Data = data;
        return instruction;
    }
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
