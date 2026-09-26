using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Model;

internal sealed partial class CssDeclarationBlock
{
    // Declaration effects only. No value grammar, substitution or computation is demanded.
    internal bool MayAffectProperty(string name, CssValueWork work)
    {
        work = ResolutionWork(work);
        name = CssPropertyRegistry.NormalizeName(name, work);
        var index = Index(work);
        var relevant = index.ContainsKey(name) || CssPropertyEffects.ResetByAll(name) && index.ContainsKey("all");
        work.CheckCancellation();
        return relevant;
    }
}
