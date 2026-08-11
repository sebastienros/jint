using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Global;

/// <summary>
/// %parseInt% and %parseFloat% — one function object per realm each, because
/// https://tc39.es/ecma262/#sec-number.parseint and https://tc39.es/ecma402/#sec-number.parsefloat
/// require <c>Number.parseInt</c> and <c>Number.parseFloat</c> to be <em>the same</em> function objects
/// as the ones on the global object.
/// </summary>
/// <remarks>
/// <para>
/// Both used to be a pair of <see cref="Runtime.Interop.ClrFunction"/>s over the same static method,
/// one built by <see cref="GlobalObject"/> and one by <c>NumberConstructor</c>, with
/// <c>ClrFunction.Equals</c> comparing the underlying delegate so that <c>Number.parseInt === parseInt</c>
/// answered true for two distinct objects. Equal-but-distinct was observable in the one place the two
/// were not built alike: the constructor's copies declared <c>length</c> 0, so <c>parseInt.length</c>
/// was 2 while <c>Number.parseInt.length</c> was 0 for a function the same test says is the same
/// function. Sharing one object makes the sameness real and the lengths agree.
/// </para>
/// <para>
/// Sharing is also what unblocks the fast-call lane, which is the reason for a purpose-built type
/// rather than a shared <c>ClrFunction</c>. A <c>ClrFunction</c>'s <c>JsCallDelegate</c> takes a
/// <c>JsValue[]</c>, so it can offer no arity-specialized entry point, and every call to these two
/// paid the generic arm: a pooled argument array, a call-stack frame, and a virtual hop through the
/// delegate. <see cref="CallFast"/> takes the arguments in registers, and the shapes below claim
/// <see cref="FastCallShape.Leaf"/> under guards that make both coercions no-ops, so a warmed
/// <c>parseInt(s, 10)</c> site elides the frame as well.
/// </para>
/// <para>
/// The guards are what keep <c>Leaf</c> honest. <c>ToString</c> of a <c>JsString</c> and
/// <c>ToInt32</c> of a number or of the <c>undefined</c> a one-argument site pads the second register
/// with are both no-ops that cannot reach user code, and neither body raises a JavaScript error —
/// an unparseable input is <c>NaN</c>, not a throw. Any other argument shape (an object with
/// <c>valueOf</c>, most of all) fails the guard and keeps its frame.
/// </para>
/// </remarks>
internal sealed class NumberParseFunction : Function.Function
{
    private static readonly JsString _parseIntName = new("parseInt");
    private static readonly JsString _parseFloatName = new("parseFloat");

    /// <summary>
    /// parseInt reads both registers. parseFloat reads only the first, so its second is
    /// <see cref="FastCallGuard.Any"/> — the lane always tests both, and a site passing a stray
    /// second argument to parseFloat must not lose frame elision over a value the body never sees.
    /// </summary>
    private static readonly FastCallShape _parseIntShape = new(
        Supported: true,
        Leaf: true,
        Variadic: false,
        Receiver: FastCallGuard.Any,
        Arg0: FastCallGuard.String,
        Arg1: FastCallGuard.Number | FastCallGuard.Undefined);

    private static readonly FastCallShape _parseFloatShape = new(
        Supported: true,
        Leaf: true,
        Variadic: false,
        Receiver: FastCallGuard.Any,
        Arg0: FastCallGuard.String,
        Arg1: FastCallGuard.Any);

    private readonly bool _isParseInt;

    internal NumberParseFunction(Engine engine, Realm realm, FunctionPrototype functionPrototype, bool isParseInt)
        : base(engine, realm, isParseInt ? _parseIntName : _parseFloatName)
    {
        _isParseInt = isParseInt;
        _prototype = functionPrototype;
        _length = new PropertyDescriptor(JsNumber.Create(isParseInt ? 2 : 1), PropertyFlag.Configurable);
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
        => _isParseInt
            ? GlobalObject.ParseInt(arguments.At(0), arguments.At(1))
            : GlobalObject.ParseFloat(arguments.At(0));

    /// <remarks>
    /// The arity is not consulted: both shapes are fixed-arity, and the built-in lane only ever asks
    /// for a site of at most two arguments, so every arity it can ask about is one the two argument
    /// registers carry in full.
    /// </remarks>
    internal override FastCallShape GetFastCallShape(int argumentCount)
        => _isParseInt ? _parseIntShape : _parseFloatShape;

    internal override JsValue CallFast(JsValue thisObject, JsValue arg0, JsValue arg1)
        => _isParseInt
            ? GlobalObject.ParseInt(arg0, arg1)
            : GlobalObject.ParseFloat(arg0);
}
