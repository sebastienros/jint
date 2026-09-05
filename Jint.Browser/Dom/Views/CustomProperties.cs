using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp.Css.Values;
using AngleSharp.Text;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// https://drafts.csswg.org/css-variables-1/#cycles - resolve each element's dependency graph before
/// AngleSharp computes any property. Its recursive resolver has no cycle guard (#3851).
/// </summary>
internal sealed class CustomProperties
{
    private readonly Dictionary<string, Variable> _variables = new(StringComparer.Ordinal);

    internal CustomProperties(
        ICssStyleDeclaration declarations,
        CustomProperties? parent)
    {
        if (parent is not null)
        {
            foreach (var (name, inherited) in parent._variables)
            {
                _variables.Add(name, inherited);
            }
        }

        foreach (var property in declarations)
        {
            if (!property.Name.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var variable = new Variable(property.RawValue);
            _variables[property.Name] = variable;
            if (property.Value.Equals("inherit", StringComparison.OrdinalIgnoreCase)
                || property.Value.Equals("unset", StringComparison.OrdinalIgnoreCase))
            {
                variable.Value = parent?.Resolve(property.Name);
                variable.State = 2;
            }
            else if (property.Value.Equals("initial", StringComparison.OrdinalIgnoreCase))
            {
                variable.State = 2;
            }
        }

        // Iterative Tarjan traversal bounds CLR stack use for both long chains and long cycles.
        // Invalidate only a cyclic component, not a dependent which may recover with a fallback.
        var path = new List<Variable>();
        var component = new Stack<Variable>();
        var index = 0;
        foreach (var variable in _variables.Values)
        {
            if (variable.State != 0)
            {
                continue;
            }

            Enter(variable, path, component, index++);
            while (path.Count != 0)
            {
                var current = path[^1];
                if (current.Next < current.Dependencies.Count)
                {
                    var name = current.Dependencies[current.Next++];
                    if (!_variables.TryGetValue(name, out var dependency))
                    {
                        continue;
                    }

                    if (dependency.State == 0)
                    {
                        Enter(dependency, path, component, index++);
                    }
                    else if (dependency.State == 1)
                    {
                        current.LowLink = Math.Min(current.LowLink, dependency.Index);
                        current.Cyclic |= ReferenceEquals(current, dependency);
                    }
                }
                else
                {
                    path.RemoveAt(path.Count - 1);
                    if (path.Count != 0)
                    {
                        path[^1].LowLink = Math.Min(path[^1].LowLink, current.LowLink);
                    }

                    if (current.LowLink == current.Index)
                    {
                        var cyclic = current.Cyclic || !ReferenceEquals(component.Peek(), current);
                        Variable member;
                        do
                        {
                            member = component.Pop();
                            member.Value = cyclic ? null : ResolveValue(member.Raw);
                            member.State = 2;
                        } while (!ReferenceEquals(member, current));
                    }
                }
            }
        }
    }

    internal ICssValue? Resolve(string name)
        => _variables.TryGetValue(name, out var variable) ? variable.Value : null;

    internal void ApplyTo(ICssStyleDeclaration computed)
    {
        foreach (var (name, variable) in _variables)
        {
            computed.RemoveProperty(name);
            if (variable.Value is { } value)
            {
                computed.SetProperty(name, value.CssText);
            }
        }
    }

    private static void Enter(Variable variable, List<Variable> path, Stack<Variable> component, int index)
    {
        variable.State = 1;
        variable.Index = index;
        variable.LowLink = index;
        path.Add(variable);
        component.Push(variable);

        var pending = new Stack<ICssValue>();
        if (variable.Raw is { } raw)
        {
            pending.Push(raw);
        }

        while (pending.TryPop(out var value))
        {
            value = ParseReferences(value);
            if (value is CssReferenceValue references)
            {
                foreach (var reference in references.References)
                {
                    pending.Push(reference);
                }
            }
            else if (value is CssVarValue reference)
            {
                variable.Dependencies.Add(reference.VariableName);
                if (reference.DefaultValue is { } fallback)
                {
                    // Fallback edges count even when the primary reference has a value.
                    pending.Push(fallback);
                }
            }
        }
    }

    private ICssValue? ResolveValue(ICssValue? value)
    {
        // Preserve AngleSharp's first-resolvable-reference ordering, but never recursively follow a
        // variable or a fallback. General token-stream substitution remains an upstream concern.
        var pending = new Stack<ICssValue>();
        if (value is not null)
        {
            pending.Push(value);
        }

        while (pending.TryPop(out value))
        {
            value = ParseReferences(value);
            if (value is CssReferenceValue references)
            {
                for (var i = references.References.Length - 1; i >= 0; i--)
                {
                    pending.Push(references.References[i]);
                }
            }
            else if (value is CssVarValue variable)
            {
                if (Resolve(variable.VariableName) is { } resolved)
                {
                    return resolved;
                }

                if (variable.DefaultValue is { } fallback)
                {
                    pending.Push(fallback);
                }
            }
            else
            {
                return value;
            }
        }

        return null;
    }

    private static ICssValue ParseReferences(ICssValue value)
    {
        // AngleSharp leaves a fallback such as calc(var(--a)) as raw text. Its own parser must
        // expose those edges too, including when the primary value makes that fallback unused.
        if (value is ICssRawValue and not CssReferenceValue
            && value.CssText.Contains("var", StringComparison.OrdinalIgnoreCase))
        {
            return new StringSource(value.CssText).ParseVars() ?? value;
        }

        return value;
    }

    private sealed class Variable(ICssValue? raw)
    {
        internal ICssValue? Raw { get; } = raw;
        internal ICssValue? Value { get; set; }
        internal List<string> Dependencies { get; } = new();
        internal int State { get; set; }
        internal int Index { get; set; }
        internal int LowLink { get; set; }
        internal int Next { get; set; }
        internal bool Cyclic { get; set; }
    }
}
