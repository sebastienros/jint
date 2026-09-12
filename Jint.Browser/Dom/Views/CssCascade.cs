using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Css.RenderTree;
using AngleSharp.Css.Values;
using AngleSharp.Dom;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// The one guarded door onto AngleSharp.Css's cascade: every caller of <c>ComputeCurrentStyle()</c> comes
/// through here, and none of them ever sees a CLR exception.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a door at all.</b> <c>ComputeCurrentStyle()</c> raises rather than skipping a declaration it
/// cannot compute, and the whole call goes with it — so one unsupported unit anywhere in the matching
/// cascade takes every other property with it. There are four callers, they answer four different questions
/// (a page's <c>getComputedStyle</c>, the flat box model's rendered set, the accessibility tree's hidden
/// verdict, the <c>CSS</c> domain's computed style), and every one of them is reachable from a protocol
/// client — where an escaping CLR exception is a <i>protocol</i> error rather than a script one, which
/// Playwright reads as the element having been detached
/// (<a href="https://github.com/sebastienros/jint/issues/3730">#3730</a>).
/// </para>
/// <para>
/// <b>Two failures remain, and neither is a percentage any more.</b>
/// <see cref="Runtime.PageRenderDevice"/> is registered on the page's browsing context, so a percentage, a
/// <c>vw</c>, a <c>vh</c> and a <c>calc()</c> over them all compute. What still raises is a unit
/// AngleSharp.Css has no conversion for — <c>ch</c> and <c>ex</c>, an
/// <c>InvalidOperationException("Unsupported unit cannot be converted.")</c> — and a document whose
/// browsing context has no CSS services at all, which is <c>InvalidOperationException("Sequence contains
/// no elements")</c> from the factory lookup. The <c>ArgumentException</c> arm is kept because it is what a
/// zero-extent device raises, and a client may still ask for one.
/// </para>
/// <para>
/// <b>Reading a property is its own guarded step, because the second failure lands there rather than in
/// the compute.</b> Without the CSS services <c>ComputeCurrentStyle()</c> answers a declaration and
/// <c>GetPropertyValue</c> on it is what raises — a property nothing declared falls through to the
/// shorthand path, which asks the browsing context for a factory it has none of. So a caller that holds a
/// declaration reads through <see cref="ValueOf"/>, never through the member directly.
/// </para>
/// <para>
/// <b>No cascade is not the same as an empty one.</b> A caller that gets <see langword="null"/> knows the
/// cascade could not be computed and can say so — <see cref="ResolvedStyle"/> answers the ten properties an
/// automation client reads, and <c>Accessibility/ElementVisibility</c> falls back to the <c>style</c>
/// content attribute — where an empty declaration would read as "nothing is declared", which is a different
/// and wrong answer.
/// </para>
/// </remarks>
internal static class CssCascade
{
    /// <summary>
    /// The computed cascade for <paramref name="element"/>, or <see langword="null"/> when AngleSharp.Css
    /// cannot compute one.
    /// </summary>
    internal static ICssStyleDeclaration? Of(IElement element, bool resolveInheritance = true)
    {
        CssRuleUsage.Observe(element);

        try
        {
            var computed = element.ComputeCurrentStyle();
            // The native computed-parent path can leave an explicit inherit unresolved when the
            // parent declares no value. Retain the existing ancestor-walk compatibility path.
            if (resolveInheritance && computed.Any(static property => property.IsInherited && !property.CanBeInherited)
                && Traversal.For(element.Owner) is { } traversal)
            {
                return traversal.Of(element);
            }

            return computed;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NullReferenceException)
        {
            return null;
        }
    }

    /// <summary>
    /// The value <paramref name="declaration"/> settled for <paramref name="property"/>, or
    /// <see langword="null"/> when it cannot answer.
    /// </summary>
    /// <param name="declaration">A declaration <see cref="Of"/> answered.</param>
    /// <param name="property">The CSS property name, lower case.</param>
    internal static string? ValueOf(ICssStyleDeclaration declaration, string property)
    {
        try
        {
            return declaration.GetPropertyValue(property);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NullReferenceException)
        {
            return null;
        }
    }

    /// <summary>
    /// https://drafts.csswg.org/css-cascade/#inheritance - a cascade shared only by one synchronous tree
    /// walk, never across DOM or CSSOM writes. Matching and ordinary inheritance remain AngleSharp's.
    /// </summary>
    /// <remarks>
    /// AngleSharp.Css 1.1.0 resolves custom properties in its computed-style APIs, which <see cref="Of"/>
    /// uses directly. Its parent-computed overload is internal, so a traversal still needs this path to
    /// avoid rematching every ancestor for every element.
    /// </remarks>
    internal enum StyleScope
    {
        All,
        Visibility,
        Layout
    }

    internal sealed class Traversal(IStyleCollection styles, StyleScope scope = StyleScope.All, bool includeVariables = false)
    {
        private readonly IStyleCollection _styles = scope != StyleScope.All
            ? new ScopedStyles(styles, scope, includeVariables)
            : styles;
        private readonly Dictionary<IElement, Cascade> _cascaded = new();
        private readonly Stack<IElement> _pending = new();
        private Traversal? _variableTraversal;
        private Traversal? _layoutTraversal;

