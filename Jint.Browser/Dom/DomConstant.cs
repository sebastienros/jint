using System.Runtime.InteropServices;

namespace Jint.Browser.Dom;

/// <summary>
/// One WebIDL constant — <c>Node.ELEMENT_NODE</c>, <c>CSSRule.STYLE_RULE</c>. Its attributes are fixed by
/// <a href="https://webidl.spec.whatwg.org/#es-constants">the specification</a>:
/// <c>{ writable: false, enumerable: true, configurable: false }</c>.
/// </summary>
/// <param name="Name">The constant's name.</param>
/// <param name="Value">Its numeric value.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct DomConstant(string Name, double Value);
