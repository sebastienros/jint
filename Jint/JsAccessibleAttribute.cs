namespace Jint;

/// <summary>
/// Marks a CLR class for source-generator-driven interop. The Jint source generator emits
/// typed accessor classes for the type's public properties and methods and wires them into
/// <see cref="Jint.Runtime.Interop.GeneratedAccessorRegistry"/> via a module initializer.
/// JS-side property reads/writes and method invocations on instances of the type bypass the
/// reflection-based <c>PropertyInfo.GetValue</c>/<c>MethodInfo.Invoke</c> hot path entirely.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class JsAccessibleAttribute : Attribute
{
}
