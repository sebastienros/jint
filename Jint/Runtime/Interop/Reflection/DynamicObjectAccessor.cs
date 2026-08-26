using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using Jint.Native;

namespace Jint.Runtime.Interop.Reflection;

internal sealed class DynamicObjectAccessor : ReflectionAccessor
{
    private JintSetMemberBinder? _setter;
    private JintGetMemberBinder? _getter;

    public DynamicObjectAccessor() : base(memberType: null)
    {
    }

    public override bool Writable => true;

    protected override object? DoGetValue(object target, string memberName)
    {
        var dynamicObject = (DynamicObject) target;
        var getter = _getter ??= new JintGetMemberBinder(memberName, ignoreCase: true);
        dynamicObject.TryGetMember(getter, out var result);
        return result;
    }

    protected override void DoSetValue(object target, string memberName, object? value)
    {
        var dynamicObject = (DynamicObject) target;
        var setter = _setter ??= new JintSetMemberBinder(memberName, ignoreCase: true);
        dynamicObject.TrySetMember(setter, value);
    }

    protected override object? ConvertValueToSet(Engine engine, object value)
    {
        // we expect value to be generally CLR type, convert when possible
        return value switch
        {
            JsBoolean jsBoolean => jsBoolean._value ? JsBoolean.BoxedTrue : JsBoolean.BoxedFalse,
            JsString jsString => jsString.ToString(),
            JsNumber jsNumber => jsNumber._value,
            JsNull => null,
            JsUndefined => null,
            _ => value
        };
    }

    /// <remarks>
    /// The two binders below are never installed in a <see cref="System.Runtime.CompilerServices.CallSite"/>.
    /// They are handed straight to <see cref="DynamicObject.TryGetMember"/> /
    /// <see cref="DynamicObject.TrySetMember"/>, which dispatches to the host's own override — which is why
    /// their <c>Fallback*</c> members throw: reaching one would mean the DLR was driving, and it never is.
    /// The <c>[RequiresDynamicCode]</c> on the base constructors is about that call-site path
    /// ("Creating a call site may require dynamic code generation"), so it does not describe this use, and
    /// the suppression says so rather than the csproj silencing it for everyone.
    /// </remarks>
    [UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
        Justification = "The binder is only ever passed to DynamicObject.TryGetMember, never used to build a " +
                        "CallSite, which is what the base constructor's attribute is about.")]
    private sealed class JintGetMemberBinder : GetMemberBinder
    {
        public JintGetMemberBinder(string name, bool ignoreCase) : base(name, ignoreCase)
        {
        }

        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject? errorSuggestion)
        {
            throw new NotImplementedException(nameof(FallbackGetMember) + " not implemented");
        }
    }

    [UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
        Justification = "The binder is only ever passed to DynamicObject.TrySetMember, never used to build a " +
                        "CallSite, which is what the base constructor's attribute is about.")]
    private sealed class JintSetMemberBinder : SetMemberBinder
    {
        public JintSetMemberBinder(string name, bool ignoreCase) : base(name, ignoreCase)
        {
        }

        public override DynamicMetaObject FallbackSetMember(
            DynamicMetaObject target,
            DynamicMetaObject value,
            DynamicMetaObject? errorSuggestion)
        {
            throw new NotImplementedException(nameof(FallbackSetMember) + " not implemented");
        }
    }
}
