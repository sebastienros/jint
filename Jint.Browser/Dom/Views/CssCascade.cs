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
    internal static ICssStyleDeclaration? Of(IElement element)
    {
        try
        {
            var traversal = Traversal.For(element.Owner);
            return traversal is null ? element.ComputeCurrentStyle() : traversal.Of(element);
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
    internal sealed class Traversal(IStyleCollection styles)
    {
        private readonly Dictionary<IElement, Cascade> _cascaded = new();
        private readonly Stack<IElement> _pending = new();

        internal static Traversal? For(IDocument? document)
        {
            if (document?.DefaultView is not { } window)
            {
                return null;
            }

            var device = document.Context.GetService<IRenderDevice>() ?? new DefaultRenderDevice();
            return new Traversal(window.GetStyleCollection(device));
        }

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
                    // Capture local variables before inheritance. A rule matching both parent and
                    // child shares property objects, so reference identity cannot identify inheritance.
                    var cascade = styles.ComputeExplicitStyle(current);
                    var variables = new CustomProperties(cascade, parent?.Variables);
                    if (parent is not null)
                    {
                        Inherit(cascade, parent.Raw);
                    }

                    // AngleSharp's ancestor walk can resolve an explicit inherit past a parent that
                    // declares nothing for a non-inherited property. Preserve that answer too.
                    if (current.ParentElement is not null
                        && cascade.Any(static property => property.IsInherited && !property.CanBeInherited))
                    {
                        cascade = styles.GetDeclarations(current);
                    }

                    parent = new Cascade(cascade, variables, Compute(current, cascade, variables, parent?.Computed));
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
                var computed = declarations.Compute(new ComputeContext(styles.Device, element.Owner?.Context, properties));
                properties.ApplyTo(computed);
                foreach (var property in declarations)
                {
                    if (!property.Name.StartsWith("--", StringComparison.Ordinal)
                        && property.RawValue is CssReferenceValue
                        && string.IsNullOrEmpty(computed.GetPropertyValue(property.Name)))
                    {
                        // Invalid at computed-value time behaves as unset, not as a lower-priority
                        // declaration. Keep the existing initial-value policy for non-inherited values.
                        computed.RemoveProperty(property.Name);
                        if (property.CanBeInherited && inherited is not null)
                        {
                            var value = inherited.GetPropertyValue(property.Name);
                            if (value.Length != 0)
                            {
                                computed.SetProperty(property.Name, value);
                            }
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
