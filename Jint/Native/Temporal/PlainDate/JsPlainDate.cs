using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal-plaindate-objects
/// </summary>
internal sealed class JsPlainDate : ObjectInstance
{
    internal JsPlainDate(Engine engine, ObjectInstance prototype, IsoDate isoDate, string calendar) : base(engine)
    {
        _prototype = prototype;
        IsoDate = isoDate;
        Calendar = calendar;
    }

    internal IsoDate IsoDate { get; }
    internal string Calendar { get; }

    internal override bool IsTemporalPlainDate => true;
}
