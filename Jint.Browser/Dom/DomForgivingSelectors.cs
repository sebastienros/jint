using System.Text;
using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp.Dom;

namespace Jint.Browser.Dom;

/// <summary>
/// Selectors’ forgiving-selector-list algorithm: parse each branch of a forgiving selector list independently, discarding invalid
/// branches. AngleSharp 1.8.2 instead either rejects the entire list or drops only its unknown pseudo.
/// This component-value walk finds list boundaries; the native parser owns selector grammar and matching.
/// </summary>
internal static class DomForgivingSelectors
{
    internal static bool MayNeedNormalization(string text)
        => text.Contains('(') && (text.Contains(":is", StringComparison.OrdinalIgnoreCase)
                                 || text.Contains(":where", StringComparison.OrdinalIgnoreCase)
                                 || (text.Contains('\\') || text.Contains("/*", StringComparison.Ordinal)) && text.Contains(':'));

    internal static string Normalize(INode root, string text)
    {
        if (!MayNeedNormalization(text))
        {
            return text;
        }
        var context = root.Owner?.Context ?? ((IDocument) root).Context;
        var parser = context.GetService<ICssSelectorParser>()
                     ?? throw new InvalidOperationException("AngleSharp did not provide a selector parser.");
        var result = Rewrite(text, parser, out var valid);
        if (!valid)
        {
            throw new DomException(DomError.Syntax);
        }
        return DomSelectorText.Scan(result);
    }

    private static string Rewrite(string text, ICssSelectorParser parser, out bool valid)
    {
        // An explicit list processed inside-out avoids adding managed recursion or a semantic depth
        // cutoff. Each node represents only a function's component-value boundaries, not selector AST.
        var functions = new List<Function>();
        var roots = new List<Function>();
        var ancestors = new Stack<Function>();
        var hasForgivingFunction = false;
        for (var i = 0; i < text.Length; i++)
        {
            while (ancestors.Count > 0 && ancestors.Peek().End <= i)
            {
                ancestors.Pop();
            }
            if (SkipToken(text, ref i))
            {
                continue;
            }
            if (text[i] is '[' or '{')
            {
                i = Closing(text, i);
                continue;
            }
            if (text[i] != ':')
            {
                continue;
            }
            var nameStart = SkipComments(text, i + 1);
            var pseudoElement = nameStart < text.Length && text[nameStart] == ':';
            if (pseudoElement)
            {
                nameStart = SkipComments(text, nameStart + 1);
            }
            var end = nameStart;
            while (end < text.Length)
            {
                var c = text[end];
                if (c == '\\')
                {
                    end = DomSelectorText.EndOfEscape(text, end) + 1;
                }
                else if (char.IsAsciiLetterOrDigit(c) || c is '_' or '-' || c >= 0x80)
                {
                    end++;
                }
                else
                {
                    break;
                }
            }
            if (end >= text.Length || text[end] != '(')
            {
                continue;
            }
            var close = Closing(text, end);
            if (close >= text.Length)
            {
                continue; // Enclosing grammar remains the native parser's responsibility.
            }
            var name = text[nameStart..end];
            if (name.Contains('\\'))
            {
                var visitor = new NameVisitor();
                parser.ParseSelector("." + name)?.Accept(visitor);
                name = visitor.Name ?? name;
            }
            var function = new Function(i, end + 1, close + 1, name, pseudoElement);
            if (ancestors.TryPeek(out var parent))
            {
                parent.Children.Add(function);
                function.InHas = parent.InHas || parent.IsHas;
            }
            else
            {
                roots.Add(function);
            }
            hasForgivingFunction |= function.Forgiving;
            functions.Add(function);
            ancestors.Push(function);
            i = end;
        }
        if (!hasForgivingFunction)
        {
            valid = true;
            return text;
        }
        for (var i = functions.Count - 1; i >= 0; i--)
        {
            var function = functions[i];
            function.Valid = !function.IsHas || !function.InHas;
            var argumentEnd = function.End - 1;
            var argument = text[function.ArgumentStart..argumentEnd];
            if (function.Forgiving || function.Strict)
            {
                var kept = new List<string>();
                var offset = function.ArgumentStart;
                foreach (var branch in Branches(argument))
                {
                    var rewritten = Render(text, offset, offset + branch.Length, function.Children, out var branchValid);
                    branchValid &= IsValid(rewritten, parser, function.IsHas);
                    if (branchValid)
                    {
                        kept.Add(rewritten);
                    }
                    else if (function.Strict)
                    {
                        function.Valid = false;
                    }
                    offset += branch.Length + 1;
                }
                argument = string.Join(",", kept);
            }
            else
            {
                argument = Render(text, function.ArgumentStart, argumentEnd, function.Children, out var childrenValid);
                function.Valid &= childrenValid;
            }
            function.Replacement = string.Concat(text.AsSpan(function.Start, function.ArgumentStart - function.Start), argument, ")");
            if (!function.Forgiving && !function.Strict)
            {
                // Native child-index selectors expose their strict `of` selector through the visitor;
                // unlike logical functions, it need not be reconstructed from a serialized predicate.
                function.Valid &= function.PseudoElement
                    ? parser.ParseSelector(function.Replacement) is not null
                    : IsValid(function.Replacement, parser, relative: false);
            }
        }
        return Render(text, 0, text.Length, roots, out valid);
    }

