using Jint.Browser.Dom;
using Jint.HtmlParser.Css.Model;

namespace Jint.Browser.Styling;

internal static partial class NativeCssBindings
{
    internal static string ContainerName(DomRealm realm, CssContainerRule rule)
    {
        var work = Work(realm);
        work.Charge(rule.ContainerName.Length);
        work.CheckCancellation();
        return rule.ContainerName;
    }

    internal static string ContainerQuery(DomRealm realm, CssContainerRule rule)
    {
        var work = Work(realm);
        work.Charge(rule.ContainerQuery.Length);
        work.CheckCancellation();
        return rule.ContainerQuery;
    }
}
