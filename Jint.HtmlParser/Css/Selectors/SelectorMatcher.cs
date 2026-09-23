using System.Collections.ObjectModel;
using System.Numerics;
using static Jint.HtmlParser.Css.Selectors.CompiledSelector;
using ComplexSelector = Jint.HtmlParser.Css.Selectors.CompiledSelector.Complex;

namespace Jint.HtmlParser.Css.Selectors;

// Selectors §4, §6 and §14: https://drafts.csswg.org/selectors/#match-a-selector-against-an-element
// Internal staging evaluator. Every accepted predicate must have an evaluator before publication.
internal static class SelectorMatcher
{
    internal static bool Matches(CompiledSelector program, Element element, Node? scopingRoot = null,
        CancellationToken cancellationToken = default)
        => TryMatch(program, element, out _, scopingRoot, cancellationToken);

    // Per-invocation checkpoint for deterministic cancellation tests; no callback
    // is retained by the compiled program or used by the production entry points.
    internal static bool Matches(CompiledSelector program, Element element, Node? scopingRoot,
        Action? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(element);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateImplemented(program, cancellationToken);
        var work = new Work(cancellationToken, checkpoint);
        var matched = TryMatchCore(program, element, ScopeFor(scopingRoot ?? element), ref work, out _);
        cancellationToken.ThrowIfCancellationRequested();
        return matched;
    }

    internal static bool TryMatch(CompiledSelector program, Element element, out SelectorSpecificity specificity,
        Node? scopingRoot = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(element);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateImplemented(program, cancellationToken);
        var scope = ScopeFor(scopingRoot ?? element);
        var work = new Work(cancellationToken);
        var matched = TryMatchCore(program, element, scope, ref work, out specificity);
        cancellationToken.ThrowIfCancellationRequested();
        return matched;
    }

