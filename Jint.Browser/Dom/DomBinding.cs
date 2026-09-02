using System.Runtime.InteropServices;

namespace Jint.Browser.Dom;

/// <summary>
/// A brand-checked receiver: the AngleSharp object a generated member calls, and the realm it belongs to.
/// </summary>
/// <typeparam name="T">The AngleSharp interface the member is declared on.</typeparam>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct DomBinding<T>(T Target, DomRealm Realm) where T : class;
