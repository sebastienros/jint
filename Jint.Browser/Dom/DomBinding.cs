using System.Runtime.InteropServices;

namespace Jint.Browser.Dom;

/// <summary>
/// A brand-checked native receiver and the realm its generated member belongs to.
/// </summary>
/// <typeparam name="T">The native receiver type declared by the binding contract.</typeparam>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct DomBinding<T>(T Target, DomRealm Realm) where T : class;
