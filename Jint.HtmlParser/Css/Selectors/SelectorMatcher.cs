using System.Collections.ObjectModel;
using System.Numerics;
using static Jint.HtmlParser.Css.Selectors.CompiledSelector;

namespace Jint.HtmlParser.Css.Selectors;

// Selectors §4, §6 and §14: https://drafts.csswg.org/selectors/#match-a-selector-against-an-element
// Internal staging evaluator. Every accepted predicate must have an evaluator before publication.
internal static partial class SelectorMatcher
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
        var work = new Work(cancellationToken, checkpoint);
        ValidateImplemented(program, ref work, cancellationToken);
        var matched = TryMatchCore(program, element, ScopeFor(scopingRoot ?? element, ref work), ref work, out _);
        cancellationToken.ThrowIfCancellationRequested();
        return matched;
    }

    internal static bool TryMatch(CompiledSelector program, Element element, out SelectorSpecificity specificity,
        Node? scopingRoot = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(element);
        cancellationToken.ThrowIfCancellationRequested();
        var work = new Work(cancellationToken);
        ValidateImplemented(program, ref work, cancellationToken);
        var scope = ScopeFor(scopingRoot ?? element, ref work);
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
        var work = new Work(cancellationToken);
        ValidateImplemented(program, ref work, cancellationToken);
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
        var work = new Work(cancellationToken);
        ValidateImplemented(program, ref work, cancellationToken);
        var scope = ScopeFor(root, ref work);
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
        => QuerySelectorAll(program, root, null, cancellationToken);

    internal static IReadOnlyList<Element> QuerySelectorAll(CompiledSelector program, Node root,
        Action? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(root);
        cancellationToken.ThrowIfCancellationRequested();
        var work = new Work(cancellationToken, checkpoint);
        ValidateImplemented(program, ref work, cancellationToken);
        var scope = ScopeFor(root, ref work);
        var results = new List<Element>();
        foreach (var candidate in NodeTraversal.DescendantElements(root, cancellationToken))
        {
            work.Step();
            if (TryMatchCore(program, candidate, scope, ref work, out _)) results.Add(candidate);
        }
        cancellationToken.ThrowIfCancellationRequested();
        work.Check();
        var snapshot = new Element[results.Count];
        for (var index = 0; index < results.Count; index++)
        {
            work.Step();
            snapshot[index] = results[index];
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new ReadOnlyCollection<Element>(snapshot);
    }

    // Walk every branch before searching. A mixed selector list cannot quietly return
    // an incomplete answer merely because an earlier supported branch matched.
    private static void ValidateImplemented(CompiledSelector program, ref Work work,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<(CompiledSelector Program, bool Relative)>();
        pending.Push((program, false));
        while (pending.Count != 0)
        {
            var (current, currentIsRelative) = pending.Pop();
            foreach (var branch in current.Branches)
            {
                work.Step();
                if (branch.LeadingCombinator is not null && !currentIsRelative)
                    throw Unsupported("relative selector");
                foreach (var compound in branch.Compounds)
                {
                    work.Step();
                    foreach (var predicate in compound.Predicates)
                    {
                        work.Step();
                        if (!IsImplemented(predicate)) throw Unsupported(predicate.Kind.ToString());
                        if (predicate.Arguments is not null)
                            pending.Push((predicate.Arguments, predicate.Kind == PredicateKind.Has));
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
        PredicateKind.NthOfType or PredicateKind.NthLastOfType => predicate.Arguments is null,
        PredicateKind.NthChild or PredicateKind.NthLastChild => true,
        PredicateKind.NthCol or PredicateKind.NthLastCol => predicate.Arguments is null,
        PredicateKind.Is or PredicateKind.Where or PredicateKind.Not or PredicateKind.Has =>
            predicate.Arguments is not null,
        PredicateKind.Slotted => true,
        _ => false
    };

    private static InvalidOperationException Unsupported(string kind)
        => new($"Selector predicate '{kind}' has no matching evaluator yet.");

    private static Node? ScopeFor(Node root, ref Work work)
    {
        if (root is not Document document) return root;
        for (var child = document.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is Element) return child;
        }
        return null;
    }

    private static bool TryMatchCore(CompiledSelector program, Element element, Node? scope,
        ref Work work, out SelectorSpecificity specificity)
    {
        // A lone compound with ordinary predicates is the common query path.
        // Keep it allocation-free; relational programs use the explicit VM.
        if (program.Branches.Count == 1)
        {
            var branch = program.Branches[0];
            if (branch.Compounds.Count == 1 && branch.LeadingCombinator is null)
            {
                var compound = branch.Compounds[0];
                var simple = true;
                foreach (var predicate in compound.Predicates)
                {
                    work.Step();
                    if (predicate.Kind is PredicateKind.Is or PredicateKind.Where or PredicateKind.Not or
                        PredicateKind.Has || predicate.Arguments is not null)
                    {
                        simple = false;
                        break;
                    }
                }
                if (simple)
                {
                    var matched = NamespaceMatches(compound.NamespaceMode, compound.NamespaceUri,
                                      element.NamespaceUri) &&
                                  (compound.TypeName is null || SelectorNameMatches(compound.TypeName,
                                      element.LocalName, IsHtmlElement(element), ref work));
                    if (matched)
                    {
                        foreach (var predicate in compound.Predicates)
                        {
                            work.Step();
                            if (!MatchPredicate(predicate, element, scope, ref work))
                            {
                                matched = false;
                                break;
                            }
                        }
                    }
                    specificity = matched ? branch.Specificity : default;
                    return matched;
                }
            }
        }
        return Evaluate(program, element, scope, ref work, out specificity);
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
                return element.ParentNode is Document;
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
            case PredicateKind.NthCol:
            case PredicateKind.NthLastCol:
                return MatchColumnPosition(predicate, element, ref work);
            default:
                throw Unsupported(predicate.Kind.ToString());
        }
    }

    private static bool MatchIdOrClass(Element element, string attributeName, string expected, bool split,
        ref Work work)
    {
        string? value = null;
        foreach (var attribute in element.Attributes)
        {
            work.Step();
            if (attribute.NamespaceUri is not null || attribute.LocalName != attributeName) continue;
            value = attribute.Value;
            break;
        }
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
        var ofType = kind is PredicateKind.FirstOfType or PredicateKind.LastOfType or
            PredicateKind.OnlyOfType or PredicateKind.NthOfType or PredicateKind.NthLastOfType;
        if (kind is PredicateKind.FirstChild or PredicateKind.FirstOfType)
            return !HasMatchingSibling(element, previous: true, ofType, ref work);
        if (kind is PredicateKind.LastChild or PredicateKind.LastOfType)
            return !HasMatchingSibling(element, previous: false, ofType, ref work);
        if (kind is PredicateKind.OnlyChild or PredicateKind.OnlyOfType)
            return !HasMatchingSibling(element, previous: true, ofType, ref work) &&
                   !HasMatchingSibling(element, previous: false, ofType, ref work);

        var fromEnd = kind is PredicateKind.NthLastChild or PredicateKind.NthLastOfType;
        var index = 1;
        for (var node = fromEnd ? element.NextSibling : element.PreviousSibling;
             node is not null; node = fromEnd ? node.NextSibling : node.PreviousSibling)
        {
            work.Step();
            if (node is Element sibling && (!ofType || SameType(element, sibling))) index++;
        }
        return MatchAnPlusB(index, predicate.A, predicate.B, ref work);
    }

    private static bool HasMatchingSibling(Element element, bool previous, bool ofType, ref Work work)
    {
        for (var node = previous ? element.PreviousSibling : element.NextSibling;
             node is not null; node = previous ? node.PreviousSibling : node.NextSibling)
        {
            work.Step();
            if (node is Element sibling && (!ofType || SameType(element, sibling))) return true;
        }
        return false;
    }

    private static bool MatchAnPlusB(int index, BigInteger a, BigInteger b, ref Work work)
    {
        work.Check();
        if (a.IsZero)
        {
            var equal = b == index;
            work.Check();
            return equal;
        }
        var difference = (BigInteger) index - b;
        work.Check();
        if (!difference.IsZero && difference.Sign != a.Sign) return false;
        var quotient = BigInteger.DivRem(difference, a, out var remainder);
        work.Check();
        return remainder.IsZero && quotient.Sign >= 0;
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
        if (text.Length > value.Length) return false;
        // KMP bounds unsuccessful matching to O(value + text), including a
        // repeated-prefix needle such as aaaa...b against aaaa...aaaa.
        work.Check();
        Span<int> failure = text.Length <= 128 ? stackalloc int[text.Length] : new int[text.Length];
        work.Check();
        failure[0] = 0;
        var prefix = 0;
        for (var index = 1; index < text.Length; index++)
        {
            work.Step();
            while (prefix != 0 && !CharacterEquals(text[index], text[prefix], ignoreCase))
            {
                work.Step();
                prefix = failure[prefix - 1];
            }
            if (CharacterEquals(text[index], text[prefix], ignoreCase)) prefix++;
            failure[index] = prefix;
        }
        var matched = 0;
        foreach (var character in value)
        {
            work.Step();
            while (matched != 0 && !CharacterEquals(character, text[matched], ignoreCase))
            {
                work.Step();
                matched = failure[matched - 1];
            }
            if (CharacterEquals(character, text[matched], ignoreCase)) matched++;
            if (matched == text.Length) return true;
        }
        return false;
    }

    private static bool CharacterEquals(char left, char right, bool ignoreCase) =>
        ignoreCase ? AsciiLower(left) == AsciiLower(right) : left == right;

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
            if (!IsDocumentWhitespace(character)) return true;
        }
        return false;
    }

    private static bool IsAsciiWhitespace(char character) => character is '\t' or '\n' or '\f' or '\r' or ' ';

    private static bool IsDocumentWhitespace(char character) => character is '\t' or '\n' or '\r' or ' ';

    private static char AsciiLower(char character) => character is >= 'A' and <= 'Z'
        ? (char) (character + ('a' - 'A')) : character;

    private struct Work(CancellationToken cancellationToken, Action? checkpoint = null)
    {
        private int _steps;
        // Shared only by one matching/query call; compiled programs retain no state.
        private Dictionary<CompiledSelector, bool>? _featurelessEligibility;
        private Dictionary<Element, HtmlTableGrid>? _tableGrids;
        internal CancellationToken Token => cancellationToken;
        internal Action? Checkpoint => checkpoint;
        internal HtmlTableGrid GridFor(Element table)
        {
            Step();
            _tableGrids ??= new Dictionary<Element, HtmlTableGrid>(ReferenceEqualityComparer.Instance);
            if (!_tableGrids.TryGetValue(table, out var grid))
            {
                grid = HtmlTableGrid.Build(table, checkpoint, cancellationToken);
                _tableGrids.Add(table, grid);
            }
            return grid;
        }
        internal bool TryGetFeaturelessEligibility(CompiledSelector program, out bool eligible)
        {
            Step();
            if (_featurelessEligibility is not null &&
                _featurelessEligibility.TryGetValue(program, out eligible)) return true;
            eligible = false;
            return false;
        }
        internal void SetFeaturelessEligibility(CompiledSelector program, bool eligible)
        {
            Step();
            (_featurelessEligibility ??= new Dictionary<CompiledSelector, bool>())[program] = eligible;
        }
        internal void Check()
        {
            checkpoint?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
        }
        internal void Step()
        {
            if ((++_steps & 255) == 0) Check();
        }
    }
}
