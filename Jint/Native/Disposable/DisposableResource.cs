using Jint.Runtime;

namespace Jint.Native.Disposable;

internal sealed record DisposableResource(JsValue ResourceValue, DisposeHint Hint, Function.Function? DisposeMethod);
