using System.Text;

namespace Jint.HtmlParser.Css.Values.Transforms;

internal static class CssTransformListSerializer
{
    internal static string Serialize(CssTransformList list, CssValueWork work)
    {
        work.CheckCancellation();
        var text = new StringBuilder();
        for (var i = 0; i < list.Count; i++)
        {
            work.Charge(1);
            var function = list[i];
            if (i != 0) text.Append(' ');
            text.Append(function.Descriptor.Name).Append('(');
            for (var j = 0; j < function.Arguments.Count; j++)
            {
                var value = function.Arguments[j].Serialize();
                work.Charge(value.Length);
                if (j != 0) text.Append(", ");
                text.Append(value);
            }
            text.Append(')');
        }
        var result = text.ToString();
        work.CheckCancellation();
        return result;
    }
}
