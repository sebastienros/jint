using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Microsoft.Playwright;

namespace Jint.Browser.Playwright;

internal abstract class ProxyTarget
{
    internal object Proxy { get; set; } = null!;

    internal abstract object? Invoke(MethodInfo method, object?[] arguments);

    protected static object? Unsupported(MethodInfo method)
        => UnsupportedOperation.For(method);
}

[SuppressMessage(
    "Performance",
    "CA1852:Seal internal types",
    Justification = "DispatchProxy generates a runtime subclass of this type.")]
internal class PlaywrightDispatchProxy : DispatchProxy
{
    internal ProxyTarget Target { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        if (targetMethod.DeclaringType == typeof(object))
        {
            return targetMethod.Name switch
            {
                nameof(ToString) => Target.GetType().Name,
                nameof(GetHashCode) => RuntimeHelpers.GetHashCode(this),
                nameof(Equals) => ReferenceEquals(this, args![0]),
                _ => throw new NotSupportedException(targetMethod.Name),
            };
        }

        return Target.Invoke(targetMethod, args ?? []);
    }
}

internal static class ProxyFactory
{
    internal static T Create<T>(ProxyTarget target) where T : class
    {
        var proxy = DispatchProxy.Create<T, PlaywrightDispatchProxy>();
        target.Proxy = proxy;
        ((PlaywrightDispatchProxy) (object) proxy).Target = target;
        return proxy;
    }
}

internal static class UnsupportedOperation
{
    private static readonly MethodInfo FaultedTaskMethod = typeof(UnsupportedOperation)
        .GetMethod(nameof(FaultedTask), BindingFlags.Static | BindingFlags.NonPublic)!;

    internal static object? For(MethodInfo method)
    {
        var exception = new NotSupportedException(
            $"Jint.Browser.Playwright does not support {method.DeclaringType?.Name}.{DisplayName(method)}.");

        if (method.ReturnType == typeof(Task))
        {
            return Task.FromException(exception);
        }

        if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return FaultedTaskMethod.MakeGenericMethod(method.ReturnType.GenericTypeArguments[0])
                .Invoke(null, [exception]);
        }

        if (method.ReturnType == typeof(ValueTask))
        {
            return new ValueTask(Task.FromException(exception));
        }

        throw exception;
    }

    private static Task<T> FaultedTask<T>(Exception exception) => Task.FromException<T>(exception);

    private static string DisplayName(MethodInfo method)
        => method.IsSpecialName && method.Name.StartsWith("get_", StringComparison.Ordinal)
            ? method.Name[4..]
            : method.Name;
}

internal static class OptionSupport
{
    internal static void EnsureOnly(object? options, string operation, params string[] supported)
    {
        if (options is null)
        {
            return;
        }

        foreach (var property in options.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var value = property.GetValue(options);
            if (value is null || IsDefaultValue(value, property.PropertyType))
            {
                continue;
            }

            if (supported.Contains(property.Name, StringComparer.Ordinal))
            {
                continue;
            }

            throw new NotSupportedException(
                $"Jint.Browser.Playwright does not support option {options.GetType().Name}.{property.Name} for {operation}.");
        }
    }

    private static bool IsDefaultValue(object value, Type type)
        => type.IsValueType && Equals(value, Activator.CreateInstance(type));
}

internal sealed class CleanupErrors
{
    private List<Exception>? _exceptions;

    internal bool HasAny => _exceptions is { Count: > 0 };

    internal void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            (_exceptions ??= []).Add(exception);
        }
    }

    internal async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            (_exceptions ??= []).Add(exception);
        }
    }

    internal void ThrowIfAny()
    {
        if (_exceptions is not { Count: > 0 } exceptions)
        {
            return;
        }

        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        throw new AggregateException(exceptions);
    }
}
