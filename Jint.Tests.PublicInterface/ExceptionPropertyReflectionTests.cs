#nullable enable

using System.Reflection;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Everything that renders a failed run reads a thrown exception's public properties reflectively - NUnit's
/// <c>ExceptionHelper</c>, xUnit's equivalent, Serilog's destructuring, an ASP.NET error page - so those
/// getters are third-party surface even though no host calls them by name.
/// </summary>
/// <remarks>
/// <para>
/// The one shape that breaks it is a by-ref-returning property. .NET Framework's
/// <see cref="PropertyInfo.GetValue(object)"/> answers one with
/// <c>NotSupportedException: ByRef return value not supported in reflection invocation</c> rather than
/// dereferencing it the way .NET Core does, and a renderer that did not expect a getter to fail that way
/// prints the reflection message in place of the failure - or, in NUnit's case, takes the test host down and
/// with it every test still queued behind it. That is
/// <see href="https://github.com/sebastienros/jint/issues/3549">#3549</see>, and it cost a <c>net472</c>
/// run its whole result rather than one red test.
/// </para>
/// <para>
/// Nothing about it is NUnit-specific and nothing about it is test-specific: the same getter is what an
/// embedder's logger calls. So the rule is Jint's rather than the runner's - an exception Jint can throw
/// exposes no by-ref-returning public property - and both tests here hold it, one by structure and one by
/// replaying what a renderer actually does.
/// </para>
/// </remarks>
public class ExceptionPropertyReflectionTests
{
    /// <summary>
    /// Every exception type in the assembly, not only the public ones: a renderer walks
    /// <see cref="Exception.InnerException"/>, and Jint's is a private nested class whose properties are
    /// enumerated all the same.
    /// </summary>
    [Test]
    public void NoExceptionTypeExposesAByRefReturningProperty()
    {
        var offenders = new List<string>();

        foreach (var type in typeof(Engine).Assembly.GetTypes())
        {
            if (!typeof(Exception).IsAssignableFrom(type))
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.PropertyType.IsByRef)
                {
                    offenders.Add($"{type.FullName}.{property.Name}");
                }
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            $"""
            By-ref-returning public properties on exception types: {string.Join(", ", offenders)}

            A public property of an exception is read by PropertyInfo.GetValue by every failure renderer, and
            .NET Framework answers a by-ref-returning one with NotSupportedException instead of dereferencing
            it. Return the value, and keep the by-ref accessor internal if a caller needs one.
            """);
    }

    /// <summary>
    /// The renderer's half of the same rule, replaying what NUnit's <c>ExceptionHelper</c> does: enumerate the
    /// public instance properties of the exception and of every inner exception, and read each one.
    /// </summary>
    [Test]
    public void AFailureRendererCanReadEveryPropertyOfAThrownJavaScriptException()
    {
        var engine = new Engine();
        var thrown = Caught.Exception(() => engine.Execute("function boom() { throw new Error('kaboom'); } boom();"));

        thrown.Should().BeOfType<JavaScriptException>();

        var read = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var exception = thrown; exception is not null; exception = exception.InnerException)
        {
            var type = exception.GetType();
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // What NUnit does verbatim, including the by-value read that used to throw here on net472.
                var value = property.GetValue(exception);
                read[$"{type.Name}.{property.Name}"] = value;
            }
        }

        read.Should().ContainKey("JavaScriptException.Location");
        read["JavaScriptException.Location"].Should().BeOfType<SourceLocation>()
            .Which.Start.Line.Should().Be(1, "the location has to survive the by-value read, not merely not throw");
    }
}
