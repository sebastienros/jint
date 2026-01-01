using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.Native.AsyncGenerator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asyncgenerator-objects
/// Represents a pending next/return/throw request for an async generator.
/// Each request has a PromiseCapability that resolves when the request completes.
/// </summary>
internal sealed class AsyncGeneratorRequest
{
    public Completion Completion { get; }
    public PromiseCapability Capability { get; }

    public AsyncGeneratorRequest(Completion completion, PromiseCapability capability)
    {
        Completion = completion;
        Capability = capability;
    }
}
