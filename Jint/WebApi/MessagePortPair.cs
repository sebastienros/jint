#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;

namespace Jint.WebApi;

/// <summary>
/// Two entangled <c>MessagePort</c> objects, one belonging to each of the engines
/// <c>Engine.WebApi.CreateMessagePortPair</c> was given. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// Each half is an ordinary <c>MessagePort</c> as far as script is concerned: give it to its own engine —
/// <c>engine.SetValue("port", pair.Local)</c> is the usual way — and the script calls <c>postMessage</c>,
/// <c>start</c>, <c>close</c> and <c>onmessage</c> on it exactly as it would on a port from a
/// <c>MessageChannel</c>.
/// </para>
/// <para>
/// <b>Each port belongs to its own engine and may only ever be touched from that engine's thread.</b>
/// <see cref="Local"/> is a <c>JsValue</c> of the engine the method was called on and <see cref="Remote"/> one
/// of the engine that was passed in; handing either to the wrong engine is the unsupported cross-engine
/// sharing of a <c>JsValue</c> that Jint has never allowed. What crosses between the two engines is not a
/// value at all — it is a serialization record with nothing engine-affine in it — and it crosses through the
/// receiving engine's event-loop queue, which is the one part of an engine any thread may touch.
/// </para>
/// </remarks>
/// <param name="Local">
/// The port owned by the engine <c>CreateMessagePortPair</c> was called on.
/// </param>
/// <param name="Remote">
/// The port owned by the engine passed as the argument.
/// </param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct MessagePortPair(JsValue Local, JsValue Remote);
#endif