    private static string Render(string text, int start, int end, List<Function> children, out bool valid)
    {
        valid = true;
        StringBuilder? builder = null;
        var copied = start;
        foreach (var child in children)
        {
            if (child.Start < start || child.End > end)
            {
                continue;
            }
            valid &= child.Valid;
            builder ??= new StringBuilder(end - start);
            builder.Append(text, copied, child.Start - copied).Append(child.Replacement);
            copied = child.End;
        }
        return builder is null ? text[start..end] : builder.Append(text, copied, end - copied).ToString();
    }

    private sealed class Function(int start, int argumentStart, int end, string name, bool pseudoElement)
    {
        internal int Start { get; } = start;
        internal int ArgumentStart { get; } = argumentStart;
        internal int End { get; } = end;
        internal bool PseudoElement { get; } = pseudoElement;
        internal bool IsHas { get; } = !pseudoElement && name.Equals("has", StringComparison.OrdinalIgnoreCase);
        internal bool Forgiving { get; } = !pseudoElement && (name.Equals("is", StringComparison.OrdinalIgnoreCase) || name.Equals("where", StringComparison.OrdinalIgnoreCase));
        internal bool Strict { get; } = !pseudoElement && (name.Equals("not", StringComparison.OrdinalIgnoreCase) || name.Equals("has", StringComparison.OrdinalIgnoreCase));
        internal bool InHas { get; set; }
        internal bool Valid { get; set; }
        internal string? Replacement { get; set; }
        internal List<Function> Children { get; } = [];
    }

    private static bool IsValid(string branch, ICssSelectorParser parser, bool relative)
    {
        try
        {
            for (var i = 0; i < branch.Length; i++)
            {
                SkipToken(branch, ref i, out var badString);
                if (badString)
                {
                    return false;
                }
            }
            DomSelectorText.Scan(branch, relative: relative);
            var selector = parser.ParseSelector(branch);
            if (selector is null)
            {
                return false;
            }
            var visitor = new NameVisitor();
            selector.Accept(visitor);
            return !visitor.HasPseudoElement;
        }
        catch (DomException exception) when (exception.Code == (int) DomError.Syntax)
        {
            return false;
        }
    }

    private static IEnumerable<string> Branches(string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (SkipToken(text, ref i))
            {
                continue;
            }
            if (text[i] is '(' or '[' or '{')
            {
                i = Closing(text, i);
            }
            else if (text[i] == ',')
            {
                yield return text[start..i];
                start = i + 1;
            }
        }
        yield return text[start..];
    }

    private static int Closing(string text, int start)
    {
        var stack = new Stack<char>();
        stack.Push(CloseFor(text[start]));
        for (var i = start + 1; i < text.Length; i++)
        {
            if (SkipToken(text, ref i))
            {
                continue;
            }
            var c = text[i];
            if (c is '(' or '[' or '{')
            {
                stack.Push(CloseFor(c));
            }
            else if (c is ')' or ']' or '}')
            {
                if (stack.Peek() != c)
                {
                    // An unmatched closing token belongs to this component value; it does not close
                    // its enclosing function. Native branch parsing will decide whether it is invalid.
                    continue;
                }
                stack.Pop();
                if (stack.Count == 0)
                {
                    return i;
                }
            }
        }
        return text.Length;
    }

    private static char CloseFor(char opening) => opening switch
    {
        '(' => ')',
        '[' => ']',
        _ => '}'
    };

    private static int SkipComments(string text, int start)
    {
        while (start + 1 < text.Length && text[start] == '/' && text[start + 1] == '*')
        {
            var end = text.IndexOf("*/", start + 2, StringComparison.Ordinal);
            start = end < 0 ? text.Length : end + 2;
        }
        return start;
    }

    private static bool SkipToken(string text, ref int i) => SkipToken(text, ref i, out _);

    private static bool SkipToken(string text, ref int i, out bool badString)
    {
        badString = false;
        if (text[i] == '\\')
        {
            i = DomSelectorText.EndOfEscape(text, i);
            return true;
        }
        if (text[i] is '\'' or '"')
        {
            var quote = text[i++];
            while (i < text.Length && text[i] != quote)
            {
                if (text[i] is '\n' or '\r' or '\f')
                {
                    badString = true;
                    return true;
                }
                if (text[i] == '\\')
                {
                    i = DomSelectorText.EndOfEscape(text, i);
                }
                i++;
            }
            return true;
        }
        if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
        {
            var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
            i = end < 0 ? text.Length : end + 1;
            return true;
        }
        return false;
    }

    private sealed class NameVisitor : ISelectorVisitor
    {
        internal string? Name { get; private set; }
        internal bool HasPseudoElement { get; private set; }
        public void Class(string value) => Name = value;
        public void Type(string name) { }
        public void Attribute(string name, string op, string? value) { }
        public void Id(string value) { }
        public void Child(string name, int step, int offset, ISelector selector) => selector.Accept(this);
        public void PseudoClass(string value) { }
        public void PseudoElement(string value) => HasPseudoElement = true;
        public void List(IEnumerable<ISelector> selectors) => Visit(selectors);
        public void Combinator(IEnumerable<ISelector> selectors, IEnumerable<string> symbols) => Visit(selectors);
        public void Many(IEnumerable<ISelector> selectors) => Visit(selectors);
        private void Visit(IEnumerable<ISelector> selectors)
        {
            foreach (var selector in selectors)
            {
                selector.Accept(this);
            }
        }
    }
}
