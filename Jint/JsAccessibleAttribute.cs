namespace Jint;

/// <summary>
/// Opts a CLR type into source-generated interop. The Jint source generator emits, for every public
/// instance property, field and method of an annotated type that it can express, the same typed
/// read/write/invoke lanes the engine otherwise builds at run time with
/// <see cref="System.Linq.Expressions.Expression"/> trees — but as ordinary C# in the annotated type's
/// own assembly, so the member is reached without reflection and without runtime code generation.
/// </summary>
/// <remarks>
/// <para>
/// The attribute alone changes nothing: the generated code registers itself only when the host calls the
/// generated <c>JsAccessibleRegistration.RegisterAll()</c> entry point, and nothing about an un-annotated
/// type — or an annotated one whose registration was never called — is affected. A member the generator
/// cannot express is simply not registered and resolves through reflection exactly as before.
/// </para>
/// <para>
/// This is not an AOT claim. It is two narrower ones: the annotated members need no metadata a trimmer
/// could remove, and reading or invoking one runs no reflection.
/// </para>
/// <para>
/// Reference types only. An instance member of a value type is deliberately outside the feature for the
/// same reason the run-time compiled lanes exclude it: the typed access would run against a boxed copy,
/// so a write would not reach the original.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class JsAccessibleAttribute : Attribute
{
}
