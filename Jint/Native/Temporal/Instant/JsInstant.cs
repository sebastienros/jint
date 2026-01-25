using System.Numerics;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal-instant-objects
/// </summary>
internal sealed class JsInstant : ObjectInstance
{
    internal JsInstant(Engine engine, ObjectInstance prototype, BigInteger epochNanoseconds) : base(engine)
    {
        _prototype = prototype;
        EpochNanoseconds = epochNanoseconds;
    }

    /// <summary>
    /// The internal slot holding the nanoseconds since Unix epoch.
    /// </summary>
    internal BigInteger EpochNanoseconds { get; }

    internal override bool IsTemporalInstant => true;
}