    internal static Element? Closest(CompiledSelector program, Element element,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(element);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateImplemented(program, cancellationToken);
        var work = new Work(cancellationToken);
        for (Node? current = element; current is not null; current = current.ParentNode)
        {
            work.Step();
            if (current is Element candidate && TryMatchCore(program, candidate, element, ref work, out _))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return candidate;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }

    internal static Element? QuerySelector(CompiledSelector program, Node root,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(root);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateImplemented(program, cancellationToken);
        var scope = ScopeFor(root);
        var work = new Work(cancellationToken);
        foreach (var candidate in NodeTraversal.DescendantElements(root, cancellationToken))
        {
            work.Step();
            if (TryMatchCore(program, candidate, scope, ref work, out _))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return candidate;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }

    internal static IReadOnlyList<Element> QuerySelectorAll(CompiledSelector program, Node root,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(root);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateImplemented(program, cancellationToken);
        var scope = ScopeFor(root);
        var work = new Work(cancellationToken);
        var results = new List<Element>();
        foreach (var candidate in NodeTraversal.DescendantElements(root, cancellationToken))
        {
            work.Step();
            if (TryMatchCore(program, candidate, scope, ref work, out _)) results.Add(candidate);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new ReadOnlyCollection<Element>(results.ToArray());
    }

    // Walk every branch before searching. A mixed selector list cannot quietly return
    // an incomplete answer merely because an earlier supported branch matched.
    private static void ValidateImplemented(CompiledSelector program, CancellationToken cancellationToken)
    {
        var pending = new Stack<CompiledSelector>();
        pending.Push(program);
        var work = new Work(cancellationToken);
        while (pending.Count != 0)
        {
            var current = pending.Pop();
            foreach (var branch in current.Branches)
            {
                if (branch.LeadingCombinator is not null) throw Unsupported("relative selector");
                foreach (var combinator in branch.Combinators)
                {
                    work.Step();
                    if (combinator == Combinator.Column) throw Unsupported("column combinator");
                }
                foreach (var compound in branch.Compounds)
                {
                    foreach (var predicate in compound.Predicates)
                    {
                        work.Step();
                        if (!IsImplemented(predicate)) throw Unsupported(predicate.Kind.ToString());
                        if (predicate.Arguments is not null) pending.Push(predicate.Arguments);
                    }
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static bool IsImplemented(Predicate predicate) => predicate.Kind switch
    {
        PredicateKind.Id or PredicateKind.Class or PredicateKind.Attribute or
        PredicateKind.PseudoElement or PredicateKind.WebkitUnknownPseudoElement or
        PredicateKind.Picker or
        PredicateKind.Scope or PredicateKind.Root or PredicateKind.Empty or
        PredicateKind.FirstChild or PredicateKind.LastChild or PredicateKind.OnlyChild or
        PredicateKind.FirstOfType or PredicateKind.LastOfType or PredicateKind.OnlyOfType or
        PredicateKind.NthChild or PredicateKind.NthLastChild or
        PredicateKind.NthOfType or PredicateKind.NthLastOfType => predicate.Arguments is null,
        PredicateKind.Slotted => true,
        _ => false
    };

    private static InvalidOperationException Unsupported(string kind)
        => new($"Selector predicate '{kind}' has no matching evaluator yet.");

    private static Node? ScopeFor(Node root) => root is Document document ? document.DocumentElement : root;

    private static bool TryMatchCore(CompiledSelector program, Element element, Node? scope,
        ref Work work, out SelectorSpecificity specificity)
    {
        specificity = default;
        var found = false;
        foreach (var branch in program.Branches)
        {
            work.Step();
            if (!MatchBranch(branch, element, scope, ref work)) continue;
            if (!found || branch.Specificity.CompareTo(specificity) > 0) specificity = branch.Specificity;
            found = true;
        }
        return found;
    }

    private static bool MatchBranch(ComplexSelector branch, Element subject, Node? scope, ref Work work)
    {
        if (branch.Compounds.Count == 1)
            return MatchCompound(branch.Compounds[0], subject, scope, ref work);

        // One frame per compound on the current path. Enumerating candidates from
        // each frame preserves backtracking without recursion or collecting ancestors.
        var frames = new List<Frame> { new(subject, branch.Compounds.Count - 1) };
        while (frames.Count != 0)
        {
            work.Step();
            var index = frames.Count - 1;
            var frame = frames[index];
            if (!frame.Checked)
            {
                frame.Checked = true;
                if (!MatchCompound(branch.Compounds[frame.Part], frame.Node, scope, ref work))
                {
                    frames.RemoveAt(index);
                    continue;
                }
                if (frame.Part == 0) return true;
                frame.Next = InitialPredecessor(frame.Node, branch.Combinators[frame.Part - 1], ref work);
            }
            var combinator = branch.Combinators[frame.Part - 1];
            var predecessor = frame.Next;
            if (predecessor is null)
            {
                frames.RemoveAt(index);
                continue;
            }
            frame.Next = NextPredecessor(predecessor, combinator, ref work);
            frames[index] = frame;
            if (predecessor is Element || ReferenceEquals(predecessor, scope) && predecessor is DocumentFragment)
                frames.Add(new Frame(predecessor, frame.Part - 1));
        }
        return false;
    }

    private static Node? InitialPredecessor(Node node, Combinator combinator, ref Work work)
    {
        if (combinator is Combinator.Child or Combinator.Descendant) return node.ParentNode;
        if (combinator == Combinator.SubsequentSibling) return node.PreviousSibling;
        if (combinator != Combinator.NextSibling) throw Unsupported(combinator.ToString());
        for (var previous = node.PreviousSibling; previous is not null; previous = previous.PreviousSibling)
        {
            work.Step();
            if (previous is Element) return previous;
        }
        return null;
    }

    private static Node? NextPredecessor(Node current, Combinator combinator, ref Work work)
    {
        if (combinator is Combinator.Child or Combinator.NextSibling) return null;
        var next = combinator == Combinator.Descendant ? current.ParentNode : current.PreviousSibling;
        work.Step();
        return next;
    }

    private static bool MatchCompound(Compound compound, Node node, Node? scope, ref Work work)
    {
        if (node is not Element element)
        {
            // A fragment is a featureless scoping root. It can anchor :scope > x,
            // but has no name, namespace, attributes or other pseudo-classes.
            if (!ReferenceEquals(node, scope) || node is not DocumentFragment ||
                compound.HasExplicitType || compound.Predicates.Count == 0) return false;
            foreach (var predicate in compound.Predicates)
            {
                work.Step();
                if (predicate.Kind != PredicateKind.Scope) return false;
            }
            return true;
        }
        if (!NamespaceMatches(compound.NamespaceMode, compound.NamespaceUri, element.NamespaceUri) ||
            compound.TypeName is not null && !SelectorNameMatches(compound.TypeName, element.LocalName,
                IsHtmlElement(element), ref work)) return false;
        foreach (var predicate in compound.Predicates)
        {
            work.Step();
            if (!MatchPredicate(predicate, element, scope, ref work)) return false;
        }
        return true;
    }

    private static bool MatchPredicate(Predicate predicate, Element element, Node? scope, ref Work work)
    {
        switch (predicate.Kind)
        {
            case PredicateKind.Id:
                return MatchIdOrClass(element, "id", predicate.Name!, split: false, ref work);
            case PredicateKind.Class:
                return MatchIdOrClass(element, "class", predicate.Name!, split: true, ref work);
            case PredicateKind.Attribute:
                return MatchAttribute(predicate, element, ref work);
            case PredicateKind.PseudoElement:
            case PredicateKind.WebkitUnknownPseudoElement:
            case PredicateKind.Picker:
            case PredicateKind.Slotted:
                return false;
            case PredicateKind.Scope:
                return ReferenceEquals(element, scope);
            case PredicateKind.Root:
                return ReferenceEquals(element, element.OwnerDocument?.DocumentElement);
            case PredicateKind.Empty:
                for (var child = element.FirstChild; child is not null; child = child.NextSibling)
                {
                    work.Step();
                    if (child is Element || child is Text text && HasNonWhitespace(text.Data, ref work) ||
                        child is CDataSection data && HasNonWhitespace(data.Data, ref work)) return false;
                }
                return true;
            case PredicateKind.FirstChild:
            case PredicateKind.LastChild:
            case PredicateKind.OnlyChild:
            case PredicateKind.FirstOfType:
            case PredicateKind.LastOfType:
            case PredicateKind.OnlyOfType:
            case PredicateKind.NthChild:
            case PredicateKind.NthLastChild:
            case PredicateKind.NthOfType:
            case PredicateKind.NthLastOfType:
                return MatchPosition(predicate, element, ref work);
            default:
                throw Unsupported(predicate.Kind.ToString());
        }
    }

    private static bool MatchIdOrClass(Element element, string attributeName, string expected, bool split,
        ref Work work)
    {
        var value = element.GetAttribute(attributeName);
        if (value is null) return false;
        var ignoreCase = element.OwnerDocument?.Kind == DocumentKind.Html &&
                         element.OwnerDocument.Mode == DocumentMode.Quirks;
        if (!split) return NameEquals(value, expected, ignoreCase, ref work);
        return ContainsWord(value, expected, ignoreCase, ref work);
    }

    private static bool MatchAttribute(Predicate predicate, Element element, ref Work work)
    {
        foreach (var attribute in element.Attributes)
        {
            work.Step();
            if (!NamespaceMatches(predicate.NamespaceMode, predicate.NamespaceUri, attribute.NamespaceUri) ||
                !SelectorNameMatches(predicate.Name!, attribute.LocalName,
                    IsHtmlElement(element) && attribute.NamespaceUri is null, ref work)) continue;
            var value = attribute.Value;
            if (predicate.Operator == AttributeOperator.Presence) return true;
            var expected = predicate.Value!;
            var ignoreCase = predicate.Modifier == 'i' || predicate.Modifier == '\0' &&
                IsHtmlElement(element) && attribute.NamespaceUri is null &&
                HasHtmlCaseInsensitiveValue(attribute.LocalName);
            if (predicate.Operator == AttributeOperator.Exact &&
                NameEquals(value, expected, ignoreCase, ref work)) return true;
            if (predicate.Operator == AttributeOperator.Includes && expected.Length != 0 &&
                !HasAsciiWhitespace(expected, ref work) && ContainsWord(value, expected, ignoreCase, ref work)) return true;
            if (predicate.Operator == AttributeOperator.DashMatch &&
                (NameEquals(value, expected, ignoreCase, ref work) ||
                 value.Length > expected.Length && value[expected.Length] == '-' &&
                 TextEquals(value.AsSpan(0, expected.Length), expected.AsSpan(), ignoreCase, ref work))) return true;
            if (predicate.Operator == AttributeOperator.Prefix && expected.Length != 0 &&
                value.Length >= expected.Length &&
                TextEquals(value.AsSpan(0, expected.Length), expected.AsSpan(), ignoreCase, ref work)) return true;
            if (predicate.Operator == AttributeOperator.Suffix && expected.Length != 0 &&
                value.Length >= expected.Length &&
                TextEquals(value.AsSpan(value.Length - expected.Length), expected.AsSpan(), ignoreCase, ref work)) return true;
            if (predicate.Operator == AttributeOperator.Substring && expected.Length != 0 &&
                ContainsText(value, expected, ignoreCase, ref work)) return true;
        }
        return false;
    }

    private static bool MatchPosition(Predicate predicate, Element element, ref Work work)
    {
        var kind = predicate.Kind;
        var fromEnd = kind is PredicateKind.LastChild or PredicateKind.OnlyChild or
            PredicateKind.LastOfType or PredicateKind.OnlyOfType or
            PredicateKind.NthLastChild or PredicateKind.NthLastOfType;
        var ofType = kind is PredicateKind.FirstOfType or PredicateKind.LastOfType or
            PredicateKind.OnlyOfType or PredicateKind.NthOfType or PredicateKind.NthLastOfType;
        var index = 1;
        for (var node = fromEnd ? element.NextSibling : element.PreviousSibling;
             node is not null; node = fromEnd ? node.NextSibling : node.PreviousSibling)
        {
            work.Step();
            if (node is Element sibling && (!ofType || SameType(element, sibling))) index++;
        }
        if (kind is PredicateKind.OnlyChild or PredicateKind.OnlyOfType)
        {
            if (index != 1) return false;
            for (var node = element.PreviousSibling; node is not null; node = node.PreviousSibling)
            {
                work.Step();
                if (node is Element sibling && (!ofType || SameType(element, sibling))) return false;
            }
            return true;
        }
        if (kind is PredicateKind.FirstChild or PredicateKind.LastChild or
            PredicateKind.FirstOfType or PredicateKind.LastOfType) return index == 1;
        return MatchAnPlusB(index, predicate.A, predicate.B);
    }

    private static bool MatchAnPlusB(int index, BigInteger a, BigInteger b)
    {
        var difference = (BigInteger) index - b;
        return a.IsZero ? difference.IsZero : difference % a == 0 && difference / a >= 0;
    }

    private static bool SameType(Element left, Element right) =>
        left.NamespaceUri == right.NamespaceUri && left.LocalName == right.LocalName;

    private static bool NamespaceMatches(NamespaceMode mode, string? expected, string? actual) => mode switch
    {
        NamespaceMode.Any => true,
        NamespaceMode.None => actual is null,
        NamespaceMode.Exact => actual == expected,
        _ => false
    };

    private static bool IsHtmlElement(Element element) =>
        element.OwnerDocument?.Kind == DocumentKind.Html && element.NamespaceUri == Namespaces.Html;

    private static bool NameEquals(string left, string right, bool ignoreCase, ref Work work) =>
        TextEquals(left.AsSpan(), right.AsSpan(), ignoreCase, ref work);

    // HTML §4.16.2 lowercases the selector spelling, then compares it to the
    // element/attribute's stored name identically. Script can create a native
    // HTML-namespace node whose stored name is uppercase; it must not match.
    private static bool SelectorNameMatches(string selectorName, string nativeName, bool lowercaseSelector,
        ref Work work)
    {
        if (selectorName.Length != nativeName.Length) return false;
        for (var index = 0; index < selectorName.Length; index++)
        {
            work.Step();
            var selectorCharacter = lowercaseSelector ? AsciiLower(selectorName[index]) : selectorName[index];
            if (selectorCharacter != nativeName[index]) return false;
        }
        return true;
    }

    // HTML §4.16.2: https://html.spec.whatwg.org/multipage/semantics-other.html#case-sensitivity-of-selectors
    private static bool HasHtmlCaseInsensitiveValue(string name) => name is
        "accept" or "accept-charset" or "align" or "alink" or "axis" or "bgcolor" or
        "charset" or "checked" or "clear" or "codetype" or "color" or "compact" or
        "declare" or "defer" or "dir" or "direction" or "disabled" or "enctype" or
        "face" or "frame" or "hreflang" or "http-equiv" or "lang" or "language" or
        "link" or "media" or "method" or "multiple" or "nohref" or "noresize" or
        "noshade" or "nowrap" or "readonly" or "rel" or "rev" or "rules" or "scope" or
        "scrolling" or "selected" or "shape" or "target" or "text" or "type" or
        "valign" or "valuetype" or "vlink";

    private static bool TextEquals(ReadOnlySpan<char> left, ReadOnlySpan<char> right, bool ignoreCase,
        ref Work work)
    {
        if (left.Length != right.Length) return false;
        for (var index = 0; index < left.Length; index++)
        {
            work.Step();
            var a = left[index];
            var b = right[index];
            if (ignoreCase)
            {
                a = AsciiLower(a);
                b = AsciiLower(b);
            }
            if (a != b) return false;
        }
        return true;
    }

    private static bool ContainsText(string value, string text, bool ignoreCase, ref Work work)
    {
        for (var index = 0; index <= value.Length - text.Length; index++)
        {
            work.Step();
            if (TextEquals(value.AsSpan(index, text.Length), text.AsSpan(), ignoreCase, ref work)) return true;
        }
        return false;
    }

    private static bool ContainsWord(string value, string word, bool ignoreCase, ref Work work)
    {
        for (var index = 0; index < value.Length;)
        {
            while (index < value.Length && IsAsciiWhitespace(value[index]))
            {
                work.Step();
                index++;
            }
            var start = index;
            while (index < value.Length && !IsAsciiWhitespace(value[index]))
            {
                work.Step();
                index++;
            }
            if (index > start && TextEquals(value.AsSpan(start, index - start), word.AsSpan(), ignoreCase,
                    ref work)) return true;
        }
        return false;
    }

    private static bool HasAsciiWhitespace(string value, ref Work work)
    {
        foreach (var character in value)
        {
            work.Step();
            if (IsAsciiWhitespace(character)) return true;
        }
        return false;
    }

    // Selectors §13.2 now permits document white space within :empty.
    private static bool HasNonWhitespace(string value, ref Work work)
    {
        foreach (var character in value)
        {
            work.Step();
            if (!IsAsciiWhitespace(character)) return true;
        }
        return false;
    }

    private static bool IsAsciiWhitespace(char character) => character is '\t' or '\n' or '\f' or '\r' or ' ';

    private static char AsciiLower(char character) => character is >= 'A' and <= 'Z'
        ? (char) (character + ('a' - 'A')) : character;

    private sealed class Frame(Node node, int part)
    {
        internal Node Node { get; } = node;
        internal int Part { get; } = part;
        internal bool Checked { get; set; }
        internal Node? Next { get; set; }
    }

    private struct Work(CancellationToken cancellationToken, Action? checkpoint = null)
    {
        private int _steps;
        internal void Step()
        {
            if ((++_steps & 255) == 0)
            {
                checkpoint?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