        internal static Traversal? For(IDocument? document, StyleScope scope = StyleScope.All)
        {
            if (document?.DefaultView is not { } window)
            {
                return null;
            }

            var device = document.Context.GetService<IRenderDevice>() ?? new DefaultRenderDevice();
            var styles = window.GetStyleCollection(device);
            return new Traversal(styles, scope);
        }

        internal ICssStyleDeclaration? LayoutOf(IElement element)
            => scope == StyleScope.Visibility ? (_layoutTraversal ??= new Traversal(styles, StyleScope.Layout)).Of(element) : Of(element);

        internal ICssStyleDeclaration? Of(IElement element)
        {
            try
            {
                // Keep ordinary declarations raw to preserve AngleSharp's child-relative lengths
                // and var() behavior. Custom properties alone inherit resolved values.
                var current = element;
                while (current is not null && !_cascaded.ContainsKey(current))
                {
                    _pending.Push(current);
                    current = current.ParentElement;
                }

                var parent = current is null ? null : _cascaded[current];
                while (_pending.TryPop(out current))
                {
                    CssRuleUsage.Observe(element);

                    // Capture local variables before inheritance. A rule matching both parent and
                    // child shares property objects, so reference identity cannot identify inheritance.
                    var cascade = _styles.ComputeExplicitStyle(current);
                    if (scope != StyleScope.All && !includeVariables)
                    {
                        RetainScope(cascade, scope);
                    }

                    var variables = parent is not null && !cascade.Any(static property => property.Name.StartsWith("--", StringComparison.Ordinal))
                        ? parent.Variables
                        : new CustomProperties(cascade, parent?.Variables);
                    if (scope != StyleScope.All)
                    {
                        RetainScope(cascade, scope);
                    }

                    if (parent is not null)
                    {
                        Inherit(cascade, parent.Raw);
                    }

                    // AngleSharp's ancestor walk can resolve an explicit inherit past a parent that
                    // declares nothing for a non-inherited property. Preserve that answer too.
                    if (current.ParentElement is not null
                        && cascade.Any(static property => property.IsInherited && !property.CanBeInherited))
                    {
                        cascade = _styles.GetDeclarations(current);
                        if (scope != StyleScope.All)
                        {
                            RetainScope(cascade, scope);
                        }
                    }

                    // Literal values need no custom-property graph. A pending shorthand longhand
                    // exposes an empty value through the public API; its internal child value may
                    // reference variables too, so resolve that case with the complete environment.
                    var computed = scope != StyleScope.All && !includeVariables && cascade.Any(static property => property.RawValue is CssReferenceValue
                        || property.RawValue is not null && property.Value.Length == 0)
                        ? (_variableTraversal ??= new Traversal(styles, scope, includeVariables: true)).Of(current)
                        : Compute(current, cascade, variables, parent?.Computed);
                    parent = new Cascade(cascade, variables, computed);
                    _cascaded.Add(current, parent);
                }

                return _cascaded[element].Computed;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NullReferenceException)
            {
                return null;
            }
            finally
            {
                _pending.Clear();
            }
        }

