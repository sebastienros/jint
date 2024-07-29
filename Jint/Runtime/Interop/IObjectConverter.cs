using System.Diagnostics.CodeAnalysis;
using Jint.Native;

namespace Jint.Runtime.Interop;

/// <summary>
/// When implemented, converts a CLR value to a <see cref="JsValue"/> instance
/// </summary>
public interface IObjectConverter
{
    bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue? result);
}
