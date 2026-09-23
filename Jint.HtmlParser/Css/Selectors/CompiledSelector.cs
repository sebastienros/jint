using System.Collections.ObjectModel;
using System.Numerics;

namespace Jint.HtmlParser.Css.Selectors;

// Selectors Level 4, §3 and §17: https://drafts.csswg.org/selectors/#structure
internal sealed class CompiledSelector
{
    internal CompiledSelector(IReadOnlyList<Complex> branches)
    {
        Branches = branches;
        var maximum = default(SelectorSpecificity);
        foreach (var branch in branches)
        {
            if (branch.Specificity.CompareTo(maximum) > 0) maximum = branch.Specificity;
        }
        MaximumSpecificity = maximum;
    }

    internal IReadOnlyList<Complex> Branches { get; }
    internal SelectorSpecificity MaximumSpecificity { get; }

    internal static IReadOnlyList<T> Freeze<T>(List<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());

    internal enum Combinator { Descendant, Child, NextSibling, SubsequentSibling, Column }
    internal enum NamespaceMode { Any, None, Exact }
    internal enum AttributeOperator { Presence, Exact, Includes, DashMatch, Prefix, Suffix, Substring }
    internal enum PredicateKind
    {
        Id, Class, Attribute, PseudoElement, WebkitUnknownPseudoElement,
        Is, Where, Not, Has, NthChild, NthLastChild, NthOfType, NthLastOfType,
        NthCol, NthLastCol, Lang, Dir, Host, HostContext, Slotted, Picker,
        Scope, Root, Empty, FirstChild, LastChild, OnlyChild, FirstOfType, LastOfType,
        OnlyOfType, AnyLink, Link, Visited, Checked, Unchecked, Indeterminate,
        Default, Enabled, Disabled, Required, Optional, Valid, Invalid, InRange,
        OutOfRange, ReadOnly, ReadWrite, PlaceholderShown, Open, Closed, Hover,
        Active, Focus, FocusWithin, FocusVisible, Target, Autofill
    }

    internal sealed class Complex
    {
        internal Complex(IReadOnlyList<Compound> compounds, IReadOnlyList<Combinator> combinators,
            Combinator? leadingCombinator, CssSourceSpan span, SelectorSpecificity specificity)
        {
            Compounds = compounds;
            Combinators = combinators;
            LeadingCombinator = leadingCombinator;
            Span = span;
            Specificity = specificity;
        }

        internal IReadOnlyList<Compound> Compounds { get; }
        internal IReadOnlyList<Combinator> Combinators { get; }
        internal Combinator? LeadingCombinator { get; }
        internal CssSourceSpan Span { get; }
        internal SelectorSpecificity Specificity { get; }
    }

    internal sealed class Compound
    {
        internal Compound(NamespaceMode namespaceMode, string? namespaceUri, string? typeName,
            bool explicitType, IReadOnlyList<Predicate> predicates, CssSourceSpan span)
        {
            NamespaceMode = namespaceMode;
            NamespaceUri = namespaceUri;
            TypeName = typeName;
            HasExplicitType = explicitType;
            Predicates = predicates;
            Span = span;
        }

        internal NamespaceMode NamespaceMode { get; }
        internal string? NamespaceUri { get; }
        internal string? TypeName { get; } // null is universal.
        internal bool HasExplicitType { get; }
        internal IReadOnlyList<Predicate> Predicates { get; }
        internal CssSourceSpan Span { get; }
    }

    internal sealed class Predicate
    {
        internal Predicate(PredicateKind kind, CssSourceSpan span, string? name = null,
            NamespaceMode namespaceMode = NamespaceMode.Any, string? namespaceUri = null,
            AttributeOperator attributeOperator = AttributeOperator.Presence, string? value = null,
            char modifier = '\0', CompiledSelector? arguments = null,
            BigInteger a = default, BigInteger b = default,
            IReadOnlyList<string>? textArguments = null)
        {
            Kind = kind;
            Span = span;
            Name = name;
            NamespaceMode = namespaceMode;
            NamespaceUri = namespaceUri;
            Operator = attributeOperator;
            Value = value;
            Modifier = modifier;
            Arguments = arguments;
            A = a;
            B = b;
            TextArguments = textArguments;
        }

        internal PredicateKind Kind { get; }
        internal CssSourceSpan Span { get; }
        internal string? Name { get; }
        internal NamespaceMode NamespaceMode { get; }
        internal string? NamespaceUri { get; }
        internal AttributeOperator Operator { get; }
        internal string? Value { get; }
        internal char Modifier { get; }
        internal CompiledSelector? Arguments { get; }
        internal BigInteger A { get; }
        internal BigInteger B { get; }
        internal IReadOnlyList<string>? TextArguments { get; }
    }
}
