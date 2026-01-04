using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.Native.AsyncGenerator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asyncgenerator-objects
/// Represents a pending next/return/throw request for an async generator.
/// Each request has a PromiseCapability that resolves when the request completes.
/// </summary>
internal readonly record struct AsyncGeneratorRequest(Completion Completion, PromiseCapability Capability);
