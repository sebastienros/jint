#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Benchmark;

/// <summary>Parse-time native pseudo-class dispatch, separated from DOM matching.</summary>
/// <remarks>
/// Each invocation freshly compiles 37 supported non-functional pseudo-classes. The historical
/// PagePseudoClassSelectorFactory string-comparison chain no longer exists: native compilation
/// uses generated length/chunk lookups. Uppercase names also exercise ASCII normalization.
/// The class-selector control uses the same names and lengths without pseudo-class identification.
/// No engine, page, matching, mailbox or selector cache enters these rows. Setup warms only the
/// selected row. Unsupported :unchecked and legacy pseudo-element aliases are excluded.
/// </remarks>
[MemoryDiagnoser]
public class SelectorParseDispatchBenchmark
{
    private static readonly string[] Names =
    [
        "scope", "root", "empty", "first-child",
        "last-child", "only-child", "first-of-type", "last-of-type",
        "only-of-type", "any-link", "link", "visited",
        "checked", "indeterminate", "default", "enabled",
        "disabled", "required", "optional", "valid",
        "invalid", "in-range", "out-of-range", "read-only",
        "read-write", "placeholder-shown", "open", "closed",
        "hover", "active", "focus", "focus-within",
        "focus-visible", "target", "autofill", "-webkit-autofill",
        "host",
    ];

    private string[] _selectors = null!;

    [GlobalSetup(Target = nameof(PseudoClasses))] public void SetupPseudoClasses() => Setup(':', false);
    [GlobalSetup(Target = nameof(UppercasePseudoClasses))] public void SetupUppercase() => Setup(':', true);
    [GlobalSetup(Target = nameof(ClassControl))] public void SetupControl() => Setup('.', false);

    private void Setup(char prefix, bool uppercase)
    {
        _selectors = Names.Select(name => prefix + (uppercase ? name.ToUpperInvariant() : name)).ToArray();
        if (Compile() != Names.Length) throw new InvalidOperationException("Selector compilation result changed.");
    }

    private int Compile()
    {
        var branches = 0;
        foreach (var selector in _selectors)
        {
            branches += SelectorCompiler.Compile(selector, null, default).Branches.Count;
        }
        return branches;
    }

    [Benchmark] public int PseudoClasses() => Compile();
    [Benchmark] public int UppercasePseudoClasses() => Compile();
    [Benchmark] public int ClassControl() => Compile();
}