        private static void Inherit(ICssStyleDeclaration declarations, ICssStyleDeclaration parent)
        {
            // AngleSharp's UpdateDeclarations is internal. Replay its ordinary-property merge through
            // CSSOM, but leave custom-property inheritance to the resolved per-element graph.
            foreach (var property in parent)
            {
                if (property.Name.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                var own = declarations.GetProperty(property.Name);
                if (own is null ? property.CanBeInherited : own.IsInherited)
                {
                    declarations.RemoveProperty(property.Name);
                    declarations.SetProperty(property.Name, property.Value, property.IsImportant ? "important" : null);
                }
            }
        }

        private ICssStyleDeclaration? Compute(
            IElement element,
            ICssStyleDeclaration declarations,
            CustomProperties properties,
            ICssStyleDeclaration? inherited)
        {
            try
            {
                var context = new ComputeContext(_styles.Device, element.Owner?.Context, properties);
                var computed = declarations.Compute(context);
                if (scope == StyleScope.All)
                {
                    properties.ApplyTo(computed);
                }

                foreach (var property in declarations)
                {
                    if (!property.Name.StartsWith("--", StringComparison.Ordinal)
                        && property.CanBeInherited
                        && inherited is not null
                        && property.RawValue is CssReferenceValue
                        && property.Compute(context).RawValue is null)
                    {
                        // Native Compute fills initial values now, but reading its parent value needs
                        // its private context. Restore inheritance through this traversal's context.
                        var value = inherited.GetPropertyValue(property.Name);
                        if (value.Length != 0)
                        {
                            computed.RemoveProperty(property.Name);
                            computed.SetProperty(property.Name, value, property.IsImportant ? "important" : null);
                        }
                    }
                }

                return computed;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NullReferenceException)
            {
                return null;
            }
        }

        private sealed record Cascade(ICssStyleDeclaration Raw, CustomProperties Variables, ICssStyleDeclaration? Computed);

        private static void RetainScope(ICssStyleDeclaration declarations, StyleScope scope)
        {
            for (var index = declarations.Length - 1; index >= 0; index--)
            {
                var name = declarations[index];
                if (!Includes(scope, name))
                {
                    declarations.RemoveProperty(name);
                }
            }
        }

        // CSS Flexbox layout consumes these declarations only. Preserve native matching and variable
        // resolution, without computing paint values for every child whose synthetic box is requested.
        private static bool Includes(StyleScope scope, string name)
            => name is "display" or "visibility" or "all"
                || scope == StyleScope.Layout && name is "flex-direction" or "flex-wrap" or "direction"
                    or "align-self" or "align-items" or "flex-basis" or "width" or "flex-grow" or "flex-shrink"
                    or "flex" or "flex-flow" or "place-items" or "place-self";

        private sealed class ScopedStyles(IStyleCollection styles, StyleScope scope, bool includeVariables) : IStyleCollection
        {
            private readonly ICssStyleRule[] _rules = styles.Select(rule => new ScopedRule(rule,
                    rule.Style.Where(property => Includes(scope, property.Name)
                        || includeVariables && property.Name.StartsWith("--", StringComparison.Ordinal)).ToArray()))
                .Where(rule => rule.Style.Length != 0).ToArray();

            public IRenderDevice Device => styles.Device;

            public IEnumerator<ICssStyleRule> GetEnumerator() => ((IEnumerable<ICssStyleRule>) _rules).GetEnumerator();

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }

    // The native merge enumerates Rule.Style. Filtering only the rule list still makes it copy every
    // paint declaration in a mixed rule for every element, then remove those declarations afterwards.
    // Keep the original property objects: serializing/reparsing would lose pending shorthand values.
    private sealed class ScopedRule(ICssStyleRule source, ICssProperty[] properties) : ICssStyleRule
    {
        public ICssStyleDeclaration Style { get; } = new ScopedDeclaration(source.Style, properties);
        public string SelectorText { get => source.SelectorText; set => throw new NotSupportedException(); }
        public ISelector Selector => source.Selector;
        public ICssRuleList Rules => source.Rules;
        public CssRuleType Type => source.Type;
        public string CssText { get => source.CssText; set => throw new NotSupportedException(); }
        public ICssRule Parent => source.Parent;
        public ICssStyleSheet Owner => source.Owner;
        public bool TryMatch(IElement element, IElement? scope, out Priority specificity)
            => source.TryMatch(element, scope, out specificity);
        public void SetParent(ICssRule rule) => throw new NotSupportedException();
        public void SetOwner(ICssStyleSheet sheet) => throw new NotSupportedException();
        public void ToCss(TextWriter writer, IStyleFormatter formatter) => source.ToCss(writer, formatter);
    }

    /// <summary>Read-only merge input over the original native properties, never exposed to script.</summary>
    private sealed class ScopedDeclaration(ICssStyleDeclaration source, ICssProperty[] properties) : ICssStyleDeclaration
    {
        public string this[int index] => (uint) index < (uint) properties.Length ? properties[index].Name : "";
        public string this[string name] => GetPropertyValue(name);
        public int Length => properties.Length;
        public ICssRule? Parent => source.Parent;
        public event Action<string>? Changed { add { } remove { } }
        public void SetParent(ICssRule? rule) => throw new NotSupportedException();
        public string CssText { get => source.CssText; set => throw new NotSupportedException(); }
        public ICssProperty GetProperty(string name)
            => properties.FirstOrDefault(property => string.Equals(property.Name, name,
                name.StartsWith("--", StringComparison.Ordinal) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))!;
        public string GetPropertyValue(string name) => GetProperty(name)?.Value ?? "";
        public string GetPropertyPriority(string name) => GetProperty(name)?.IsImportant == true ? "important" : "";
        public void SetProperty(string name, string value, string? priority = null) => throw new NotSupportedException();
        public string RemoveProperty(string name) => throw new NotSupportedException();
        public void SetPropertyPriority(string name, string priority) => throw new NotSupportedException();
        public void SetDefaultProperty(string name, string value) => throw new NotSupportedException();
        public void Update(string value) => throw new NotSupportedException();
        public void ToCss(TextWriter writer, IStyleFormatter formatter) => source.ToCss(writer, formatter);
        public IEnumerator<ICssProperty> GetEnumerator() => ((IEnumerable<ICssProperty>) properties).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>The device and cycle-free variables AngleSharp's own value computation resolves against.</summary>
    private sealed class ComputeContext(
        IRenderDevice device,
        IBrowsingContext? context,
        CustomProperties properties) : ICssComputeContext
    {
        public IRenderDevice Device => device;
        public IBrowsingContext? Context => context;
        public IValueConverter? Converter => null;

        public ICssValue? Resolve(string name) => properties.Resolve(name);
    }
}
