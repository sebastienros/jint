namespace Jint.HtmlParser.Css;

/// <summary>An immutable stylesheet syntax tree retaining its original source.</summary>
/// <remarks>
/// <para>Rules preserve syntax and source spans without asserting selector validity, property support, or CSSOM semantics.</para>
/// </remarks>
public sealed class CssStyleSheetSyntax
{
    // Adopts the parser's completed, exclusively owned array without exposing a mutable alias.
    internal CssStyleSheetSyntax(string source, CssRuleSyntax[] rules)
    {
        Source = source;
        Rules = Array.AsReadOnly(rules);
    }

    /// <summary>Gets the exact original stylesheet source.</summary>
    public string Source { get; }

    /// <summary>Gets the recovered rules in source order.</summary>
    public IReadOnlyList<CssRuleSyntax> Rules { get; }
}
