using System.Reflection;
using Jint.Native;

namespace Jint.Runtime.Interop;

/// <summary>
/// Describes a failed CLR interop method or constructor resolution, passed to
/// <see cref="Options.ClrResolutionErrorDecoratorDelegate"/>. Carries the full structured
/// information regardless of <see cref="Options.InteropOptions.ExposeDetailedResolutionErrors"/>;
/// the decorator runs host-side, so nothing here is visible to the running script unless the
/// decorator writes it onto the error object.
/// </summary>
public sealed class ClrResolutionErrorInfo
{
    private readonly JsValue[] _arguments;
    private readonly MethodDescriptor[] _candidates;

    // lazily computed projections, benign race - last writer wins
    private MethodBase[]? _candidateMembers;
    private string[]? _candidateSignatures;
    private string? _detailedMessage;

    internal ClrResolutionErrorInfo(Type type, string? memberName, JsCallArguments arguments, MethodDescriptor[]? candidates)
    {
        Type = type;
        MemberName = memberName;
        // defensive element-wise copy: the incoming array can be a shared expression-cache array or
        // rented from the engine's argument pool, both reused once the call site unwinds. The cached
        // variant is also an Unsafe.As-reinterpreted array (see ExpressionCache.ArgumentListEvaluation)
        // whose runtime type is not JsValue[], so Span/Array.Copy based copies would throw.
        var copy = new JsValue[arguments.Length];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = arguments[i];
        }
        _arguments = copy;
        _candidates = candidates ?? [];
    }

    /// <summary>
    /// The CLR type the failed call targeted.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// The invoked member name, or <c>null</c> for constructor resolution failures.
    /// </summary>
    public string? MemberName { get; }

    /// <summary>
    /// Whether a constructor call failed to resolve (as opposed to a method call).
    /// </summary>
    public bool IsConstructor => MemberName is null;

    /// <summary>
    /// The JavaScript argument values the script supplied. The list is a copy and safe to retain.
    /// </summary>
    public IReadOnlyList<JsValue> Arguments => _arguments;

    /// <summary>
    /// The candidate CLR members that were considered but did not match the supplied arguments.
    /// </summary>
    public IReadOnlyList<MethodBase> Candidates => _candidateMembers ??= Array.ConvertAll(_candidates, static x => x.Method);

    /// <summary>
    /// Renderings of the candidate signatures, in the same format the detailed resolution error message uses,
    /// e.g. <c>Say(String message)</c>.
    /// </summary>
    public IReadOnlyList<string> CandidateSignatures => _candidateSignatures ??= Array.ConvertAll(_candidates, static x => x.ToString());

    /// <summary>
    /// The detailed resolution error message naming the target type, the provided argument types and the
    /// candidate signatures. Available even when
    /// <see cref="Options.InteropOptions.ExposeDetailedResolutionErrors"/> is disabled - it is only handed
    /// to the host-side decorator, which decides what the script gets to see.
    /// </summary>
    public string DetailedMessage => _detailedMessage ??= IsConstructor
        ? InteropErrorHelper.CreateNoMatchingConstructorMessage(Type, _arguments, _candidates)
        : InteropErrorHelper.CreateNoMatchingMethodMessage(Type, MemberName!, _arguments, _candidates);
}
