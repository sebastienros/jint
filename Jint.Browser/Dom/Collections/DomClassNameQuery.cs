using AngleSharp.Dom;

namespace Jint.Browser.Dom.Collections;

/// <summary>https://dom.spec.whatwg.org/#concept-getelementsbyclassname</summary>
internal static class DomClassNameQuery
{
    internal static IEnumerable<IElement> Find(INode root, string[] classes)
    {
        if (classes.Length == 0)
        {
            yield break;
        }

        // AngleSharp deliberately leaves runtime quirks matching to its consumer (#1319/#1321).
        // Read the root's current node document on each live read: adoption can change the mode.
        var quirks = (root as IDocument ?? root.Owner)?.CompatMode == "BackCompat";
        foreach (var element in root.Descendants<IElement>())
        {
            if (Matches(element.ClassList, classes, quirks))
            {
                yield return element;
            }
        }
    }

    private static bool Matches(ITokenList tokens, string[] classes, bool quirks)
    {
        foreach (var name in classes)
        {
            if (!quirks)
            {
                if (!tokens.Contains(name))
                {
                    return false;
                }

                continue;
            }

            var found = false;
            for (var i = 0; i < tokens.Length; i++)
            {
                if (EqualsIgnoreAsciiCase(tokens[i], name))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private static bool EqualsIgnoreAsciiCase(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            var a = left[i];
            var b = right[i];
            if (a != b && ((a | 0x20) is < 'a' or > 'z' || (a | 0x20) != (b | 0x20)))
            {
                return false;
            }
        }

        return true;
    }
}
