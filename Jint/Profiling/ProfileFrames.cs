using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter;

namespace Jint.Profiling;

/// <summary>
/// How both profilers name, locate and classify one function. Shared so that a frame means the same thing
/// whichever instrument produced it, and so that neither of them grows its own answer to "what is this
/// function called".
/// </summary>
internal static class ProfileFrames
{
    internal const string AnonymousFrameName = "<anonymous>";

    /// <summary>
    /// The name of the synthetic frame every sampled stack is rooted at: the program itself, which is not on
    /// the call stack because only function activations are.
    /// </summary>
    internal const string ProgramFrameName = "(program)";

    private static readonly Assembly JintAssembly = typeof(Engine).GetTypeInfo().Assembly;

    /// <summary>
    /// Identity comparison for the interning maps both profilers keep. Hand-written because
    /// <c>ReferenceEqualityComparer</c> is .NET 5+ and Jint still targets net472 and netstandard2.0, and
    /// because the keys — a <see cref="JintFunctionDefinition"/> or a <see cref="Function"/> — must not be
    /// compared by any <c>Equals</c> override they may have.
    /// </summary>
    internal static readonly IEqualityComparer<object> FunctionIdentity = new IdentityComparer();

    /// <summary>
    /// The name, file, declaration position and owning program to show for <paramref name="function"/>.
    /// </summary>
    internal static ScriptProfileFrame Describe(Function function, JintFunctionDefinition? definition)
    {
        var name = ResolveName(function, definition);

        var node = (Node?) definition?.Function;
        if (node is null)
        {
            return new ScriptProfileFrame(name, File: null, Line: null, Column: null);
        }

        var location = node.Location;
        if (location == default)
        {
            return new ScriptProfileFrame(name, File: null, Line: null, Column: null);
        }

        var file = string.IsNullOrEmpty(location.SourceFile) ? null : location.SourceFile;

        // Column is reported one-based, matching the column Jint puts in a stack trace; the parser's is an
        // index.
        // The function's [[ScriptOrModule]] is the script that was active when it was created, which is the
        // program its body belongs to for everything but eval and the Function constructor — and those two
        // are what OwningProgramOf declines rather than mis-attributing.
        return new ScriptProfileFrame(
            name,
            file,
            location.Start.Line,
            location.Start.Column + 1,
            function._scriptOrModule.OwningProgramOf(node));
    }

    /// <summary>
    /// The name to show for a function, resolved once when its frame is interned and without running any
    /// script: the name its declaration carries, else the own <c>name</c> property's stored value when that
    /// is a plain string (which is where an inferred name such as the <c>f</c> of
    /// <c>var f = function () {}</c> lives), else <see cref="AnonymousFrameName"/>.
    /// </summary>
    /// <remarks>
    /// Two things decide the order. A frame is interned by definition, so the declared name is the one that
    /// identifies all of its instances, and reading it first also means a script function's <em>pending</em>
    /// name descriptor — the shared sentinel whose value accessors throw to make a leak loud — is never
    /// touched, since that sentinel only stands in for a declared name in the first place. And a descriptor
    /// carrying <see cref="PropertyFlag.CustomJsValue"/> is declined rather than read: resolving one runs
    /// host code, which is a profiler's business least of all.
    /// </remarks>
    internal static string ResolveName(Function function, JintFunctionDefinition? definition)
    {
        var declared = definition?.Name;
        if (!string.IsNullOrEmpty(declared))
        {
            return declared!;
        }

        var descriptor = function._nameDescriptor;
        if (descriptor is not null
            && (descriptor.Flags & PropertyFlag.CustomJsValue) == PropertyFlag.None
            && descriptor.Value is JsString ownName)
        {
            var name = ownName.ToString();
            if (name.Length > 0)
            {
                return name;
            }
        }

        return AnonymousFrameName;
    }

    /// <summary>
    /// Whose code <paramref name="function"/> runs, by the only question that separates the three: which
    /// assembly the body belongs to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A function parsed from source is the script's, whatever produced the source. Everything else is
    /// native, and the split there is not a type test, because <see cref="ClrFunction"/> is the one type
    /// both parties use — the engine wires most of its own built-ins with it and a host registers its
    /// callables with it — so the delegate's own method decides. The interop wrappers are the mirror case:
    /// Jint's types running the host's code, and they are named because their own type would answer
    /// "built-in".
    /// </para>
    /// <para>
    /// The classification is resolved once per interned function, never per sample.
    /// </para>
    /// </remarks>
    internal static ProfileFrameCategory Classify(Function function)
    {
        if (function._functionDefinition is not null)
        {
            return ProfileFrameCategory.Script;
        }

        if (function is MethodInfoFunction or DelegateWrapper or GetterFunction or SetterFunction)
        {
            return ProfileFrameCategory.HostInterop;
        }

        var implementation = function is ClrFunction clr
            ? clr._func.Method.DeclaringType
            : function.GetType();

        // A null declaring type is a delegate the host emitted at run time, which is host code by
        // definition.
        return implementation is not null && ReferenceEquals(implementation.GetTypeInfo().Assembly, JintAssembly)
            ? ProfileFrameCategory.BuiltIn
            : ProfileFrameCategory.HostInterop;
    }

    private sealed class IdentityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
